# PoEValuation — Frontend API Reference

Everything a frontend needs to build against this service: auth, every endpoint, exact request and
response shapes, error contract, rate limits and the integration gotchas.

Field names below are the **wire names** (ASP.NET camelCase serialisation of the DTOs in
`src/PoE.Valuation.Contracts`). Examples are shape-exact; the numbers are representative of the dev
database (chaos ↔ divine ≈ 610–725 chaos per divine).

---

## 1. Base URL and origin rules

| Environment | Base URL |
| --- | --- |
| Dev (`https` launch profile) | `https://localhost:7003` (also `http://localhost:5024`) |
| Dev (`http` launch profile) | `http://localhost:5024` |
| Self-hosted (Docker/UnRAID behind a TLS proxy) | your published host, e.g. `https://poe.lan.example.com` |

Containers, proxy rules, env vars and volumes are covered in **§12**. Everything else in this document
applies unchanged in both environments.

**There is no CORS configuration in the API.** `Program.cs` never calls `AddCors`/`UseCors`, and the
session cookie is `SameSite=Strict; Secure`. So the frontend must reach the API from the **same
origin** — either served by the API itself or through a dev/production proxy:

```ts
// Vite (vite.config.ts)
export default defineConfig({
  server: {
    proxy: {
      "/api":    { target: "https://localhost:7003", changeOrigin: true },
      "/auth":   { target: "https://localhost:7003", changeOrigin: true },
      "/health": { target: "https://localhost:7003", changeOrigin: true },
    },
  },
});
```

```jsonc
// Next.js (next.config.js)
async rewrites() { return [{ source: "/api/:path*", destination: "https://localhost:7003/api/:path*" }]; }
```

`Secure` cookies are never stored or sent over plain `http`, so anything that has to log in must be
reached over HTTPS — locally `https://localhost:7003`, in Docker a proxy that terminates TLS (§12.3).
Serving the API as `http://<unraid-ip>:7003` looks healthy but every `/api/*` call answers `401`,
because the browser silently drops `poe_sid`.

---

## 2. Authentication

Login is a **server-driven PKCE redirect**, not something the frontend performs with fetch.

| Route | Method | Auth | Behaviour |
| --- | --- | --- | --- |
| `/auth/login` | GET | no | `302` to `https://www.pathofexile.com/oauth/authorize?...` (PKCE; `state` kept in Redis for 10 min) |
| `/auth/callback` | GET | no | PoeTrade redirects back here. Success: sets `poe_sid`, `302` → `/`. Failure: `400` problem |
| `/auth/logout` | POST | cookie | Deletes the Redis session, clears the cookie, `204` empty |

Scopes requested: `service:psapi service:leagues service:cxapi`.

### Session cookie

| Property | Value |
| --- | --- |
| Name | `poe_sid` |
| Value | opaque session id (a GUID), **not** the PoE access token |
| `HttpOnly` | `true` — the frontend can never read it |
| `SameSite` | `Strict` |
| `Secure` | `true` |
| `Path` | `/` |
| `MaxAge` | 8 hours |
| Server-side session TTL | 90 days (Redis `session:{id}`) |

The API refreshes the PoE access token transparently when it is expired or within one minute of
expiring, so a long-lived session does not need a re-login.

### How the frontend logs in

Start login with a **full-page navigation** (never `fetch` — it is a cross-origin 302):

```html
<a href="/auth/login">Log in with Path of Exile</a>
```
```ts
window.location.assign("/auth/login");
```

After the callback the browser lands on `/` with `poe_sid` set. Every `/api/*` call then just needs
`credentials: "include"`.

### How the frontend detects "not logged in"

`401` with an **empty body** is the only signal (missing cookie *or* unknown/expired session — they
are indistinguishable by design). Treat any `401` as "re-login required":

```ts
if (res.status === 401) window.location.assign("/auth/login");
```

---

## 3. Conventions that apply to every endpoint

- **JSON casing:** camelCase; `Content-Type: application/json`.
- **Item ids are PoE metadata paths** — `Metadata/Items/Currency/CurrencyRerollRare`. They contain
  `/`, which is a route separator, so **always URL-encode them in a path segment**
  (`Metadata%2FItems%2FCurrency%2FCurrencyRerollRare`). The API decodes the segment for you. In a
  query string (`reference=…`) they may be sent raw.
- **League is a name, not a number:** `Standard`, `Solo Self-Found`, `Hardcore SSF`,
  `Limey Whelps (PL86569)`. Allowed characters: letters, digits, space, `- _ . ( ) '`; max 64 chars.
- **Item names** (lookup / by-name): max 120 chars, no control characters, no `:`. Punctuation is
  fine — names are compared **normalized** (lower-cased, apostrophes and periods removed, every other
  separator collapsed to one space), so `Jeweller's Orb`, `jewellers orb` and `Jewellers  Orb` are
  the same item.
- **Rates are always "units of the reference per 1 unit of the item"**, and
  `lowestRate <= highestRate` is guaranteed by the API.
- **Nullable numbers:** `lowestRate`, `highestRate`, `volumeItem`, `volumeReference` can be `null`
  when the upstream snapshot did not publish them. Render a placeholder; do not assume a number.
