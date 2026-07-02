# Path Cusomisation

## Path Templates

The following resource keys can be configured for different hostnames. These are all set on appSettings `PathSettings:PathRules`, per-hostname overrides on `PathSettings:PathRules:Overrides:{hostname}:{path-type}`, where

> [!NOTE]
> Customisation is by hostname, _not_ customer specific. 

| Path Type         | Description                           | Default                                  | Example                     | `{resourceId}`                                                       | Uses                                             |
| ----------------- | ------------------------------------- | ---------------------------------------- | --------------------------- | -------------------------------------------------------------------- | ------------------------------------------------ |
| ResourcePublic    | Hierarchical Manifest and Collections | `/{customerId}/{hierarchyPath}`          | `/99/path/to/resource`      | N/A                                                                  | `"id"` generation. Incoming resource parsing     |
| ManifestPrivate   | API Manifests                         | `/{customerId}/manifests/{resourceId}`   | `/99/manifests/abcsa12321`   | Unique Id of Manifest                                                | `"id"` generation. Incoming resource parsing     |
| CollectionPrivate | API Collections                       | `/{customerId}/collections/{resourceId}` | `/99/collections/coll_1234`  | Unique Id of Collection                                              | `"id"` generation. Incoming resource parsing     |
| Canvas            | Canvases, all representations         | `/{customerId}/canvases/{resourceId}`    | `/99/collections/canvas_abc` | Unique Id of Canvas                                                  | `"id"` generation. Incoming resource parsing     |
| TextServiceJob    | TextServices jobId                    | `/{customerId}/iiif/{resourceId}`        | `/99/iiif/my_manifest`       | Id of [TextServices Job](../rfcs/0007-text-services.md#job-identity) | Set `X-Forwarded-Path` for text-service requests |

The above types are managed as constants in `Repository.Paths.PresentationResourceType`.

Related reading
* [RFC 0003 Identity-Rewrites](../rfcs/0003-identity-rewrites.md)
* [RFC 0007 Text-Services](../rfcs/0007-text-services.md)
* [Canvas Id Parsing notes](.canvas-id-parsing.md)

## Orchestrator + Presentation URL

In addition to the above per-hostname configurations, there are 2 settings that can be controlled per-customer.

### Orchestrator URL

Used to identify hostname to use when calling Orchestrator. This is used for NQ generation and text-service requests.

* `DLCS:OrchestratorUri` - default.
* `DLCS:CustomerOrchestratorUri:{customerId}` - customer specific override.

### Presentation URL

Used for parsing incoming paths and id generation.

* `PathSettings:PresentationApiUrl` - default.
* `PathSettings:CustomerPresentationApiUrl:{customerId}` - customer specific override.

Related reading:
* `OrchestratorUrl` - https://github.com/dlcs/iiif-presentation/issues/367
* `PresentationApiUrl` - https://github.com/dlcs/iiif-presentation/issues/370

## Helpers

Templates are mapped to `PathTemplate` type, which contains helpers for replacing `{customerId}`, `{resourceId}` etc values in strings.