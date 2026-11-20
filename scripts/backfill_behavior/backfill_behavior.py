"""
Backfill the `behavior` column of the `manifests` and `collections` tables from the IIIF JSON stored in S3.

Only rows that have no behaviors other than the presentation-specific ones (`public-iiif`, `storage-collection`)
are considered. Storage collections are skipped as they have no stored JSON. Presentation-specific behaviors are
ignored if found in S3 (the API removes them before writing, since October 2024); the database is the source of truth
for them, and behaviors from S3 are appended after any already stored.

Rows are processed in pages ordered by primary key (id, customer_id). S3 objects in a page are read concurrently, then any
updates for the page are written and committed together. After each page the last processed key is logged; pass it
to --start-after to resume an interrupted run.
"""
import argparse
import json
import sys
from concurrent.futures import ThreadPoolExecutor

import boto3
import psycopg2
from botocore.config import Config
from botocore.exceptions import ClientError
from psycopg2.extras import execute_values

PRESENTATION_BEHAVIORS = {"public-iiif", "storage-collection"}

# rows with no behaviors beyond the presentation-specific ones
# manifests: wherever behavior count = 0
# collections: non-storage collections whose behavior is empty or only public-iiif
RESOURCE_FILTERS = {
    "manifests": "cardinality(behavior) = 0"
    "collections": "NOT ('storage-collection' = ANY(behavior)) AND behavior <@ ARRAY['public-iiif']::text[]",
}


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--connection-string", required=True,
                        help="libpq connection string, e.g. 'host=localhost dbname=presentation user=postgres'")
    parser.add_argument("--bucket", required=True, help="Presentation storage bucket name")
    parser.add_argument("--profile", help="AWS profile to use (defaults to standard credential chain)")
    parser.add_argument("--region", default="eu-west-1", help="AWS region (default: eu-west-1)")
    parser.add_argument("--customer", type=int, help="Only backfill resources for this customer")
    parser.add_argument("--type", choices=RESOURCE_FILTERS.keys(), action="append",
                        help="Resource type to backfill; can be repeated (default: both)")
    parser.add_argument("--batch-size", type=int, default=1000,
                        help="Rows read from the database, and committed, per page (default: 1000)")
    parser.add_argument("--workers", type=int, default=16, help="Concurrent S3 reads (default: 16)")
    parser.add_argument("--start-after", metavar="CUSTOMER_ID/ID",
                        help="Resume after this key, as logged by a previous run. Requires a single --type")
    parser.add_argument("--limit", type=int, help="Stop after processing this many rows per resource type")
    parser.add_argument("--dry-run", action="store_true",
                        help="Read from S3 and list every change that would be made, without writing")

    args = parser.parse_args()
    if args.start_after is not None:
        if not args.type or len(args.type) != 1:
            parser.error("--start-after requires exactly one --type")
        args.start_after = parse_key(parser, args.start_after)
    return args


def parse_key(parser, value):
    customer_id, sep, resource_id = value.partition("/")
    if not sep or not resource_id or not customer_id.isdigit():
        parser.error(f"--start-after must be in the form CUSTOMER_ID/ID, got '{value}'")
    return int(customer_id), resource_id


def read_behavior(s3, bucket, key):
    """Read the behavior list from the IIIF JSON at the given key, or None if the object doesn't exist"""
    try:
        response = s3.get_object(Bucket=bucket, Key=key)
    except ClientError as e:
        if e.response["Error"]["Code"] in ("NoSuchKey", "404"):
            return None
        raise

    iiif = json.load(response["Body"])
    return [b for b in iiif.get("behavior") or [] if b not in PRESENTATION_BEHAVIORS]


def fetch_page(conn, table, customer, after, page_size):
    """
    Fetch the next page of (customer_id, id, behavior) rows still to backfill, starting after the (customer_id, id)
    `after`. Ordered by (id, customer_id) to follow the primary key.
    """
    query = f"SELECT customer_id, id, behavior FROM {table} WHERE {RESOURCE_FILTERS[table]}"
    params = []
    if customer is not None:
        query += " AND customer_id = %s"
        params.append(customer)
    if after is not None:
        customer_id, resource_id = after
        query += " AND (id, customer_id) > (%s, %s)"
        params.extend([resource_id, customer_id])
    query += " ORDER BY id, customer_id LIMIT %s"
    params.append(page_size)

    with conn.cursor() as cursor:
        cursor.execute(query, params)
        return cursor.fetchall()


def write_updates(conn, table, updates):
    """
    Set behavior for each (customer_id, id, existing, behavior), only where it still has the existing value read
    from the database, and commit
    """
    with conn.cursor() as cursor:
        execute_values(
            cursor,
            f"""UPDATE {table} AS t SET behavior = v.behavior
                FROM (VALUES %s) AS v(customer_id, id, existing, behavior)
                WHERE t.customer_id = v.customer_id AND t.id = v.id AND t.behavior IS NOT DISTINCT FROM v.existing""",
            updates,
            template="(%s, %s, %s::text[], %s::text[])")
    conn.commit()


def backfill(conn, s3, executor, args, table, start_after):
    processed = updated = empty = missing = 0
    after = start_after

    if after is not None:
        print(f"{table}: resuming after {after[0]}/{after[1]}")

    while args.limit is None or processed < args.limit:
        page_size = args.batch_size if args.limit is None else min(args.batch_size, args.limit - processed)
        rows = fetch_page(conn, table, args.customer, after, page_size)
        if not rows:
            break

        keys = [f"{customer_id}/{table}/{resource_id}" for customer_id, resource_id, _ in rows]
        behaviors = executor.map(lambda k: read_behavior(s3, args.bucket, k), keys)

        updates = []
        for (customer_id, resource_id, existing), key, behavior in zip(rows, keys, behaviors):
            if behavior is None:
                print(f"  {key}: not found in S3, skipping")
                missing += 1
                continue

            additional = [b for b in behavior if b not in (existing or [])]
            if not additional:
                empty += 1
                continue

            merged = (existing or []) + additional
            if args.dry_run:
                print(f"  {key}: {existing} -> {merged}")
            updates.append((customer_id, resource_id, existing, merged))

        if updates and not args.dry_run:
            write_updates(conn, table, updates)

        processed += len(rows)
        updated += len(updates)
        after = rows[-1][:2]
        print(f"{table}: processed {processed}, last key {after[0]}/{after[1]}", flush=True)

    action = "would update" if args.dry_run else "updated"
    print(f"{table}: {action} {updated}, no behavior {empty}, missing from S3 {missing}")


def main():
    args = parse_args()
    session = boto3.Session(profile_name=args.profile, region_name=args.region)
    s3 = session.client("s3", config=Config(max_pool_connections=args.workers))

    with psycopg2.connect(args.connection_string) as conn, ThreadPoolExecutor(max_workers=args.workers) as executor:
        for table in args.type or RESOURCE_FILTERS.keys():
            backfill(conn, s3, executor, args, table, args.start_after)

    return 0


if __name__ == "__main__":
    sys.exit(main())
