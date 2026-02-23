# Database update failure
* Status: decided
* Author: Jack Lewis
* Decider: Jack Lewis
* Date: 2026-02-23

## Context and Problem Statement

As part of the upgrade to pg14, the update failed with the following error:

```
pg_restore: creating TABLE "public214e5tgjdk8eb8p62lxhenbcaclytnjl.collections"
pg_restore: while PROCESSING TOC:
pg_restore: from TOC entry 203; 1259 3358458 TABLE collections presentation_user
pg_restore: error: could not execute query: ERROR:  function digest(text, unknown) does not exist
LINE 5:       ELSE encode(substr(digest(ts::text || txt, 'sha256'), ...
                                 ^
HINT:  No function matches the given name and argument types. You might need to add explicit type casts.
QUERY:  
  SELECT
    CASE
      WHEN ts IS NULL THEN '00000000-0000-0000-0000-000000000000'::uuid
      ELSE encode(substr(digest(ts::text || txt, 'sha256'), 1, 16), 'hex')::uuid
    END

CONTEXT:  SQL function "deterministic_uuid_sha256" during inlining
Command was: 
-- For binary upgrade, must preserve pg_type oid
SELECT pg_catalog.binary_upgrade_set_next_pg_type_oid('3358460'::pg_catalog.oid);


-- For binary upgrade, must preserve pg_type array oid
SELECT pg_catalog.binary_upgrade_set_next_array_pg_type_oid('3358459'::pg_catalog.oid);


-- For binary upgrade, must preserve pg_class oids
SELECT pg_catalog.binary_upgrade_set_next_heap_pg_class_oid('3358458'::pg_catalog.oid);
SELECT pg_catalog.binary_upgrade_set_next_toast_pg_class_oid('4717976'::pg_catalog.oid);
SELECT pg_catalog.binary_upgrade_set_next_index_pg_class_oid('4717978'::pg_catalog.oid);
```

From this, clearly the error is around the `digest` function. However, the suggested fix of adding explicit type cast (i.e.: `digest(ts::text || txt, 'sha256'::text)`) was attempted, but this still caused the same error (outside of changing the error message to `ERROR:  function digest(text, text) does not exist`) 

### Investigation

This `digest` function is used as part of the `deterministic_uuid_sha256` function that is used to create an `Etag` as part of a computed column. Additionally, the `digest` function was found to be part of the `pgcrypto` library.

In order to test this issue, a new database was spun up using the latest snapshot from the existing dev database running on pg13 so that the fix could be isolated and so that upgrades/rollbacks would not impact users of the dev database.

Initially, this was assumed to be an issue with the `search_path` not being able to see the `digest` function, but after checking the database with the query:

```sql
SELECT proname as name, nspname as schema
FROM pg_proc f
LEFT JOIN pg_depend d ON d.objid = f.oid AND d.deptype = 'e'
LEFT JOIN pg_extension e ON e.oid = d.refobjid
JOIN pg_namespace n ON n.oid = f.pronamespace
WHERE f.proname = 'digest';
```

it could be seen that the `digest` function existed in the public schema, which should be on the default `search_path`

After this, it was possible that there was an issue with that version of the `pgcrypto`, but after checking this, it was dicovered that this was the latest version of the library (outside of a small update to another function in pg18, which had only just been released)

Next steps were is that the `pgcrypto` library was installed by the presentation database user, as opposed to the admin, so it was worth checking that the user had the ability to install extensions. The answer to this question was "sort of" - while the extension *was* installed and usable for the presentation schema, there is a set of steps that were nopt performed to allow the presentation user to install the `pgcrypto` extension, which are as below:

```sql
alter user <presentation user> set rds.allowed_delegated_extensions = 'pgcrypto';
```

> NOTE: this needs to be run as the admin user

However, while this would allow the user to install the extension properly, the same issue was still occurring. So something else needed to be done.  


This turned out to be the actual issue. Which is that the `Etag` column was a computed column that store their expression in the table metadata. When pg_upgrade runs, it tries to recreate the table schema before the extensions are fully initialized or within a restricted `search_path`.  Because the column definition likely looks like `GENERATED ALWAYS AS (digest(data, 'sha256'))`, Postgres can't find the digest function during the metadata restore phase, and the whole upgrade bails.

## Decision Drivers

- No data loss - there should be nothing lost in the current database
- Ease of use - lack of long term maintenance
- Reduce downtime - The fix should be as quick to apply as possible

## Considered Options


- Drop and recreate the column after update
- Use a wrapper function to trick the validator
  - Create a wrapper function in `pg_catalog` to call `digest` which should be available everywhere
- Move to public schema
- Replace the `digest` function with an inbuilt function

## Decision Outcome

Clearly, from the above, finding a function to replace `digest` would be the optimal fix.  From looking at the signature of this function, it's creating a sha256, based on a `timestampz`.  After checking through the documentation, there was actually a `sha256` function added to pg13 which would perform the same way.  Using this function would look like this:

```
sha256(ts::text::bytea)
```

and then while testing, these 2 functions do provide the same output:

```sql
SELECT sha256('2026-11-03 00:00:00-07'::timestamptz::text::bytea);

SELECT digest('2026-11-03 00:00:00-07'::timestamptz::text::bytea, 'sha256');
```

So this is a viable alternative to using the `digest` function.

The double cast is annoying as `digest` can accept `text`, whereas `sha256` only accepts a byte array, but this feels like a small issue in the face of no longer relying on the `digest` function and it's entirely possible that the library is calling byte array internally anyway, so this will be the chosen fix.  Additionally, as the `pgcrypto` library is only used in this single place, the `pgcrypto` library can also be removed as it's no longer needed.

Essentially then, the fix for this issue is to create a new migration that removes the `pgcrypto` library and modifies the `deterministic_uuid_sha256` with the following SQL:

```sql
CREATE OR REPLACE FUNCTION deterministic_uuid_sha256(ts timestamptz, txt text)
RETURNS uuid
LANGUAGE sql
IMMUTABLE
AS $$
  SELECT
    CASE
      WHEN ts IS NULL THEN '00000000-0000-0000-0000-000000000000'::uuid
      ELSE encode(substr(sha256(ts::text::bytea), 1, 16), 'hex')::uuid
    END
$$;
```

## Pros and Cons of the Options

### Drop and recreate the column after update

- This would cause the function to not exist during the update
- Data would be lost when dropping the column that would need to be recreated
- Would need to be redone every time a database upgrade is performed
- Additional downtime while the column is dropped and recreated

### Use a wrapper function to trick the validator
- RDS limits your ability to write into `pg_catalog`, so this might not work
- It's essentially adding additional complexity to the function
- Unknown effect on downtime - but likely not much
- No need to do further fixes once applied

### Move to public schema
- This was attempted, but it didn't work

### Replace the `digest` function with an inbuilt function

- Required some additional investigation
- No issues with data loss as the column still exists
- Fix can be applied asynchronously, so downtime would be limited to the upgrade
- No need to do further fixes once applied

