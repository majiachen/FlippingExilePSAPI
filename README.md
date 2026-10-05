# PoEValuation

ASP.NET Core (net10.0) API that aggregates Path of Exile market data (PoeTrade OAuth + the public
currency-exchange endpoint), caches digests in Redis, and persists hourly currency-exchange
snapshots in PostgreSQL.

## Prerequisites

- .NET 10 SDK
- PostgreSQL 14+ (dev instance: `192.168.0.10:5432`)
- Redis 7+ (optional at startup; the cache layer degrades gracefully)

## Database setup (once per instance)

```bash
psql -h 192.168.0.10 -U postgres -d postgres -c "CREATE DATABASE poevaluation;"
psql -h 192.168.0.10 -U postgres -d poevaluation -f db/schema.sql
```

`db/schema.sql` is idempotent; the application does not create the schema at startup.

## Connection strings (secrets)

`Sql:ConnectionString` is deliberately absent from `appsettings*.json`. In Development, provide it
via a user secret (or an environment variable):

```bash
dotnet user-secrets set "Sql:ConnectionString" \
  "Host=192.168.0.10;Port=5432;Database=poevaluation;Username=postgres;Password=postgres" \
  --project src/PoE.Valuation.Api/PoE.Valuation.Api.csproj
```

For non-Development environments, set the environment variable `Sql__ConnectionString` (the double
underscore is the configuration-section separator). `OAuth:ClientSecret` and `Redis:ConnectionString`
follow the same pattern (`OAuth__ClientSecret`, `Redis__ConnectionString`); the
`appsettings.{Environment}.json` files provide non-secret development defaults. Registered values are
redacted from all log output via `SensitiveValueStore`/`LogSanitizer`.

## Running

```bash
dotnet run --project src/PoE.Valuation.Api/PoE.Valuation.Api.csproj
```

## Deployment (Docker / UnRAID)

Publish the API and the frontend behind **one HTTPS origin** (SWAG / Nginx Proxy Manager / Traefik):
the API sets no CORS and its session cookie is `Secure`, so plain `http://` hosting silently breaks
login and every `/api/*` call answers `401`. Kestrel runs as `http://+:8080` inside the docker network
and the proxy terminates TLS.

The container exits at startup unless these are set: `OAuth__ClientId`, `OAuth__ClientSecret`,
`OAuth__RedirectUri` (the **published** `https://<host>/auth/callback`, registered character-for-character
in the GGG developer portal), `Redis__ConnectionString` and `Sql__ConnectionString`. Apply
`db/schema.sql` to the Postgres container once. Run a single API replica.

Full topology, compose example, proxy rules and volumes: **`docs/FRONTEND_API.md`, section 12**.

## Authentication

Login is the PKCE flow: `GET /auth/login` redirects to PoeTrade, which redirects back to the
**callback route `GET /auth/callback`**; that route issues the `poe_sid` cookie and the Redis session.

`OAuth:RedirectUri` must therefore be the callback route itself, and it must be registered
character-for-character in the GGG OAuth client — a mismatch makes PoeTrade refuse the authorize
request before the app is ever reached. With the `https` launch profile (`https://localhost:7003`)
that means:

```bash
dotnet user-secrets set "OAuth:RedirectUri" "https://localhost:7003/auth/callback" \
  --project src/PoE.Valuation.Api/PoE.Valuation.Api.csproj
```

## Tests

- `dotnet test tests/PoE.Valuation.UnitTests` — no external services required.
- `dotnet test tests/PoE.Valuation.IntegrationTests` — no external services required (connections
  are lazy and the poller is disabled in tests).

## Item name → metadata path

The currency-exchange snapshots are keyed by PoE **metadata paths**
(`Metadata/Items/Currency/CurrencyRerollRare`), but the stash API only ever publishes an item's
`name` and `base_type`. The `item_metadata` table is the map between the two.

It is refreshed wholesale from the public PoE trade data catalogue
(`PathOfExile:TradeDataBaseUrl` + `static`) by `ItemMetadataCatalogRefresher`, which holds the Redis
lock `lock:item-catalog` so only one instance writes it. The catalogue publishes no metadata field;
the path is inside each entry's icon URL as a base64url-encoded JSON array whose `f` member is the
game asset path (`2DItems/Currency/CurrencyRerollRare`), and the asset root `2DItems` is the same
tree as the metadata root `Metadata/Items`.

Lookups compare **normalized** names (`ItemNameNormalizer.Normalize`: lower-cased, apostrophes and
periods removed, every other separator collapsed to a single space), so `Jeweller's Orb`,
`jewellers orb` and `Jewellers  Orb` all resolve to the same item.

## API surface

Frontend-facing contract (request/response shapes, error contract, rate limits, TypeScript client):
**[`docs/FRONTEND_API.md`](docs/FRONTEND_API.md)**.

| Route | Purpose |
| --- | --- |
| `GET /api/valuation/{itemId}?reference=&league=` | Latest hourly valuation. `{itemId}` is a metadata path — URL-encode it (`Metadata%2FItems%2F…`); the API decodes the segment. |
| `GET /api/history/{itemId}?reference=&league=&range=24h\|7d\|30d` | Hourly price history. |
| `GET /api/items/lookup?q=&league=&limit=` | Item name (or trade alias, or metadata path) → ranked metadata paths, each with the reference currencies that actually have market data. |
| `GET /api/valuation/by-name/{itemName}?reference=&league=` | Valuation from a stash-style item name; `reference` may be a name or a metadata path. |
| `POST /api/items/catalog/refresh` | Forces a catalogue refresh (returns rows written). |
| `GET /api/stash?league=` | The account stash for a league. |

All `/api/*` routes require the `poe_sid` session cookie and are rate-limited per session.

## Frontend contract notes

- Metadata paths contain `/`, which is a route separator: always URL-encode them in a path segment
  (`Metadata%2FItems%2FCurrency%2FCurrencyRerollRare`). In a query string they may be sent raw.
- A valuation needs **both** an item and a reference currency that appear in the same market pair.
  `GET /api/items/lookup?league=Standard` returns the reference currencies that have data, so a
  frontend never has to guess.
- The catalogue covers the current game version only. Retired items (for example
  `Metadata/Items/Currency/CurrencyDeepwater`) have snapshots in the DB but no catalogue entry, so
  they resolve only when the metadata path is supplied directly.
- `lowestRate`/`highestRate` are always **units of the reference currency per 1 unit of the item**,
  ordered ascending (`lowestRate <= highestRate`). The published `lowest_ratio`/`highest_ratio`
  dictionaries describe the pair in whichever orientation the league produced
  (`{ divine: 1, chaos: 610 }` and `{ divine: 1, chaos: 725 }`), so the labels alone are not an
  ordering; `ValuationRate.ReferencePerItemRange` converts both extremes and sorts them.
- A valuation with no snapshot for that item/reference/league combination answers `404`, including
  when `reference` is omitted: the market is keyed by both sides, so there is nothing to pick.
  Query `GET /api/items/lookup?league=` for the references that do have data.