- **Timestamps:** ISO-8601 with offset, always UTC (`"2026-10-05T09:00:00+00:00"`).
- **Auth:** every `/api/*` route requires `poe_sid`; otherwise `401` (empty body).
- **Rate limiting:** per session **and per endpoint**, fixed window —
  **30 requests per 300 s** (`RateLimits:DefaultMaxRequests` / `DefaultWindowSeconds`). Exceeding it
  returns `429` with an **empty body and no `Retry-After` header**. Each endpoint has its own bucket
  (`valuation`, `history`, `items-lookup`, `valuation-by-name`, `items-catalog-refresh`, `stash`), so
  hammering one endpoint does not exhaust the others.
- **Data freshness:** snapshots are hourly; the item catalogue refreshes every 3600 s (stale after
  86 400 s); the stash response is cached per session for 15 min; `GET /api/valuation/{itemId}` only
  returns a snapshot from the **last 24 hours**.

### Error contract

| Status | Body | Meaning |
| --- | --- | --- |
| `400` | `application/problem+json` — `{"title":"Invalid input","detail":"<why>"}` | bad item id / league / name / range |
| `401` | empty | no or invalid session |
| `404` | empty | no snapshot for that item + reference + league |
| `404` | `{"title":"No catalogue match","detail":"No item in the PoE item catalogue matches '…'. Try GET /api/items/lookup?q=…"}` | a name resolved to no catalogue entry |
| `429` | empty | per-session, per-endpoint rate limit hit |
| `499` | empty | client aborted the request (non-standard status the API sends on cancellation) |
| `502` | `{"title":"Upstream unavailable","status":502}` | upstream PoE API failed (league list, stash) |
| `503` | `{"title":"Cache unavailable","status":503}` | Redis unavailable |
| `503` | `{"title":"Service unavailable","status":503,"detail":"The database is temporarily unavailable. Please retry."}` | Postgres unavailable |
| `400` | `{"title":"Authentication failed","detail":"The authorization callback was invalid or has expired."}` | bad/expired OAuth callback (`/auth/callback`) |

`502`/`503` are **retryable**; `400`/`401`/`404` are not.

---

## 4. Endpoints

### 4.1 `GET /health`

No auth. Use it for a readiness probe / "API reachable" banner.

```json
{ "status": "Healthy", "timestampUtc": "2026-10-05T10:12:03+00:00" }
```

---

### 4.2 `GET /api/items/lookup` — the endpoint a search box calls

Resolves a human name (or a trade alias, or a metadata path) to ranked catalogue entries **and** to
the reference currencies that actually have market data. This is the endpoint that removes all
guessing about which `reference` to send to the valuation endpoints.

**Query parameters**

| Param | Required | Notes |
| --- | --- | --- |
| `q` | yes | item name, trade alias (`chaos`) or metadata path. Max 120 chars |
| `league` | no | when omitted, `markets` is always `[]` (there is nothing to scope the snapshot query to) |
| `limit` | no | 1–50, default **10** (out-of-range values are clamped, not rejected) |

**Response `200`**

```json
{
  "query": "chaos orb",
  "catalogUpdatedAtUtc": "2026-10-05T08:00:00+00:00",
  "count": 1,
  "matches": [
    {
      "itemId": "Metadata/Items/Currency/CurrencyRerollRare",
      "displayName": "Chaos Orb",
      "category": "Currency",
      "tradeAlias": "chaos",
      "matchKind": "Exact",
      "markets": [
        {
          "reference": "Metadata/Items/Currency/CurrencyModValues",
          "referenceDisplayName": "Divine Orb",
          "snapshotHourUtc": "2026-10-05T09:00:00+00:00",
          "lowestRate": 0.001379310344827586,
          "highestRate": 0.001639344262295082,
          "volumeItem": 40,
          "volumeReference": 50
        }
      ]
    }
  ]
}
```

| Field | Type | Notes |
| --- | --- | --- |
| `query` | string | the query exactly as supplied |
| `catalogUpdatedAtUtc` | string \| null | `null` when the catalogue has never been refreshed → no matches are then possible |
| `count` | int | `matches.length` |
| `matches[].itemId` | string | pass this to `/api/valuation/{itemId}` or `/api/history/{itemId}` |
| `matches[].displayName` | string | item name as PoE publishes it |
| `matches[].category` | string | catalogue category id (`Currency`, `Maps`, `QuestItems`, `Divination`, …) |
| `matches[].tradeAlias` | string \| null | trade-site alias (`chaos`, `divine`), when known |
| `matches[].matchKind` | string | `Exact` \| `Alias` \| `Prefix` \| `Contains` — how confidently it resolved |
| `matches[].markets` | array | up to **20** reference currencies that have data; `[]` when no `league` was given |
| `markets[].reference` | string | send this as `reference` to the valuation endpoints |
| `markets[].referenceDisplayName` | string | reference name, or its metadata path when the catalogue does not name it |

**Ranking is deterministic:** strongest match kind first, then the shortest normalized name (a more
specific name beats a longer one that merely contains the query), then the metadata path. The same
query and catalogue always produce the same order, so it is safe to auto-pick `matches[0]` and offer
the rest as alternatives.

**Status codes:** `200` (including `count: 0` — an empty match list is **not** a 404), `400` (bad `q`
or `league`), `401`, `429`, `503`.

---

### 4.3 `GET /api/valuation/by-name/{itemName}` — valuation from a stash-style name

