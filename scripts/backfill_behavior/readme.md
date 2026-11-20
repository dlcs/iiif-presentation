# Backfill Behavior

Python script to populate the `behavior` column of `manifests` and `collections` from the IIIF JSON stored in S3.

The API stores `behavior` whenever a manifest or collection is created or updated. The migration that added the
column populated `public-iiif` and `storage-collection` for collections from the old `is_public` and
`is_storage_collection` columns, but any other behaviors only exist in S3 until the resource is next written. This
script fills them in without waiting for a write.

* Only rows with no behaviors beyond `public-iiif` are processed (none at all for manifests), so the script is safe
  to re-run. Rows written since the migration with no behaviors are read from S3 again, which finds nothing to add.
* Storage collections are skipped, as they have no JSON in S3.
* `public-iiif` and `storage-collection` are ignored if found in S3. The API removes them before writing (since
  October 2024), and the database is the source of truth for them. Behaviors from S3 are appended after any already
  stored.
* A row is only updated if its `behavior` still has the value read at the start of the page, so a concurrent API
  write to a different value isn't overwritten.
* Resources with no additional behaviors are left unchanged.

Rows are processed in pages (`--batch-size`) in primary key order. The S3 objects for a page are read concurrently
(`--workers`), then all updates for the page are written in a single statement and committed. After each page a
progress line is logged with the last processed key:

```
manifests: processed 42000, last key 2/a1b2c3d4
```

If a run is interrupted, pass that key to `--start-after` to resume from the next page. At most one uncommitted page
is lost, and as rows are only updated while unchanged, re-processing it is harmless.

## Usage

```bash
pip install boto3 psycopg2-binary

# check the reconciliation against a sample - lists every change that would be made, without writing
python backfill_behavior.py --connection-string "host=localhost dbname=presentation user=postgres password=..." \
  --bucket my-presentation-storage-bucket --profile my-profile --dry-run --limit 500

# apply
python backfill_behavior.py --connection-string "..." --bucket my-presentation-storage-bucket --profile my-profile

# resume an interrupted manifests run
python backfill_behavior.py --connection-string "..." --bucket my-presentation-storage-bucket --profile my-profile \
  --type manifests --start-after 2/a1b2c3d4
```

Optional arguments:

* `--dry-run` - read from S3 and list each resource that would be updated, with its current and new behaviors,
  without writing.
  Summary counts are reported per resource type.
* `--limit <n>` - stop after processing `n` rows per resource type. Useful with `--dry-run` to check a sample.
* `--customer <id>` - only process resources for a single customer.
* `--type manifests` / `--type collections` - only process one resource type (can be repeated).
* `--batch-size <n>` - rows per page, and per commit. Defaults to `1000`.
* `--workers <n>` - concurrent S3 reads. Defaults to `16`.
* `--start-after <customer_id>/<id>` - resume after the given key, as logged by a previous run. Requires a single
  `--type`.
* `--region <region>` - AWS region, defaults to `eu-west-1`.
