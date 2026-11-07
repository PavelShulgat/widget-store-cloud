# Cloud Database Architecture — Widget & Co

Proof of concept for migrating the Widget & Co online store from ISP hosting to Azure.
No frontend; the deliverable is the data architecture and the service layer around it.

**Implementation status.** Queue Storage, Table Storage, Blob Storage and SQL (EF Core)
are implemented and demonstrable. Cosmos DB for the review forum is designed but not
implemented — see §11.

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

| Data | Service | Status | Reasoning |
|---|---|---|---|
| Product specifications | SQL via EF Core | Implemented | Structured, relational, low write volume, benefits from constraints and indexing. The scenario states it is already in SQL; there is no reason to move it. |
| Product images | Blob Storage | Implemented | Binary objects of unbounded size. Storing them in SQL inflates row size and backup time for no benefit. Only the blob URL is kept in the product row. |
| Incoming orders (write) | Queue Storage | Implemented | Decouples accepting an order from persisting it. See §3. |
| Orders (persisted) | Table Storage | Implemented | High write volume, always accessed by customer, no joins required. Cheaper per transaction than SQL and scales horizontally by partition. |
| Computed metrics | Table Storage | Implemented | Small, append-only, read by metric name. Shares the storage account with orders. |
| Customer data shared with Wiley & Co | Separate store behind its own service boundary | Designed | Two departments sharing one database schema is the coupling this architecture exists to avoid. Shared via API contract, not a shared connection string. |
| Forum reviews | Cosmos DB (NoSQL API) | Designed, not implemented | Schema-flexible documents, very high volume, anonymous. Cosmos' analytical store gives marketing a profiling path without touching the transactional workload. |

The principle throughout: **access pattern decides the store**, not familiarity with one
technology.

### Local provider substitution

The PoC runs against **Azurite** (storage emulator) and **SQLite** (relational), so the
whole system works offline with no Azure subscription. The EF Core code is provider-
agnostic: moving to Azure SQL is a single line in `DependencyInjection.cs`
(`UseSqlite` → `UseSqlServer`), which is the point of the EF abstraction and the reason
the repository interface exposes no provider-specific types.

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

### Contrast: the product write path

`POST /api/products` returns **201 Created**, synchronously, because the product genuinely
exists in SQL by the time the response is written. The catalogue has low write volume and
no peak-load problem, so queueing it would add latency and complexity for nothing.

The two status codes are not a style choice — they describe two genuinely different
guarantees, and the difference is the architecture doing its job.

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

Same inversion for the same reason: "the last N values of this metric" is a
single-partition top-N read.

### Blob naming

```
product-images/{ProductId}/{random32hex}{extension}
```

Blob Storage has a flat namespace, but a `/` prefix behaves as a virtual folder for
listing. Prefixing by product id makes "all images for this product" a cheap prefix query;
the random suffix avoids collisions and stops a re-upload silently overwriting an existing
image.

---

## 5. The orderprocessed metric

The scenario requires order date and ship date to be stored and used to calculate an
*orderprocessed* metric. The metric is **precomputed on a schedule, not calculated on
demand** — a deliberate consequence of the partition key choice.

Averaging (ship date − order date) across all customers is a **cross-partition scan**,
because the data is partitioned by customer. Cross-partition queries in Table Storage have
no index to use and cost time proportional to table size. Serving that synchronously on an
HTTP request would reintroduce exactly the kind of slow path this architecture removed.

`CalculateOrderMetricsFunction` (TimerTrigger) therefore runs the scan on a schedule,
writes a single aggregate row, and any consumer reads that one row in constant time.

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
| Entry | `PlaceOrderCommand`, `ShipOrderCommand`, `CreateProductCommand` | `GetCustomerOrdersQuery`, `GetProductsQuery` |
| Dispatch | MediatR `ISender` | MediatR `ISender` |
| Storage interface | `IOrderWriteStore` | `IOrderReadStore` |
| Path (orders) | Controller → queue → Function → table | Controller → table |
| Response | 202 / 201 / 204 | Data, synchronous |

