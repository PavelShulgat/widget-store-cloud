# Widget & Co — Cloud Database PoC

Proof of concept for the Widget & Co online store, built for the Cloud Databases
assignment (Inholland, Cloud Computing minor). C# Web API plus two Azure Functions,
no frontend.

The design and the reasoning behind it are in [`docs/architecture.md`](docs/architecture.md).

## Azure services

| Service | Used for |
|---|---|
| Queue Storage | Order write path — the API enqueues and returns 202 |
| Table Storage | Persisted orders and computed metrics |
| Blob Storage | Product images |
| SQL (EF Core) | Product catalogue |
| Azure Functions | Queue-triggered order processing, timer-triggered metrics |

Cosmos DB for the review forum is designed but not implemented — see §11 of the
architecture document.

## Projects

```
OnlineStore.Api             Web API, controllers, DI wiring
OnlineStore.Application     Commands, queries, handlers, interfaces
OnlineStore.Domain          Entities
OnlineStore.Infrastructure  Azure SDK and EF Core implementations
OnlineStore.Functions       ProcessOrderFunction, CalculateOrderMetricsFunction
OnlineStore.Tests           Unit tests (no emulator required)
```

Dependencies point inward: Application declares the interfaces, Infrastructure
implements them.

## Running locally

Requires .NET 8 SDK, Azure Functions Core Tools v4, and Azurite.

```bash
cd src/OnlineStore
dotnet build
dotnet ef database update --project OnlineStore.Infrastructure --startup-project OnlineStore.Api
```

Then three terminals:

```bash
azurite                                  # storage emulator
cd OnlineStore.Api       && dotnet run   # API on http://localhost:5296
cd OnlineStore.Functions && func start   # Functions host
```

Swagger is at `http://localhost:5296/swagger`.

## Trying it out

```bash
# create a product
curl -X POST http://localhost:5296/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Widget Pro","description":"The flagship widget","price":49.99,"stockQuantity":100}'

# upload an image to it
curl -X POST "http://localhost:5296/api/products/<productId>/images" -F "file=@image.jpg"

# place an order (returns 202; the Function persists it)
curl -X POST http://localhost:5296/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","lines":[{"productId":"<productId>","quantity":2,"unitPrice":49.99}]}'

# mark it shipped, then wait for the metrics timer
curl -X PATCH "http://localhost:5296/api/orders/<orderId>/ship?customerId=11111111-1111-1111-1111-111111111111"
```

## Tests

```bash
cd src/OnlineStore
dotnet test
```

The tests mock every storage interface, so they run with no emulator, no network and no
database.

## Configuration

Connection strings live in `appsettings.Development.json` (API) and `local.settings.json`
(Functions). The latter is gitignored. Both point at Azurite and a local SQLite file by
default.