One call: name → metadata path → latest valuation. `{itemName}` may be a display name, a base type, a
trade alias or an already-encoded metadata path.

**Parameters**

| Param | Location | Required | Notes |
| --- | --- | --- | --- |
| `itemName` | path | yes | URL-encode it (`Chaos%20Orb`, `Metadata%2FItems%2F…`) |
| `reference` | query | no | a name **or** a metadata path. Omit it and there is no market to pick → `404` |
| `league` | query | no | omit it and there is no league to scope to → `404` |

> Note: here `reference` is validated as an **item name** (max 120 chars), while
> `/api/valuation/{itemId}` validates it as an **item id** (max 500 chars). Very long metadata paths
> are therefore safer on `/api/valuation/{itemId}`.

**Response `200`**

```json
{
  "item": {
    "input": "Chaos Orb",
    "itemId": "Metadata/Items/Currency/CurrencyRerollRare",
    "displayName": "Chaos Orb",
    "matchKind": "Exact",
    "alternatives": []
  },
  "reference": {
    "input": "Divine Orb",
    "itemId": "Metadata/Items/Currency/CurrencyModValues",
    "displayName": "Divine Orb",
    "matchKind": "Exact",
    "alternatives": []
  },
  "league": "Standard",
  "marketId": "Metadata/Items/Currency/CurrencyRerollRare|Metadata/Items/Currency/CurrencyModValues",
  "snapshotHourUtc": "2026-10-05T09:00:00+00:00",
  "lowestRate": 0.001379310344827586,
  "highestRate": 0.001639344262295082,
  "volumeItem": 40,
  "volumeReference": 50
}
```

`item.alternatives` / `reference.alternatives` are the **other** metadata paths that matched the same
input, strongest first. Empty when the resolution was unambiguous. When they are non-empty the UI
should offer them ("did you mean …?") because only the first match was valued.

**Status codes:** `200`; `400` (bad name/league); `404` problem `No catalogue match` (the name, or the
reference, matched nothing); `404` empty (resolved fine, but no snapshot for that pair + league);
`401`; `429`; `503`.

### 4.4 `GET /api/valuation/{itemId}` — latest valuation from metadata paths

**Parameters**

| Param | Location | Required | Notes |
| --- | --- | --- | --- |
| `itemId` | path | yes | metadata path, URL-encoded |
| `reference` | query | no | metadata path of the reference currency. Omit → `404` |
| `league` | query | no | Omit → `404` |

A market is keyed by **both** sides plus the league, so a valuation with no reference (or no league)
has nothing to pick and answers `404` — query `GET /api/items/lookup?league=…` for the references that
do have data.

**Response `200`**

```json
{
  "itemId": "Metadata/Items/Currency/CurrencyRerollRare",
  "referenceItemId": "Metadata/Items/Currency/CurrencyModValues",
  "league": "Standard",
  "marketId": "Metadata/Items/Currency/CurrencyRerollRare|Metadata/Items/Currency/CurrencyModValues",
  "snapshotHourUtc": "2026-10-05T09:00:00+00:00",
  "lowestRate": 0.001379310344827586,
  "highestRate": 0.001639344262295082,
  "volumeItem": 40,
  "volumeReference": 50
}
```

Only snapshots from the **last 24 hours** are considered. `marketId` is the raw compound key
(`itemA|itemB`) and may be the reverse of the pair you asked for — the API searches both market
directions. Useful for debugging, not for display.

**Status codes:** `200`, `400`, `404` (empty), `401`, `429`, `503`.

---

### 4.5 `GET /api/history/{itemId}` — hourly price series

**Parameters**

| Param | Location | Required | Notes |
| --- | --- | --- | --- |
| `itemId` | path | yes | metadata path, URL-encoded |
| `range` | query | **yes** | `24h`, `7d` or `30d` (case-insensitive). Anything else → `400` |
| `reference` | query | no | omit → empty series |
| `league` | query | no | omit → empty series |

**Response `200`**

```json
{
  "itemId": "Metadata/Items/Currency/CurrencyRerollRare",
  "referenceItemId": "Metadata/Items/Currency/CurrencyModValues",
  "league": "Standard",
  "range": "24h",
  "fromUtc": "2026-10-04T10:00:00+00:00",
  "count": 2,
  "points": [
    { "snapshotHourUtc": "2026-10-05T09:00:00+00:00", "lowestRate": 0.001379310344827586, "highestRate": 0.001639344262295082, "volumeItem": 40, "volumeReference": 50 },
    { "snapshotHourUtc": "2026-10-05T08:00:00+00:00", "lowestRate": 0.0014, "highestRate": 0.0016, "volumeItem": 31, "volumeReference": 44 }
  ],
  "cached": false
}
```

- `points` are **newest first** — reverse them for a left-to-right chart.
- The window is capped (`24h` → 48 points, `7d` → 168, `30d` → 720), so a chart never over-fetches.
- **No data is not an error:** history answers `200` with `count: 0` and `points: []` (unlike
  `/api/valuation/{itemId}`, which answers `404`). Handle both.
- `cached` is currently always `false` (the series is read from Postgres); treat it as informational.

**Status codes:** `200`, `400` (bad `range`/`itemId`/`league`), `401`, `429`, `503`.

---

### 4.6 `GET /api/stash?league=` — the logged-in account's stash

