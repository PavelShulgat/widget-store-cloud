# Cloud Database Architecture — Widget & Co

Proof of concept for migrating the Widget & Co online store from ISP hosting to Azure.
No frontend; the deliverable is the data architecture and the service layer around it.

---

## 1. The problem being solved

The scenario names one concrete failure: *"the order placing process has been the main
culprit in the sluggish performance of the website"* during peak hours. Everything below
follows from treating that as the primary constraint rather than as one requirement among
many.

A single relational database serving both the product catalogue and the order write path
couples them: a burst of order inserts contends for the same connection pool, locks and IO
that catalogue reads need. The fix is not a bigger database but a different shape — the
write path is decoupled from storage entirely, and each kind of data is placed in the
service whose access pattern it actually matches.

---

## 2. Service selection

| Data | Service | Reasoning |
|---|---|---|
| Product specifications | Azure SQL + EF Core | Structured, relational, low write volume, benefits from joins and constraints. The scenario states it is already in SQL; there is no reason to move it. |
| Product images | Blob Storage | Binary objects of unbounded size. Storing them in SQL inflates row size and backup time for no benefit. Only the blob URL is kept in the product row. |
| Incoming orders (write) | Queue Storage | Decouples accepting an order from persisting it. See §3. |
| Orders (persisted) | Table Storage | High write volume, always accessed by customer, no joins required. Cheaper per transaction than SQL and scales horizontally by partition. |
| Computed metrics | Table Storage | Small, append-only, read by metric name. Shares the storage account with orders. |
| Customer data shared with Wiley & Co | Separate store behind its own service boundary | Two departments sharing one database schema is the coupling this architecture exists to avoid. Shared via API contract, not a shared connection string. |
| Forum reviews | Cosmos DB (NoSQL API) | Schema-flexible documents, very high volume, anonymous (no relational integrity to enforce). Cosmos' analytical store gives marketing a profiling path without touching the transactional workload. |

The principle throughout: **access pattern decides the store**, not familiarity with one
technology.

---

## 3. The order write path

```
POST /api/orders
      │
      ▼
OrdersController ──▶ PlaceOrderCommand ──▶ PlaceOrderCommandHandler
                            (MediatR)              │
                                                   ▼
                                           IOrderQueue (Azure Queue Storage)
                                                   │
                            202 Accepted ◀─────────┘
                                                   │
                                                   ▼ (asynchronous)
                                        ProcessOrderFunction (QueueTrigger)
                                                   │
                                                   ▼
                                           IOrderWriteStore (Table Storage)
```

The API returns **202 Accepted** with an order id, not 201 Created. Nothing has been
created yet — the order has been accepted for processing. This is the honest status code
and it reflects what the architecture actually guarantees.

Consequences, stated plainly:

- **Peak load is absorbed by the queue**, not by the database. The API's throughput
  ceiling becomes "how fast can it enqueue", which is high and roughly constant.
- **The datastore can fail without failing the request.** Messages accumulate in the queue
  and drain when it recovers.
- **The Function scales independently of the API.** Order processing capacity is tuned by
  Function concurrency settings, not by scaling web servers.
- **The cost is eventual consistency.** An order is not immediately readable after the
  POST returns. For this domain that is acceptable: the customer gets a confirmation id
  immediately, and the order appears in their history a moment later.

Queue messages are base64-encoded (`QueueMessageEncoding.Base64`) to match what the
Functions queue trigger expects.

---

## 4. Partition and row key design

This is where throughput is won or lost in Table Storage, so both keys are deliberate.

### Orders

```
PartitionKey = CustomerId
RowKey       = (DateTimeOffset.MaxValue.Ticks - OrderDate.UtcTicks) padded to 19 digits
               + "_" + OrderId
```

**Why CustomerId as the partition key.** Table Storage throttles per partition, so a
partition key with few distinct values creates a hot partition that caps write throughput
regardless of how much the rest of the table is idle. Partitioning by customer spreads
writes across as many partitions as there are active customers — exactly the behaviour
needed during a traffic peak, where many customers each place a few orders.

Rejected alternatives:

- *Date as partition key* — every order on a given day lands in one partition. This is the
  textbook hot-partition anti-pattern, and peak hours are precisely when it would bite.
- *OrderId as partition key* — perfect write distribution, but "show me my orders" becomes
  a full table scan. Reads are frequent enough that this is a bad trade.

**Why inverted ticks as the row key.** Table Storage returns rows in ascending RowKey
order and offers no sort. Subtracting the timestamp from `MaxValue` inverts the ordering
so the newest order sorts first, which makes "my recent orders" a cheap top-N read of a
single partition with no post-processing.

