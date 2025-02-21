# Storage Keys

IIIF-Presentation makes use of S3 to store manifests and collections. These keys all follow a known pattern, documented here.

> [!Note]
> All keys should be generated in a common class, changing use in 1 place should change _all_ usages

See `BucketHelperX` for code that generates keys.

| Name             | Format                                                | Example                            | Description                                                                                         |
| ---------------- | ----------------------------------------------------- | ---------------------------------- | --------------------------------------------------------------------------------------------------- |
| Manifest         | `{Storage}/{Customer}/manifests/{Manifest-Id}`        | `iiif-p/1/manifests/abc123`        | IIIF manifests, whether saved as-is or generated                                                    |
| Staged Manifest* | `{Storage}/staged/{Customer}/manifests/{Manifest-Id}` | `iiif-p/staged/1/manifests/abc123` | In-flight generated IIIF manifests. Will contain in-complete `"items"` that will be populated later |
| IIIF Collections | `{Storage}/{Customer}/collections/{Collection-Id}`    | `iiif-p/1/collections/abc123`      | IIIF Collections                                                                                    |

* `*` indicates that this is a proposed key and not currently used. `/staged/` key will allow easy clean-up of 'stale' in-flight manifests using prefix lifecycle rules.

## Note on IIIF Identities

> [!Important]
> The `id` property of IIIF resources stored in S3 will **always** be the API (aka flat) path.

Each IIIF resource has 1 single API path (`/manifests/2/abc123`) but could eventually have multiple hierarchical paths: `/2/1940s/1984`, `/2/novels/dystopian/1984`, `/2/authors/english/o/1984`.