Returns every item in the account's stash tabs for one league. This is where a frontend gets the
`name` / `baseType` pairs it can feed to `/api/valuation/by-name/{itemName}`.

| Param | Required | Notes |
| --- | --- | --- |
| `league` | **yes** | missing → `400` `{"detail":"League is required."}`; unknown league → `400` `{"detail":"League 'X' was not found."}` |

**Response `200`**

```json
{
  "league": "Standard",
  "leagueId": "Standard",
  "lastRefreshedAt": "2026-10-05T10:05:00+00:00",
  "count": 2,
  "items": [
    {
      "itemId": "s3:3QvpMk,2171900",
      "league": "Standard",
      "name": "Chaos Orb",
      "baseType": "Chaos Orb",
      "frameType": 0,
      "itemLevel": 0,
      "stackSize": 18,
      "gemLevel": null,
      "gemQuality": null,
      "corrupted": false,
      "verified": true,
      "note": "for sale"
    }
  ],
  "cached": true
}
```

- `leagueId` is the PoE league id the data actually came from — for most leagues it equals the name,
  for event leagues it is the name itself (`Limey Whelps (PL86569)`).
- `cached: true` means the response came from the per-session Redis cache (**15-minute TTL**) and was
  for the same league; `false` means it was fetched from PoE just now.
- `itemId` here is a **PoE stash item id**, not a metadata path — it is an identity for the stash item
  and is *not* what the valuation endpoints take. Use `name` / `baseType` for valuation.
- `frameType` is PoE's frame code (0 normal, 1 magic, 2 rare, 3 unique, …) — handy for colouring rows.
- `stackSize` / `gemLevel` / `gemQuality` / `note` are nullable.

**Status codes:** `200`, `400`, `401`, `429`, `502` (PoE API failure), `503`.

---

### 4.7 `POST /api/items/catalog/refresh`

Forces a full catalogue refresh (normally the background poller does it every hour). No body, no
parameters.

```json
{ "refreshedCount": 640, "totalCount": 640, "catalogUpdatedAtUtc": "2026-10-05T10:07:00+00:00" }
```

`refreshedCount: 0` means another instance held the refresh lock (`lock:item-catalog`, 10-minute TTL)
— it is **not** an error. Only call this from an admin/debug screen: it is rate-limited like
everything else and it rewrites the whole catalogue table.

**Status codes:** `200`, `401`, `429`, `502`, `503`.


---

## 5. Quick reference