Two details that matter in practice:

- The value is **zero-padded to 19 digits**, because RowKeys compare as strings. Without
  padding, "9" sorts after "10" and the ordering silently breaks.
- The **OrderId is appended** so two orders written on the same tick cannot collide.

### Metrics

```
PartitionKey = metric name  (e.g. "orderprocessed")
RowKey       = inverted ticks of computation time
```

Same inversion for the same reason: "the last N values of this metric" is a single-partition
top-N read.

---

## 5. The orderprocessed metric

The scenario requires order date and ship date to be stored and used to calculate an
*orderprocessed* metric. The metric is **precomputed on a schedule, not calculated on
demand** — a deliberate consequence of the partition key choice.

Averaging (ship date − order date) across all customers is a **cross-partition scan**,
because the data is partitioned by customer. Cross-partition queries in Table Storage have
no index to use and cost time proportional to table size. Serving that synchronously on an
HTTP request would reintroduce exactly the kind of slow path this architecture removed.

`CalculateOrderMetricsFunction` (TimerTrigger) therefore runs the scan on a schedule, writes
a single aggregate row, and any consumer reads that one row in constant time.

The trade-off is staleness bounded by the schedule interval, which for a business metric is
irrelevant.

A note on the timer: its schedule state lives in blob storage rather than in the host
process, so a restarted Function detects missed runs (`IsPastDue`) and catches up. The
Function stays stateless and horizontally scalable.

---

## 6. CQRS and the service layer

Read and write paths share no code and no model.

| | Write | Read |
|---|---|---|
| Entry | `PlaceOrderCommand`, `ShipOrderCommand` | `GetCustomerOrdersQuery` |
| Dispatch | MediatR `ISender` | MediatR `ISender` |
| Storage interface | `IOrderWriteStore` | `IOrderReadStore` |
| Path | Controller → queue → Function → table | Controller → table |
| Response | 202 / 204, asynchronous | Data, synchronous |

The mediator (MediatR) keeps controllers free of business logic: a controller action
constructs a command or query and sends it, nothing more. Handlers depend only on
interfaces declared in the Application layer.

Splitting the store into two interfaces rather than one `IOrderRepository` is what makes
the separation enforceable rather than conventional — a read handler cannot write, because
the type it depends on has no write method.

---

## 7. Layering and dependency direction

```
OnlineStore.Api             ──▶ Application, Infrastructure   (DI wiring only)
OnlineStore.Functions       ──▶ Application, Infrastructure   (DI wiring only)
OnlineStore.Infrastructure  ──▶ Application                   (implements its interfaces)
OnlineStore.Application     ──▶ Domain                        (commands, queries, contracts)
OnlineStore.Domain          ──▶ (nothing)
```

Dependencies point inward. The Application layer declares the interfaces
(`IOrderQueue`, `IOrderWriteStore`, `IOrderReadStore`, `IMetricsStore`); Infrastructure
implements them against Azure SDKs; neither Api nor Functions contains business logic.

Two practical consequences:

- **Both hosts reuse the same core.** The API and the Functions host are two entry points
  over one Application layer — no duplicated logic.
- **Handlers are testable without Azure.** Every dependency is an interface, so unit tests
  mock them and run with no emulator and no network.

---

## 8. Security

- Connection strings come from configuration (`appsettings`, `local.settings.json`), never
  from source. `local.settings.json` is gitignored.
- In Azure these move to Application Settings, and in production to Key Vault with managed
  identity, removing stored credentials entirely.
- Reviews are anonymous by design: no user identifier is written alongside review content,
  which keeps the marketing analytics path free of personal data.

---

## 9. Local development

The whole system runs against **Azurite**, the Azure Storage emulator, via the connection
string `UseDevelopmentStorage=true`. Queues, tables and blobs all work offline, so the PoC
is developed and demonstrated without an Azure subscription.

```
Terminal 1   azurite
Terminal 2   dotnet run   (OnlineStore.Api)
Terminal 3   func start   (OnlineStore.Functions)
```

---

## 10. Known limitations

Stated deliberately rather than hidden:

- `FindAsync` locates an order by filtering on `OrderId` within a customer's partition,
  which is a partition scan rather than a point lookup. It is cheap because a customer has
  few orders; the cleaner design passes the RowKey to the caller.
- The metric scan reads all shipped orders in the window on every run. At real volume this
  would be replaced by an incremental aggregate or Azure Data Explorer.
- There is no dead-letter handling beyond the queue's built-in poison-message behaviour.
- The PoC has no frontend and no authentication, both out of scope per the assignment.
