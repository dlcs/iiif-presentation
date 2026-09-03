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
* `PathSettings:LegacyPresentationApiUrl` - optional legacy hostname (e.g. `presentation-api.*`, while `PresentationApiUrl`
  is moving to `iiif.*`). A deployment that has never had a legacy hostname can leave this unset.
* `PathSettings:LegacyHostnameCutoffDate` - cut-off date used alongside `LegacyPresentationApiUrl`.

When generating the flat (private) `"id"` of a Manifest/Collection/Canvas, the host is chosen with the following
precedence:
1. `CustomerPresentationApiUrl` override, if set for the customer.
2. `LegacyPresentationApiUrl`, if set and the resource's `Created` date is before `LegacyHostnameCutoffDate`.
3. `PresentationApiUrl`.

> [!NOTE]
> The public hierarchical `"id"` always uses `CustomerPresentationApiUrl` if set, else `PresentationApiUrl` -
> `LegacyPresentationApiUrl` is never used there, regardless of the resource's `Created` date. This keeps existing
> flat ids stable (avoiding breaking changes for consumers who saved them) while still pushing everyone towards the
> current hostname for the human/public-facing hierarchical paths.

### Legacy Host Redirect

When `PathSettings:LegacyPresentationApiUrl` is set, `LegacyHostRedirectMiddleware` redirects any request received on
that hostname to the equivalent path on the current hostname (customer specific override, else `PresentationApiUrl`):
* `GET` (and `HEAD`) requests get a `301 - Moved Permanently`.
* `PUT`/`POST`/`DELETE`/`PATCH` requests get a `308 - Permanent Redirect`, since a `301` risks clients dropping the
  request body and switching the verb to `GET`.

Where possible, an anonymous/unauthorised `GET` of a flat Manifest/Collection is redirected straight to its public
hierarchical url on the new hostname, rather than to the equivalent flat url there - this saves the extra
`303 - See Other` hop that would otherwise happen when that flat url is requested again on the new hostname.

No redirect happens if `LegacyPresentationApiUrl` is unset, or the request isn't for that hostname.

Related reading:
* `OrchestratorUrl` - https://github.com/dlcs/iiif-presentation/issues/367
* `PresentationApiUrl` - https://github.com/dlcs/iiif-presentation/issues/370
* `LegacyPresentationApiUrl` - https://github.com/dlcs/iiif-presentation/issues/654, [ADR 0004 - Moving `presentation-api.*` to `iiif.*`](https://github.com/dlcs/private-protagonist/blob/main/docs/adr/0004-move-presentation-url.md)
* Legacy host redirects - https://github.com/dlcs/iiif-presentation/issues/653

## Helpers

Templates are mapped to `PathTemplate` type, which contains helpers for replacing `{customerId}`, `{resourceId}` etc values in strings.