| Route | Method | Auth | Rate-limit bucket | Success body |
| --- | --- | --- | --- | --- |
| `/health` | GET | no | — | `{ status, timestampUtc }` |
| `/auth/login` | GET | no | — | `302` to PoeTrade (navigate, don't fetch) |
| `/auth/callback` | GET | no | — | `302` to `/` + `poe_sid` cookie |
| `/auth/logout` | POST | cookie | — | `204` empty |
| `/api/items/lookup?q=&league=&limit=` | GET | cookie | `items-lookup` | `ItemLookupResponse` |
| `/api/valuation/by-name/{itemName}?reference=&league=` | GET | cookie | `valuation-by-name` | `ItemValuationResponse` |
| `/api/valuation/{itemId}?reference=&league=` | GET | cookie | `valuation` | `ValuationResponse` |
| `/api/history/{itemId}?reference=&league=&range=` | GET | cookie | `history` | `HistoryResponse` |
| `/api/stash?league=` | GET | cookie | `stash` | `StashResponse` |
| `/api/items/catalog/refresh` | POST | cookie | `items-catalog-refresh` | `ItemCatalogRefreshResponse` |

---

## 6. Recommended frontend flows

### 6.1 Item search → price (the main screen)

```
1. GET /api/items/lookup?q=<typed text>&league=<selected league>&limit=10
   ├─ count === 0            → "no such item" (200, not 404)
   ├─ matches[0]             → the item to show
   └─ matches[1..]           → "other results" chips
2. markets[] of the chosen match → the reference-currency dropdown.
   Each entry already carries its latest rate, so the dropdown can show prices with no extra call.
3. On selection: GET /api/valuation/{itemId}?reference=<chosen reference>&league=<league>
   → big number: lowestRate … highestRate (units of reference per 1 item)
4. Chart: GET /api/history/{itemId}?reference=…&league=…&range=24h|7d|30d → reverse points → plot.
```

Do **not** hard-code a reference currency. `markets[]` is the list of references that actually have
data for that item and league; anything else is a guaranteed `404`.

### 6.2 Stash screen

```
1. GET /api/stash?league=<league>
2. Render items (name, baseType, frameType, stackSize, note).
3. Per row, price it with GET /api/valuation/by-name/{encodeURIComponent(name)}?reference=<reference>&league=<league>
   — or resolve once with /api/items/lookup and then use /api/valuation/{itemId} for the rest.
4. If `item.alternatives` (or `reference.alternatives`) is non-empty, show a "did you mean" affordance.
```

`GET /api/stash` is cached for 15 minutes per session, so a manual "refresh" button is worth having
(and expect `cached: true` on most clicks).

### 6.3 League selector

League names are free text validated by the API (`letters, digits, space - _ . ( ) '`, ≤ 64 chars).
There is no `/api/leagues` endpoint yet, so keep a small static list in the frontend for now
(`Standard`, `Hardcore`, `Solo Self-Found`, `Hardcore SSF`, …) and surface the `400`
`League 'X' was not found.` detail when a typed league does not exist upstream.

### 6.4 Error handling policy

| Status | UI action |
| --- | --- |
| `401` | show "log in" CTA and/or `location.assign("/auth/login")` |
| `400` | show `detail` inline next to the field that caused it |
| `404` (empty) | "no market data for this item / reference / league yet" + suggest another reference from `markets[]` |
| `404` (`No catalogue match`) | show the `detail`, which already points at `/api/items/lookup` |
| `429` | "too many requests, wait a few minutes" + back off (no `Retry-After` header is sent) |
| `502` / `503` | "temporarily unavailable" + retry with exponential backoff (2–3 attempts) |


---

## 7. Ready-to-use TypeScript client

Drop-in types mirroring the DTOs, plus a thin wrapper that handles the cookie, the problem+json
errors and the URL-encoding rules.

```ts
// src/api/types.ts
export type MatchKind = "Exact" | "Alias" | "Prefix" | "Contains";
export type HistoryRange = "24h" | "7d" | "30d";

export interface MarketOption {
  reference: string;
  referenceDisplayName: string;
  snapshotHourUtc: string;
  lowestRate: number | null;
  highestRate: number | null;
  volumeItem: number | null;
  volumeReference: number | null;
}

export interface ItemMatch {
  itemId: string;
  displayName: string;
  category: string;
  tradeAlias: string | null;
  matchKind: MatchKind;
  markets: MarketOption[];
}

export interface ItemLookupResponse {
  query: string;
  catalogUpdatedAtUtc: string | null;
  count: number;
  matches: ItemMatch[];
}

export interface ResolvedItem {
  input: string;
  itemId: string;
  displayName: string;
  matchKind: MatchKind | "None";
  alternatives: string[];
}

export interface ItemValuationResponse {
  item: ResolvedItem;
  reference: ResolvedItem;
  league: string;
  marketId: string;
  snapshotHourUtc: string;
  lowestRate: number | null;
  highestRate: number | null;
  volumeItem: number | null;
  volumeReference: number | null;
}

export interface ValuationResponse {
  itemId: string;
  referenceItemId: string;
  league: string;
  marketId: string;
  snapshotHourUtc: string;
  lowestRate: number | null;
  highestRate: number | null;
  volumeItem: number | null;
  volumeReference: number | null;
}

export interface HistoryPoint {
  snapshotHourUtc: string;
  lowestRate: number | null;
  highestRate: number | null;
  volumeItem: number | null;
  volumeReference: number | null;
}

export interface HistoryResponse {
  itemId: string;
  referenceItemId: string;
  league: string;
  range: HistoryRange;
  fromUtc: string;
  count: number;
  points: HistoryPoint[];
  cached: boolean;
}

export interface StashItem {
  itemId: string;
  league: string;
  name: string;
  baseType: string;
  frameType: number;
  itemLevel: number;
  stackSize: number | null;
  gemLevel: number | null;
  gemQuality: number | null;
  corrupted: boolean;
  verified: boolean;
  note: string | null;
}

export interface StashResponse {
  league: string;
  leagueId: string;
  lastRefreshedAt: string;
  count: number;
  items: StashItem[];
  cached: boolean;
}

export interface CatalogRefreshResponse {
  refreshedCount: number;
  totalCount: number;
  catalogUpdatedAtUtc: string | null;
}
```

```ts
// src/api/client.ts
import type {
  CatalogRefreshResponse, HistoryRange, HistoryResponse, ItemLookupResponse,
  ItemValuationResponse, StashResponse, ValuationResponse,
} from "./types";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly title?: string,
    public readonly detail?: string,
  ) {
    super(detail ?? title ?? `HTTP ${status}`);
  }
  get isAuth(): boolean { return this.status === 401; }
  get isRetryable(): boolean { return this.status === 429 || this.status === 502 || this.status === 503; }
}

/** Same-origin base — see §1. Empty string when the frontend is proxied behind the API. */
const BASE = "";

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(BASE + path, { credentials: "include", ...init });

  if (res.status === 204) return undefined as T;

  const isJson = (res.headers.get("content-type") ?? "").includes("json");
  const body: { title?: string; detail?: string } | null = isJson ? await res.json() : null;

  if (!res.ok) throw new ApiError(res.status, body?.title, body?.detail);
  return body as T;
}

const enc = encodeURIComponent;

function qs(params: Record<string, string | number | undefined>): string {
  const parts = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== "")
    .map(([k, v]) => `${enc(k)}=${enc(String(v))}`);
  return parts.length ? `?${parts.join("&")}` : "";
}

export const api = {
  health: () => request<{ status: string; timestampUtc: string }>("/health"),

  lookup: (q: string, league?: string, limit = 10) =>
    request<ItemLookupResponse>(`/api/items/lookup${qs({ q, league, limit })}`),

  valuationByName: (itemName: string, reference?: string, league?: string) =>
    request<ItemValuationResponse>(`/api/valuation/by-name/${enc(itemName)}${qs({ reference, league })}`),

  valuation: (itemId: string, reference: string, league: string) =>
    request<ValuationResponse>(`/api/valuation/${enc(itemId)}${qs({ reference, league })}`),

  history: (itemId: string, reference: string, league: string, range: HistoryRange) =>
    request<HistoryResponse>(`/api/history/${enc(itemId)}${qs({ reference, league, range })}`),

  stash: (league: string) => request<StashResponse>(`/api/stash${qs({ league })}`),

  refreshCatalog: () => request<CatalogRefreshResponse>("/api/items/catalog/refresh", { method: "POST" }),
};

/** "Metadata/Items/Currency/CurrencyRerollRare" -> "CurrencyRerollRare" (debug labels only). */
export const pathTail = (itemId: string): string => itemId.split("/").pop() ?? itemId;

/** Rates are reference-per-item, so small ones need precision, big ones decimals. */
export const formatRate = (rate: number | null): string =>
  rate === null ? "—" : rate >= 1 ? rate.toFixed(2) : rate.toPrecision(4);
```


---

## 8. Caching & polling guidance

| Data | Changes | Suggested client behaviour |
| --- | --- | --- |
| Lookup results (`matches`, `markets`) | catalogue hourly, markets hourly | cache per `(q, league, limit)` for ~5 min; the response already contains the latest rate, so a search page needs no follow-up call |
| Latest valuation | hourly | cache ~5 min; refresh on a 60 s timer only for a visible detail page |
| History | hourly, appended | cache ~5 min for `24h`, ~10 min for `7d`/`30d`; refetch on tab focus |
| Stash | user-driven, server cache 15 min | fetch on mount, expose a manual refresh, expect `cached: true` otherwise |
| `catalogUpdatedAtUtc` | hourly | show it as "catalogue updated …" so users know how fresh names are |

Rate limits are **30 requests / 5 min per endpoint per session**, so a stash grid that prices every
row with `/api/valuation/by-name` will hit `429` quickly. Batch strategy:

1. Prefer one `/api/items/lookup?league=…` call per distinct `name` and read `markets[0]` — that alone
   gives a price without a valuation call.
2. Only call `/api/valuation/{itemId}` for the row the user actually opened.
3. De-duplicate identical `(itemId, reference, league)` lookups within a render pass.

---

## 9. Gotchas

1. **Encode metadata paths in path segments.** `Metadata/Items/...` contains `/`. Send
   `Metadata%2FItems%2FCurrency%2FCurrencyRerollRare`; the API un-escapes the segment. Sending it raw
   produces extra route segments and a `404` from routing, not from the data layer.
2. **`404` has two meanings.** Empty body = "resolved fine, no snapshot". `No catalogue match` problem
   = "the name matched nothing". Different UI copy for each.
3. **History is `200` with an empty array; valuation is `404`.** Do not reuse one error branch for both.
4. **`lowestRate`/`highestRate` are already oriented.** They are always reference-per-item and always
   ascending — never re-invert them, and never display the raw `marketId` orientation as a hint.
5. **Retired items resolve only by metadata path.** The catalogue covers the current game version, so
   an item such as `Metadata/Items/Currency/CurrencyDeepwater` has snapshots but no catalogue entry:
   `/api/items/lookup?q=…` finds nothing, while `/api/valuation/{itemId}` still works. A "paste a
   metadata path" debug input is worth having.
6. **`limit` is clamped, not validated.** `limit=0` or `limit=1000` silently becomes 1 or 50.
7. **`429` carries no `Retry-After`.** Back off client-side (the window is 300 s).
8. **Login must be a navigation, not an XHR** — `/auth/login` answers `302` to
   `www.pathofexile.com`, which fetch cannot follow cross-origin.
9. **`SameSite=Strict` means a link from another site will not carry the cookie.** Start login from a
   page of your own origin.
10. **`itemLevel` on stash items is not nullable** (it is `0` for currency), while
    `stackSize`/`gemLevel`/`gemQuality`/`note` are.

---

## 10. Known open items (as of this revision)

- **OAuth is not yet usable end-to-end:** `GET /auth/login` currently answers `403` from PoeTrade
  because the callback URL is not registered on the GGG developer portal yet. Register the URL the
  browser will actually use — for the deployed stack that is the **published** host
  (`https://<your-host>/auth/callback`), not `https://localhost:7003/auth/callback` (§12.2). Until
  that is done, build the UI against a manually inserted `poe_sid` cookie (see §11) and expect `401`
  from the real flow.
- **No CORS support** in the API: same-origin hosting or a proxy is mandatory (§1).
- **No `/api/leagues` endpoint**: league names must be hard-coded client-side for now (§6.3).
- **`HistoryResponse.cached` is always `false`** in the current implementation.
- **No "who am I" endpoint**: the frontend cannot read the logged-in PoE username; detect login only
  by the absence of a `401`.

---

## 11. Manual smoke test (no browser)

```bash
# unauthenticated → 401
curl -i https://localhost:7003/api/items/lookup?q=chaos%20orb

# authenticated (cookie copied from a browser after a successful login)
curl -i -b "poe_sid=<session-id>" \
  "https://localhost:7003/api/items/lookup?q=chaos%20orb&league=Standard"

curl -i -b "poe_sid=<session-id>" \
  "https://localhost:7003/api/valuation/by-name/Chaos%20Orb?reference=Divine%20Orb&league=Standard"

curl -i -b "poe_sid=<session-id>" \
  "https://localhost:7003/api/valuation/Metadata%2FItems%2FCurrency%2FCurrencyRerollRare?reference=Metadata%2FItems%2FCurrency%2FCurrencyModValues&league=Standard"

curl -i -b "poe_sid=<session-id>" \
  "https://localhost:7003/api/history/Metadata%2FItems%2FCurrency%2FCurrencyRerollRare?reference=Metadata%2FItems%2FCurrency%2FCurrencyModValues&league=Standard&range=7d"

# bad league → 400 problem
curl -i -b "poe_sid=<session-id>" "https://localhost:7003/api/stash?league=Not%20A%20League"
```


---

## 12. Self-hosted deployment (Docker + UnRAID)

### 12.1 Recommended topology: one origin, TLS at the proxy

```
browser ──HTTPS──▶ reverse proxy (SWAG / Nginx Proxy Manager / Traefik) :443
                     │
                     ├─ /            ─▶ web container   (SPA static files)   http :80
                     ├─ /api/*       ─▶ api container   (Kestrel)             http :8080
                     ├─ /auth/*      ─▶ api container                         http :8080
                     └─ /health      ─▶ api container                         http :8080
                                          ├─▶ redis:6379      (user-defined bridge network)
                                          └─▶ postgres:5432
```

The browser only ever sees **one HTTPS origin**, so:

- no CORS is needed (the API has none, and adding it is the wrong fix here),
- `poe_sid` (`SameSite=Strict; Secure`) is same-site and secure, so it is always sent,
- the OAuth callback and the post-login `Redirect("/")` both land on the frontend.

UnRAID's own 443 is the management UI, not a place to publish apps — publish this stack through a
proxy container (SWAG, Nginx Proxy Manager or Traefik are all in Community Applications).

### 12.2 Configuration is env vars, and the app refuses to start without them

`ValuationOptionsValidator` runs before the first request; any missing value throws and the container
exits. Required in every environment:

| Config key | Container env var | Requirement |
| --- | --- | --- |
| `OAuth:ClientId` | `OAuth__ClientId` | non-empty |
| `OAuth:ClientSecret` | `OAuth__ClientSecret` | non-empty (redacted in logs) |
| `OAuth:RedirectUri` | `OAuth__RedirectUri` | absolute **https** URI, character-for-character equal to the URL registered at GGG |
| `Redis:ConnectionString` | `Redis__ConnectionString` | non-empty, e.g. `redis:6379` |
| `Sql:ConnectionString` | `Sql__ConnectionString` | non-empty, e.g. `Host=postgres;Port=5432;Database=poevaluation;Username=…;Password=…` |
| — | `ASPNETCORE_URLS` | `http://+:8080` (plain HTTP inside the docker network; TLS is the proxy's job) |
| — | `ASPNETCORE_ENVIRONMENT` | `Production` (so `appsettings.Development.json` defaults are not used) |

`OAuth:RedirectUri` is the one that changes when you leave your dev machine: it becomes
`https://<your-published-host>/auth/callback`, and that exact string must be registered in the GGG
developer portal. A mismatch is rejected by PoeTrade before the request reaches the container, and
surfaces as a `403` page from PoeTrade rather than an error from this API. If the portal rejects an
internal hostname, publish a real hostname for the callback (Cloudflare Tunnel, Tailscale Funnel, or a
public domain pointing at the proxy) — verify the portal's rules rather than assuming LAN-only works.

`Properties/launchSettings.json` (`https://localhost:7003`, `http://localhost:5024`) is used only by
`dotnet run` and Rider. It has no effect inside a container.

### 12.3 Cookies and TLS — the failure mode to expect

- `Secure = true` is hardcoded in `SessionCookie.Options`. The browser must reach the API over HTTPS.
  Over `http://` the login flow appears to work (PoeTrade redirects back, the callback runs) but the
  cookie is never stored, and every `/api/*` call then answers `401` with an empty body. Check
  DevTools → Application → Cookies after login before debugging anything else.
- `SameSite = Strict` is safe here **because the frontend is same-origin**. It is not the reason a
  deployed login fails.
- The PKCE `state` is stored in Redis (`oauth:state:{state}`), **not** in a cookie, so a cross-site
  callback navigation carries no cookie requirement and `SameSite=Strict` does not break the flow.
- There is **no `ForwardedHeaders` middleware**. Today that is harmless: the API never builds absolute
  URLs from the request (`RedirectUri` comes from config, the callback redirects to the relative `/`),
  and rate limits are keyed by session id (`ratelimit:{sessionId}:{endpoint}`), not client IP — so a
  shared LAN NAT does not throttle your users. Forward `X-Forwarded-For`/`X-Forwarded-Proto` anyway if
  you want meaningful logs, and add `UseForwardedHeaders` first if you ever introduce HSTS, CORS or
  anti-forgery.

### 12.4 Proxy rules (nginx / SWAG style)

```nginx
location = /health { proxy_pass http://api:8080; }
location ^~ /auth/ { proxy_pass http://api:8080; }
location ^~ /api/  { proxy_pass http://api:8080; }
location /         { try_files $uri $uri/ /index.html; }   # SPA fallback
```

- **`/auth/*` must reach the API, never the SPA router.** PoeTrade redirects the browser to
  `https://<host>/auth/callback?code=…&state=…`; if the SPA fallback serves `index.html` there, the
  callback handler never runs and login fails with no visible error.
- Keep `proxy_read_timeout` ≥ 30 s for `/api/stash` and `POST /api/items/catalog/refresh`: the PoE
  client timeout is `PathOfExile:RequestTimeoutSeconds = 30`, so a 10 s proxy timeout turns a slow
  upstream into a proxy error instead of the API's own `502`.
- No WebSockets, no streaming, no request body limits beyond the defaults.

### 12.5 Containers, volumes, replicas

| Container | Image | Needs | Notes |
| --- | --- | --- | --- |
| `api` | `mcr.microsoft.com/dotnet/aspnet:10.0` (project targets `net10.0`) | env vars from §12.2, outbound internet | runs the pollers as background services |
| `web` | your SPA build (nginx, Caddy, or the SPA's own image) | proxy rules from §12.4 | static files only |
| `redis` | `redis:7` | volume | sessions, caches, rate limits, service token, locks |
| `postgres` | `postgres:14+` | volume, `POSTGRES_DB=poevaluation` | hourly snapshots + item catalogue |

```yaml
# compose.yaml (UnRAID: put the stack on its own docker compose app)
services:
  api:
    image: poevaluation-api:latest
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      OAuth__ClientId: "<client-id>"
      OAuth__ClientSecret: "<client-secret>"
      OAuth__RedirectUri: "https://poe.lan.example.com/auth/callback"
      Redis__ConnectionString: "redis:6379"
      Sql__ConnectionString: "Host=postgres;Port=5432;Database=poevaluation;Username=poeval;Password=…"
    depends_on: [redis, postgres]
    expose: ["8080"]
  web:
    image: poevaluation-web:latest
    expose: ["80"]
  redis:
    image: redis:7
    volumes: ["/mnt/user/appdata/poevaluation/redis:/data"]
  postgres:
    image: postgres:16
    environment: { POSTGRES_DB: poevaluation, POSTGRES_USER: poeval, POSTGRES_PASSWORD: "…" }
    volumes: ["/mnt/user/appdata/poevaluation/postgres:/var/lib/postgresql/data"]
```

Both containers are reached only through the proxy (no host port publishing needed).

**Schema is not created by the app.** `db/schema.sql` is applied once, manually:

```bash
docker exec -i poevaluation-postgres-1 psql -U poeval -d poevaluation < db/schema.sql
```

What each store holds, and what you lose if a volume is deleted:

- **Redis** — `session:{id}` (90-day TTL, i.e. the login), `stash:{sessionId}`, `history:{itemId}:{range}`,
  `ratelimit:{sessionId}:{endpoint}`, `service:token:cxapi`, `league:{name}`, and the polling locks.
  Losing it logs every user out and forces a new PoE service token; market history is untouched.
- **Postgres** — hourly currency digests and the item catalogue. Losing it empties `/api/history`
  (`200` with `points: []`) and makes `/api/valuation/{itemId}` answer `404` until the poller refills.

Replicas: run **one** `api` container. The pollers are protected by Redis locks
(`lock:polling:{leagueId}`, 5 min auto-renewed; `lock:item-catalog`, 10 min), so extra replicas cannot
double-write — but each replica still runs its own polling loop and catch-up work for no benefit.

Health checks: `GET /health` returns `200 {"status":"Healthy","timestampUtc":…}` **without probing
Redis or Postgres**, so a `HEALTHCHECK` on it only proves the process is up. Dependency failures show
up as `503` on `/api/*` instead.

Outbound access: the `api` container must resolve and reach `api.pathofexile.com`,
`www.pathofexile.com` and `web.poecdn.com`. On an isolated bridge network without internet, `/api/stash`
and the pollers fail as `502` (`Upstream unavailable`). Timezone settings are irrelevant — every
timestamp is UTC and the poller works in completed hours.

### 12.6 If you keep the frontend on a separate origin

| Frontend location | Works with today's code? | What it would take |
| --- | --- | --- |
| Same HTTPS origin via the proxy (recommended) | yes | nothing |
| Subdomain of the same site (`app.example.com` + `api.example.com`) | no | `AddCors` with `AllowCredentials = true` and an explicit origin list; `SameSite=Strict` is still fine (same site) |
| Different host/port or different site (`poe.lan:3000` + `poe.lan:7003`) | no | CORS **and** changing the cookie to `SameSite=None; Secure` — a code change in `SessionCookie.Options`, and it weakens CSRF protection |

With credentials-based cookie auth, CORS cannot use a wildcard origin; you must list each frontend
origin. Proxying under one origin is both less work and safer.

### 12.7 Deploy checklist

1. Publish `web` and `api` behind **one** HTTPS host (§12.1).
2. Register `https://<that-host>/auth/callback` in the GGG developer portal, then set the identical
   value in `OAuth__RedirectUri`.
3. Set every env var in §12.2 — the container exits with a `Configuration is invalid:` list if any is
   missing, so read the first log lines after the first boot.
4. Apply `db/schema.sql` to the Postgres container once.
5. Route `/api`, `/auth` and `/health` to the API; keep the SPA fallback out of `/auth` (§12.4).
6. Log in once and confirm `poe_sid` exists in DevTools with `HttpOnly`, `Secure` and `SameSite=Strict`.
   No cookie → you are on plain HTTP.
7. Point `/mnt/user/appdata/poevaluation/{redis,postgres}` at a share that is in your backup set.
8. Keep one `api` replica; use `Polling:Enabled=false` on any extra instance you add for testing.
9. Re-run the §11 smoke tests against the published host instead of `localhost`.

