-- =============================================================================
-- PoEValuation — PostgreSQL database schema
--
-- Idempotent: safe to run repeatedly. Run this script once on the target
-- PostgreSQL instance before first starting PoE.Valuation.Api (the
-- application no longer creates the schema at startup).
--
-- One-time database creation (from any machine that has psql):
--     psql -h <host> -U postgres -d postgres -c "CREATE DATABASE poevaluation;"
--
-- Then run this script against the database it creates (from the repo root):
--     psql -h <host> -U postgres -d poevaluation -f db/schema.sql
--
-- Sql:ConnectionString must point at that database, e.g.:
--     Host=<host>;Port=5432;Database=poevaluation;Username=postgres;Password=<password>
-- =============================================================================

CREATE TABLE IF NOT EXISTS exchange_rate_snapshots (
    snapshot_hour_utc TIMESTAMPTZ NOT NULL, -- start of the digest hour, UTC
    league            TEXT        NOT NULL,
    market_id         TEXT        NOT NULL, -- compound 'itemA|itemB' key from the API, unique per league
                                            -- (the same pair is listed under multiple leagues in one digest)
    currency_a        TEXT        NOT NULL,
    currency_b        TEXT        NOT NULL,
    metrics_json      JSONB       NOT NULL, -- hourly market digest JSON (volume_traded / lowest_stock / ... keyed by item id)
    CONSTRAINT pk_exchange_rate_snapshots PRIMARY KEY (snapshot_hour_utc, league, market_id)
);

CREATE TABLE IF NOT EXISTS polling_state (
    id               INT         NOT NULL,
    last_change_id   BIGINT      NULL,      -- most recent next_change_id from the API; NULL until first commit
    updated_at_utc   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT pk_polling_state PRIMARY KEY (id),
    CONSTRAINT ck_polling_state_id CHECK (id = 1)
);

-- Single-row sentinel the poller uses for optimistic cursor hand-over.
INSERT INTO polling_state (id)
VALUES (1)
ON CONFLICT (id) DO NOTHING;

-- -----------------------------------------------------------------------------
-- Item catalogue: item name -> metadata path
--
-- The currency-exchange snapshots are keyed by PoE metadata paths
-- (Metadata/Items/Currency/CurrencyRerollRare), while the stash API only publishes an item's
-- name and base type. This table is the map between the two, refreshed wholesale from the PoE
-- trade data catalogue by ItemMetadataCatalogRefresher.
--
-- lookup_key is the normalized display name (ItemNameNormalizer.Normalize: lower-cased,
-- apostrophes/periods removed, other separators collapsed to single spaces). It is what the
-- lookup endpoints search, so a client never has to reproduce PoE's exact spelling.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS item_metadata (
    metadata         TEXT        NOT NULL, -- Metadata/Items/Currency/CurrencyRerollRare
    display_name     TEXT        NOT NULL, -- Chaos Orb
    category         TEXT        NOT NULL, -- Currency | Maps | ... ('' when the catalogue gave none)
    trade_alias      TEXT        NULL,     -- chaos (the trade site's item alias)
    lookup_key       TEXT        NOT NULL, -- normalized display name: 'chaos orb'
    refreshed_at_utc TIMESTAMPTZ NOT NULL, -- when this row was written by the last catalogue swap
    CONSTRAINT pk_item_metadata PRIMARY KEY (metadata),
    CONSTRAINT ck_item_metadata_lookup_key CHECK (lookup_key <> '')
);

-- The lookup filters on lookup_key equality/prefix/substring; the planner uses this index for the
-- equality and prefix arms and for the ORDER BY length(lookup_key) tie-break.
CREATE INDEX IF NOT EXISTS ix_item_metadata_lookup_key ON item_metadata (lookup_key);

