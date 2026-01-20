/*
Bulk upsert by unique name (PostgreSQL) with XML or JSON input.

Input XML format:
<Items>
  <Item SourceId="1" Name="Alice" />
  <Item SourceId="2" Name="Bob" />
</Items>

Input JSON format:
{
  "Items": [
    { "SourceId": 1, "Name": "Alice" },
    { "SourceId": 2, "Name": "Bob" }
  ]
}

Behavior:
- For each Item, match destination row by name.
- If name exists: reuse existing id.
- If name is new: insert and generate id.
- Marks destination row as imported (is_imported = true).
- Returns a result set mapping source_id -> destination_id with success/inserted flags.
- Processes distinct names in batches (default 500) to keep writes predictable on large payloads.
- Designed for parallel callers: keep the unique constraint on name; inserts use ON CONFLICT to avoid duplicates.
*/

-- Demo table (adjust schema/table/columns as needed)
CREATE TABLE IF NOT EXISTS item_catalog
(
    id          BIGSERIAL PRIMARY KEY,
    name        TEXT NOT NULL,
    is_imported BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = CURRENT_SCHEMA()
          AND tablename = 'item_catalog'
          AND indexname = 'ux_item_catalog_name'
    ) THEN
        EXECUTE 'CREATE UNIQUE INDEX ux_item_catalog_name ON item_catalog(name)';
    END IF;
END $$;

CREATE OR REPLACE FUNCTION upsert_item_catalog_from_json(p_items_json JSONB, p_batch_size INT DEFAULT 500)
RETURNS TABLE
(
    source_id BIGINT,
    destination_id BIGINT,
    inserted BOOLEAN,
    success BOOLEAN,
    error_message TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_batch_size INT := 500;
BEGIN
    v_batch_size := CASE WHEN p_batch_size IS NULL OR p_batch_size <= 0 THEN 500 ELSE p_batch_size END;

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_input(source_id BIGINT NOT NULL, name TEXT NULL) ON COMMIT DROP;
    TRUNCATE tmp_item_input;

    INSERT INTO tmp_item_input(source_id, name)
    SELECT
        x.source_id,
        NULLIF(BTRIM(x.name), '') AS name
    FROM JSONB_TO_RECORDSET(COALESCE(p_items_json->'Items', '[]'::jsonb)) AS x(source_id BIGINT, name TEXT);

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_distinct_names(name TEXT PRIMARY KEY) ON COMMIT DROP;
    TRUNCATE tmp_item_distinct_names;

    INSERT INTO tmp_item_distinct_names(name)
    SELECT DISTINCT name
    FROM tmp_item_input
    WHERE name IS NOT NULL;

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_existing_names(name TEXT PRIMARY KEY) ON COMMIT DROP;
    TRUNCATE tmp_item_existing_names;

    INSERT INTO tmp_item_existing_names(name)
    SELECT d.name
    FROM item_catalog t
    JOIN tmp_item_distinct_names d ON d.name = t.name;

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_batch_names(name TEXT PRIMARY KEY) ON COMMIT DROP;

    LOOP
        TRUNCATE tmp_item_batch_names;

        INSERT INTO tmp_item_batch_names(name)
        SELECT name
        FROM tmp_item_distinct_names
        ORDER BY name
        LIMIT v_batch_size;

        EXIT WHEN NOT FOUND;

        INSERT INTO item_catalog(name, is_imported)
        SELECT name, TRUE
        FROM tmp_item_batch_names
        ON CONFLICT (name) DO UPDATE
            SET is_imported = EXCLUDED.is_imported;

        DELETE FROM tmp_item_distinct_names d
        USING tmp_item_batch_names b
        WHERE b.name = d.name;
    END LOOP;

    RETURN QUERY
    SELECT
        i.source_id,
        t.id AS destination_id,
        (i.name IS NOT NULL AND e.name IS NULL) AS inserted,
        (i.name IS NOT NULL) AS success,
        CASE WHEN i.name IS NULL THEN 'Name is required' ELSE NULL END AS error_message
    FROM tmp_item_input i
    LEFT JOIN item_catalog t ON t.name = i.name
    LEFT JOIN tmp_item_existing_names e ON e.name = i.name
    ORDER BY i.source_id;
END $$;

CREATE OR REPLACE FUNCTION upsert_item_catalog_from_xml(p_items_xml XML, p_batch_size INT DEFAULT 500)
RETURNS TABLE
(
    source_id BIGINT,
    destination_id BIGINT,
    inserted BOOLEAN,
    success BOOLEAN,
    error_message TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_batch_size INT := 500;
BEGIN
    v_batch_size := CASE WHEN p_batch_size IS NULL OR p_batch_size <= 0 THEN 500 ELSE p_batch_size END;

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_input(source_id BIGINT NOT NULL, name TEXT NULL) ON COMMIT DROP;
    TRUNCATE tmp_item_input;

    INSERT INTO tmp_item_input(source_id, name)
    SELECT
        NULLIF((xpath('string(@SourceId)', x))[1]::text, '')::BIGINT AS source_id,
        NULLIF(BTRIM((xpath('string(@Name)', x))[1]::text), '') AS name
    FROM UNNEST(xpath('/Items/Item', p_items_xml)) AS t(x);

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_distinct_names(name TEXT PRIMARY KEY) ON COMMIT DROP;
    TRUNCATE tmp_item_distinct_names;

    INSERT INTO tmp_item_distinct_names(name)
    SELECT DISTINCT name
    FROM tmp_item_input
    WHERE name IS NOT NULL;

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_existing_names(name TEXT PRIMARY KEY) ON COMMIT DROP;
    TRUNCATE tmp_item_existing_names;

    INSERT INTO tmp_item_existing_names(name)
    SELECT d.name
    FROM item_catalog t
    JOIN tmp_item_distinct_names d ON d.name = t.name;

    CREATE TEMP TABLE IF NOT EXISTS tmp_item_batch_names(name TEXT PRIMARY KEY) ON COMMIT DROP;

    LOOP
        TRUNCATE tmp_item_batch_names;

        INSERT INTO tmp_item_batch_names(name)
        SELECT name
        FROM tmp_item_distinct_names
        ORDER BY name
        LIMIT v_batch_size;

        EXIT WHEN NOT FOUND;

        INSERT INTO item_catalog(name, is_imported)
        SELECT name, TRUE
        FROM tmp_item_batch_names
        ON CONFLICT (name) DO UPDATE
            SET is_imported = EXCLUDED.is_imported;

        DELETE FROM tmp_item_distinct_names d
        USING tmp_item_batch_names b
        WHERE b.name = d.name;
    END LOOP;

    RETURN QUERY
    SELECT
        i.source_id,
        t.id AS destination_id,
        (i.name IS NOT NULL AND e.name IS NULL) AS inserted,
        (i.name IS NOT NULL) AS success,
        CASE WHEN i.name IS NULL THEN 'Name is required' ELSE NULL END AS error_message
    FROM tmp_item_input i
    LEFT JOIN item_catalog t ON t.name = i.name
    LEFT JOIN tmp_item_existing_names e ON e.name = i.name
    ORDER BY i.source_id;
END $$;

-- Example usage:
-- SELECT * FROM upsert_item_catalog_from_json('{"Items":[{"SourceId":1,"Name":"Alice"},{"SourceId":2,"Name":"Bob"}]}'::jsonb, 500);
-- SELECT * FROM upsert_item_catalog_from_xml('<Items><Item SourceId="1" Name="Alice"/><Item SourceId="2" Name="Bob"/></Items>'::xml, 500);
