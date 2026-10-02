# Storage Keys

IIIF-Presentation makes use of S3 to store manifests and collections. These keys all follow a known pattern, documented here.

> [!Note]
> All keys should be generated in a common class, changing use in 1 place should change _all_ usages

See `BucketHelperX` for code that generates keys.

| Name                             | Format                                                          | Example                                      | Description                                                                                         |
| -------------------------------- | --------------------------------------------------------------- | -------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| Manifest                         | `{Storage}/{Customer}/manifests/{Manifest-Id}`                  | `iiif-p/1/manifests/abc123`                  | IIIF manifests, whether saved as-is or generated                                                    |
| Original Manifest Payload        | `{Storage}/{Customer}/manifests/{Manifest-Id}/original`         | `iiif-p/1/manifests/abc123/original`         | Stores the original payload if the required modification by processing                              |
| Staged Manifest                  | `{Storage}/staging/{Customer}/manifests/{Manifest-Id}`          | `iiif-p/staging/1/manifests/abc123`          | In-flight generated IIIF manifests. Will contain in-complete `"items"` that will be populated later |
| Staged Original Manifest Payload | `{Storage}/staging/{Customer}/manifests/{Manifest-Id}/original` | `iiif-p/staging/1/manifests/abc123/original` | Stores the payload in the update scenario that requires background handler                          |
| IIIF Collections                 | `{Storage}/{Customer}/collections/{Collection-Id}`              | `iiif-p/1/collections/abc123`                | IIIF Collections                                                                                    |

## Prefixes

* `/{Customer}/` is the general prefix, keeps all resources for a customer together.
* `/staging/` groups those resources that are in-flight. These are relatively transient to useful to be able to identify independantly.

## Staging / Original

The Manifest can exist in multiple places (using terminology from above)
* `Manifest` - final/complete and publicly-servable IIIF Manifest.
* `Original Manifest Payload` - Manifest body from PUT/POST. This is updated when the processing of a Manifest is complete (ie it's saved to `Manifest`)
* `Staged Manifest` - in-flight Manifest that needs further work to complete (ie population from Adjuncts, Assets or text-service content).
* `Staged Original Manifest Payload` - Manifest body from PUT/POST for in-flight Manifest that needs further work.

Manifests can exist in multiple locations at once.

## Note on IIIF Identities

> [!Important]
> The `id` property of IIIF resources stored in S3 will **always** be the API (aka flat) path.

Each IIIF resource has 1 single API path (`/manifests/2/abc123`) but could eventually have multiple hierarchical paths: `/2/1940s/1984`, `/2/novels/dystopian/1984`, `/2/authors/english/o/1984`.