The mediator (MediatR) keeps controllers free of business logic: a controller action
constructs a command or query and sends it, nothing more. Handlers depend only on
interfaces declared in the Application layer.

Splitting the store into two interfaces rather than one `IOrderRepository` is what makes
the separation enforceable rather than conventional — a read handler cannot write, because
the type it depends on has no write method.

Read queries use `AsNoTracking()` on the EF Core side: catalogue reads never mutate
entities, so EF's change tracker is pure overhead for them.

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
(`IOrderQueue`, `IOrderWriteStore`, `IOrderReadStore`, `IMetricsStore`, `IImageStore`,
`IProductRepository`); Infrastructure implements them against Azure SDKs and EF Core;
neither Api nor Functions contains business logic.

Two practical consequences:

- **Both hosts reuse the same core.** The API and the Functions host are two entry points
  over one Application layer — no duplicated logic.
- **Handlers are testable without Azure.** Every dependency is an interface, so the unit
  test suite mocks them with Moq and runs with **no emulator, no network and no database**.
  That the tests need no infrastructure at all is the concrete evidence that the coupling
  is genuinely loose rather than nominally so.

---

## 8. Security

- Connection strings are read from configuration — `appsettings.Development.json` for the
  API, `local.settings.json` for the Functions host — and never appear in source.
- `local.settings.json` is gitignored, so the file that would hold real credentials outside
  the emulator is never committed.
- In Azure these move to Application Settings; in production to Key Vault with a managed
  identity, which removes stored credentials entirely.
- Reviews are anonymous by design: no user identifier is written alongside review content,
  which keeps the marketing analytics path free of personal data.

### Blob access

The `product-images` container is configured with `PublicAccessType.Blob`: individual
blobs are readable by URL, but the container cannot be listed, so URLs cannot be
enumerated. This is convenient for a PoC and lets a CDN serve images without the API in
the request path.

Production would differ. Many Azure accounts disable anonymous blob access at the account
level, and the correct pattern is a private container with short-lived SAS URLs generated
per request. That trades a little latency for revocable, time-bounded access.

---

## 9. Local development

```
Terminal 1   azurite                        # queues, tables, blobs
Terminal 2   dotnet run   (OnlineStore.Api)
Terminal 3   func start   (OnlineStore.Functions)
```

SQLite needs no process — the database is a file created by `dotnet ef database update`.

End-to-end demonstration:

1. `POST /api/products` → 201 with a product id
2. `POST /api/products/{id}/images` → 201, image in Blob Storage, URL written to SQL
3. `GET /api/products` → product row now carries the blob URL
4. `POST /api/orders` → 202, message on the queue
5. `ProcessOrderFunction` drains the queue → order row in Table Storage
6. `PATCH /api/orders/{id}/ship` → 204, ShipDate set
7. `CalculateOrderMetricsFunction` fires on schedule → `orderprocessed` written to metrics

---

## 10. Known limitations

Stated deliberately rather than hidden:

- `FindAsync` locates an order by filtering on `OrderId` within a customer's partition,
  which is a partition scan rather than a point lookup. It is cheap because a customer has
  few orders; the cleaner design passes the RowKey to the caller.
- The metric scan reads all shipped orders in the window on every run. At real volume this
  would be replaced by an incremental aggregate or Azure Data Explorer.
- `GetShippedSinceAsync` uses a raw OData filter string rather than a LINQ expression,
  because the LINQ translation of a nullable `DateTimeOffset` comparison proved unreliable
  against the emulator.
- Image upload and the SQL metadata update are two separate operations with no transaction
  across them. A failure between the two leaves an orphaned blob. Production would
  reconcile with a cleanup job, since no distributed transaction spans Blob and SQL.
- There is no dead-letter handling beyond the queue's built-in poison-message behaviour.
- The PoC has no frontend and no authentication, both out of scope per the assignment.

---

