# Mixed Manifests

"Mixed manifests" refers to the ability for API consumers to created manifests using `"items"`, for standard IIIF, and/or `"paintedResources"`, to instruct IIIF-Presentation to ingest or refer to a IIIF-CS managed asset.

As mentioned in the [Manifest Write (MVP)](0002-manifest-write-mvp.md):

> MVP doesn't support mixing `"paintedResources"` and `"items"` properties 

The above applies to create and update operations; API consumers had to pick which method they were using to manage Manifests and stick to that in subsequent requests for the same resource.

This applied to all early [releases](https://github.com/dlcs/iiif-presentation/releases) of IIIF Presentation, as of the time of writing v0.8.0 is the targeted release for mixed-manifest support.

This RFC outlines the approach to how we will support mixing create and update methods. These are explored in more detail in https://github.com/dlcs/iiif-presentation-tests/compare/api-scratchpad...feature/mixed-manifest-scratchpad

> [!TIP]
> The linked mixed-manifest-scratchpad tests contain a lot of information that should give some context to decisions outlined here.

## Rules 

### Overview

* Manifests can be written with `"items"` alone, `"paintedResources"` alone or a combination of both.
* Behaviour for handling `"paintedResources"` without `"items"` remains unchanged.
* Behaviour for handling `"items"` without `"paintedResources"` slightly changes as we will now identify managed Assets (initially only via canonical path).
* For PUT and POST operations all submitted payloads are only examined on their own, we do not combine with any data we've seen before (e.g. data stored in S3). Each payload contains the intent for the final Manifest.

> [!WARNING]
> Highlighting that the last point, above, could lead to destructive operations. 
>
> If a user has carefully curated properties on Canvases and submit only `"paintedResources"` then all of the Canvas-level properties will be lost. To mitigate this they would need to submit both `"items"` and `"paintedResources"` together.
>
> Purely additive payloads are not yet supported - these will come in the future as PATCH operations or a specific `/paintedResources` endpoint.

## Rules

Below sections examine how we will handle different payload shapes

### `"paintedResources"` only

> [!TIP]
> `"paintedResources"` only means `"paintedResources"` are in payload but there are no Canvases (`"items"` property) included - there can be other arbitrary IIIF content on manifest.

The behaviour will be as-is:
* Assets associated with Manifest in Protagonist DB (via `Images.Manifests` column).
* All CanvasPainting records in DB have `AssetId` set.
* Resulting `"items"` in the final manifest have been generated from IIIF-CS content-resources.
* Manifest will be returned immediately if there are no ingest operations required, or asynchronously if ingest required.

> [!IMPORTANT]
> If the Manifest has previously been decorated with canvas-level IIIF properties then these will be lost.

### `"items"` only

> [!TIP]
> `"items"` only means there are Canvases but no `"paintedResources"` in payload.

The behaviour will be:
* Assets that are referenced in Canvases are associated with Manifest in Protagonist DB (via `Images.Manifests` column). See below for [identifying managed assets](#identify-managed-assets).
* All CanvasPainting records in DB have `CanvasOriginalId` set. Those referencing a managed asset have `AssetId` set.
  * To check - Will this cause any changed in behaviour if something has both?
* Resulting `"items"` in the final manifest are identical to initial payload (IIIF-Presentation won't change anything - even if the referenced content is a managed asset).
* Manifests will always be returned immediately, it's not possible to ingest an asset via `"items"`.

> [!CAUTION]
> If the Manifest has previously been created via `"paintedResources"` the link to those assets must be maintained.

### Both `"paintedResources"` and `"items"`

Accept both `"items"` (Canvases) and `"paintedResources"` in the same payload.

The behaviour will be a combination of the above two approaches, summarised as:
* Assets associated with Manifest in Protagonist DB (via `Images.Manifests` column). These will be Assets specified via `"paintedResources"` OR identified in `"items"` body.
* CanvasPainting records in DB will have a `CanvasOriginalId` if they originate from `"items"`. They may have `AssetId` if originating from `"paintedResource"` or from a Canvas referencing a managed asset.
* Resulting `"items"` in the final manifest are:
  * Identical to the initial payload IF they were supplied from `"items"` alone.
  * Generated from IIIF-CS content-resources if supplied from `"paintedResources"` alone.
  * A combination of provided JSON and IIIF-CS content-resources, if they were supplied from both (see below for validation related to this).
* Manifest may or may not be returned immediately, depending on whether Assets are ingested.
  * This may involve a change to the placeholder canvases that are returned to consumers as they may now contain full and partial Canvases (see [#227](https://github.com/dlcs/iiif-presentation/issues/227)).

The suggested approach to processing these would be to:
* Process the JSON `"items"` into `CanvasPainting` objects, or an interim object that models the intent of the Canvas.
* Compare this with the provided `"paintedResources"` to get a unified list of `CanvasPainting` (or `CanvasPainting`-like) objects.
* With this unified list we can validate that there are no conflicting instructions.
* The `canvasOrder` set on `CanvasPainting` objects from `"items"` is the order that they are provided. `canvasOrder` set on those from `paintedResources` is defaulted to the order that they are provided but can be controlled via explicit property.
  * With the caveat that `canvasOrder` stays constant if `choiceOrder` is increasing, so default values from the sequence of `paintedResources` need to take that into account.

Validation rules for when a payload contains a Canvas that will be created from both `"items"` and `"paintedResources"`:
* The `"items"` Canvas _cannot_ contain any AnnotationPages. If it does we reject as merging will be difficult. 
  * Should a use case require this then it would be a 2 step process.
* If there are `"items"` and `"paintedResources"` targeting the same final Canvas, the latter must have `"canvasPainting"` property (ie cannot just be `"asset"`) as we need some indication of where the final Canvas should be placed.
* Start with strict validation. If anything is ambiguous, return a 400|BadRequest with an explanation of what couldn't be understood. We can loosen these rules moving forward if required.

Following on from the above, the rules for concatenating to get a unified list should be defensive:
* `CanvasPainting` objects can be taken from `"items"` or `"paintedResources"`, or both.
* As the JSON `"items"` are processed first, these will all have a `canvasOrder` (and `choiceOrder` potentially). 
* `CanvasPainting` objects can't only match on `canvasOrder`. If they point to the same final Canvas then they must specify a `CanvasId`. This avoids any ambiguity.
* If the same `CanvasPainting` property (e.g. `canvasLabel` or `label`) is supplied by both `"items"` and `"paintedResource"` then they _must_ match, or fail.
* Accept short form canvasId via `"items"` if, and only if, it's used to join to a `canvasPainting` object. In this instance the `"id"` value of the canvas will be rewritten. _This is the only time we should be rewriting JSON values!_
 
## Identify managed assets

All Asset identification checks must take the CustomerId into consideration - Customer 99 can only identify Assets belonging to Customer 99.

To avoid exhaustively interrogating the body of every paintingAnnotation we should have a whitelist of known hostnames that the associated Protagonist instance is hosted on, this can be used to shortcut the checks - we should only attempt to identify an Asset if the hostname is in known hostnames whitelist. This would avoid potential issues where an ImageService for CustomerA's dedicated instance is included in a Manifest PUT to CustomerB's IIIF-P API.

> [!CAUTION]
> Some customers may have custom rewrite rules in place for Assets. The initial MVP implementation of mixed-manifests will only identify Assets that use the canonical path for the asset type. Identifying rewritten paths and attributing those paths to specific customers and spaces will come in a future development.

The process to identify whether a content resource is a managed asset is to look at the paintingAnnotation `"body"` property. Details are below on how this identification could be done for the different types of assets.

##### Images

The canonical path is `/iiif-img/{version?}/{customer}/{space}/{asset}/{image-request}`

Attempt to identify the Asset from any referenced ImageService on a `body` with `"type": "Image"`. If this is a Protagonist imageService then this asset must be managed by that instance. This will be `id`, for `ImageService3`, or `@id`, for `ImageService2`. 
* `$.items[*].items[*].items[*].body.service[*].id` OR `$.items[*].items[*].items[*].body.service[*]['@id']`
* Examples:
  * `https://dlcs.host/iiif-img/v3/99/10/tk_421` - explicit V3
  * `https://dlcs.host/iiif-img/v2/99/10/tk_421` - explicit V2
  * `https://dlcs.host/iiif-img/99/10/tk_421` - canonical version

We can also attempt to identify an Asset from `body.id` alone, which is a static image, not a IIIF ImageService.
* `$.items[*].items[*].items[*].body.id`
* Example:
  * `https://dlcs.host/iiif-img/99/10/tk_421/full/1350,900/0/default.jpg`

If the `body.id` is identified as one managed asset but its service is a different managed asset, then that's a 400|BadRequest. 

> [!NOTE]
> There may be legitimate reasons for wanting to have body and services that do not match, we won't implement until someone requires this.

##### Audio / Video

The canonical paths:
* Audio: `/iiif-av/{customer}/{space}/{asset}/full/max/default.{extension}`
* Video: `/iiif-av/{customer}/{space}/{asset}/full/full/max/max/0/default.{extension}`

Attempt to identify the Asset from the `body.id` only.
* `$.items[*].items[*].items[*].body.id`
* Examples
  * `https://dlcs.host/iiif-av/99/10/tk_421/full/max/default.mp3`
  * `https://dlcs.host/iiif-av/99/10/tk_421/full/full/max/max/0/default.mp4`

> [!TIP]  
> If there are multiple transcodes available the body may be a `"type": "Choice"`, in which case each choice can be checked.

##### File

The canonical paths: `/file/{customer}/{space}/{asset}`

> [!CAUTION]
> As there are a number of different ways that `/file/` paths could be referenced in a Manifest (`"seeAlso"`, `"rendering"`, `"accompanyingCanvas"` etc) we will not attempt to identify these.
>
> Referenced `/file/` types will be handled by adjuncts.

## Examples

Below are some example use-cases and sample payloads.

### 1. Ingest assets then decorate with JSON

Use case - I have an automated process that takes images from _{source system}_ and generates Manifest from these. Once they are complete, I open in ManifestEditor to add some canvas-level properties.

First payload, contains `"paintedResources"`
```json
{
    "type": "Manifest",
    "slug": "first-example",
    "parent": "-container-",
    "label": {
        "en": [
            "Example One"
        ]
    },
    "paintedResources": [
        {
            "asset": {
                "id": "first",
                "mediaType": "image/tiff",
                "origin": "https://example.org/images/first.tiff"
            }
        },
        {
            "asset": {
                "id": "second",
                "mediaType": "image/tiff",
                "origin": "https://example.org/images/second.tiff"
            }
        }
    ]
}
```

this will create the following in DB:

| manifest_id | canvas_id | canvas_original_id | asset_id     | canvas_order | choice_order |
| ----------- | --------- | ------------------ | ------------ | ------------ | ------------ |
| abc1        | alpha     | `null`             | 99/10/first  | 0            | `null`       |
| abc1        | beta      | `null`             | 99/10/second | 1            | `null`       |

> [!NOTE]
> Choice order `null` or `0` both mean _"this is not a choice"_ at the API level - *must* be 1-based. Otherwise people will introduce bugs from default values for int in whatever languages they are using.

If I follow up by PUTting

```json
{
    "type": "Manifest",
    "slug": "first-example",
    "parent": "-container-",
    "label": { "en": ["Example One"] },
    "service": [
        {
            "id": "https://customer.example/auth/clickthrough",
            "type": "AuthAccessService2",
            "label": { "en": ["This is just an example"] }
        }
    ],
    "items": [
        {
            "id": "https://presentation.example/99/canvases/alpha",
            "type": "Canvas",
            "rendering": [ // "rendering" added via vanilla JSON
                { 
                    "id": "https://customer.example/svg/first",
                    "type": "Image",
                    "label": { "en": ["SVG XML for page text"] },
                    "format": "image/svg+xml"
                }
            ],
            "seeAlso": [ // "seeAlso" added via vanilla JSON
                { 
                    "id": "https://customer.example/text/alto/alpha",
                    "type": "Dataset",
                    "profile": "http://www.loc.gov/standards/alto/v3/alto.xsd",
                    "label": { "none": ["METS-ALTO XML"] },
                    "format": "application/xml+alto"
                }
            ],
            "items": [
                {
                    "id": "https://presentation.example/99/canvases/alpha/annopages/0",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://presentation.example/99/canvases/alpha/annotations/0",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "id": "https://dlcs.example/iiif-img/99/10/first/full/max/0/default.jpg",
                                "type": "Image",
                                "format": "image/jpeg",
                                "service": [
                                    {
                                        "id": "https://digirati.io/images/manifests/basic_iiif_manifest",
                                        "type": "ImageService3",
                                        "profile": "level1",
                                        "service": [ // "service" added via vanilla JSON
                                            {
                                                "id": "https://customer.example/auth/probe/first",
                                                "type": "AuthProbeService2",
                                                "service": [
                                                    {
                                                        "id": "https://customer.example/auth/clickthrough",
                                                        "type": "AuthAccessService2"
                                                    }
                                                ]
                                            }
                                        ]
                                    }
                                ]
                            },
                            "target": "https://presentation.example/99/canvases/alpha"
                        }
                    ]
                }
            ]
        },
        {
            "id": "https://presentation.example/99/canvases/beta",
            "type": "Canvas",
            "metadata": [ // "metadata" added via vanilla JSON
                { 
                    "label": { "en": ["Something"] },
                    "value": { "en": ["Another thing"] }
                }
            ],
            "items": [
                {
                    "id": "https://presentation.example/99/canvases/beta/annopages/0",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://presentation.example/99/canvases/beta/annotations/0",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": { // standard content-resource from DLCS
                            },
                            "target": "https://presentation.example/99/canvases/beta"
                        }
                    ]
                }
            ]
        }
    ],
    "paintedResources": [] // intentionally cleared this
}
```

The result of this operation is that the above payload is stored in S3 exactly as it is. We received the above as a `"items"` only payload but identify and save the `assetId` in database:

| manifest_id | canvas_id | canvas_original_id                             | asset_id     | canvas_order | choice_order |
| ----------- | --------- | ---------------------------------------------- | ------------ | ------------ | ------------ |
| abc1        | alpha     | https://presentation.example/99/canvases/alpha | 99/10/first  | 0            | `null`       |
| abc1        | beta      | https://presentation.example/99/canvases/beta  | 99/10/second | 1            | `null`       |

### 2. Ingest assets and decorate with JSON in single transaction

Use case - I have an automated process that takes images from _{source system}_ and metadata from _{metadata system}_ and I want to generate a Manifest from these in one single payload.

Single payload, contains both `"paintedResources"` and `"items"`:
```json
{
    "type": "Manifest",
    "slug": "second-example",
    "parent": "-container-",
    "label": { "en": ["Example Two"] },
    "service": [
        {
            "id": "https://customer.example/auth/clickthrough",
            "type": "AuthAccessService2",
            "label": { "en": ["This is just an example"] }
        }
    ],
    "items": [
        {
            "id": "alpha", // Short form - will be set by API
            "type": "Canvas",
            "rendering": [ // "rendering" added via vanilla JSON
                { 
                    "id": "https://customer.example/svg/first",
                    "type": "Image",
                    "label": { "en": ["SVG XML for page text"] },
                    "format": "image/svg+xml"
                }
            ],
            "seeAlso": [ // "seeAlso" added via vanilla JSON
                { 
                    "id": "https://customer.example/text/alto/alpha",
                    "type": "Dataset",
                    "profile": "http://www.loc.gov/standards/alto/v3/alto.xsd",
                    "label": { "none": ["METS-ALTO XML"] },
                    "format": "application/xml+alto"
                }
            ]
            // No "items" - inclusion would invalidate. NOTE this means we can't exactly replicate example-1 in single payload
        },
        {
            "id": "beta",
            "type": "Canvas",
            "metadata": [ // "metadata" added via vanilla JSON
                { 
                    "label": { "en": ["Something"] },
                    "value": { "en": ["Another thing"] }
                }
            ]
            // Again - no "items"
        }
    ],
    "paintedResources": [
        {
            "canvasPainting": {
                "canvasId": "alpha",
                "canvasOrder": 0
            },
            "asset": {
                "id": "first",
                "mediaType": "image/tiff",
                "origin": "https://example.org/images/first.tiff"
            }
        },
        {
            "canvasPainting": {
                "canvasId": "beta",
                "canvasOrder": 1
            },
            "asset": {
                "id": "second",
                "mediaType": "image/tiff",
                "origin": "https://example.org/images/second.tiff"
            }
        }
    ]
}
```

this will create the following in DB:

| manifest_id | canvas_id | canvas_original_id | asset_id     | canvas_order | choice_order |
| ----------- | --------- | ------------------ | ------------ | ------------ | ------------ |
| def2        | alpha     | `null`             | 99/10/first  | 0            | `null`       |
| def2        | beta      | `null`             | 99/10/second | 1            | `null`       |

The final Manifest will be the above but with the respective PaintingAnnotation properties populated with content-resources for ingested assets.

### 3. Mix of "items" only and "paintedResources" in single transaction

Use case - I have an automated process that takes images from _{source system}_ and metadata from _{metadata system}_ and I want to generate a Manifest from these in one single payload. In addition to that I want to include some pure IIIF Canvases.

Single payload, contains both `"paintedResources"` and `"items"` to create 3 canvases:
* Canvas1, `alpha`, will contain `"rendering"` and `"seeAlso"` from JSON. Painting annotations will come from Protagonist.
* Canvas2, `beta`, will be as provided in JSON.
* Canvas3, `gamma`, will only contain content from Protagonist.

```json
{
    "type": "Manifest",
    "slug": "three-example",
    "parent": "-container-",
    "label": { "en": ["Example Three"] },
    "items": [
        {
            "id": "alpha", // Short form - will be set by API
            "type": "Canvas",
            "rendering": [ // "rendering" added via vanilla JSON
                {
                    "id": "https://customer.example/svg/first",
                    "type": "Image",
                    "label": {
                        "en": [
                            "SVG XML for page text"
                        ]
                    },
                    "format": "image/svg+xml"
                }
            ],
            "seeAlso": [ // "seeAlso" added via vanilla JSON
                {
                    "id": "https://customer.example/text/alto/alpha",
                    "type": "Dataset",
                    "profile": "http://www.loc.gov/standards/alto/v3/alto.xsd",
                    "label": {
                        "none": [
                            "METS-ALTO XML"
                        ]
                    },
                    "format": "application/xml+alto"
                }
            ]
            // No "items" - inclusion would invalidate. NOTE this means we can't exactly replicate example-1 in single payload
        },
        {
            // This entire canvas is treated as JSON only
            "id": "beta",
            "type": "Canvas",
            "metadata": [
                {
                    "label": { "en": ["Something"] },
                    "value": { "en": ["Another thing"] }
                }
            ],
            "items": [
                {
                    "id": "https://customer.example/canvas/beta",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://customer.example/canvas/beta/annotations/1",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "id": "http://customer.example/static_images/full/120,180/0/default.jpg",
                                "type": "Image",
                                "format": "image/jpeg",
                                "height": 180,
                                "width": 120,
                                "service": [ {} ] // standard IIIF, omitted
                            },
                            "target": "https://customer.example/canvas/beta"
                        },
                        {
                            "id": "https://customer.example/canvas/beta/annotations/2",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "type": "TextualBody",
                                "format": "text/html",
                                "value": "<p style='font-size:1000px; background-color: rgba(16, 16, 16, 0.5); padding:300px'>Something informative.</p>",
                                "language": "en"
                            },
                            "target": "https://customer.example/canvas/beta/canvas#xywh=5500,12200,8000,5000"
                        }
                    ]
                }
            ]
        }
    ],
    "paintedResources": [
        {
            "canvasPainting": {
                "canvasId": "alpha",
                "canvasOrder": 0
            },
            "asset": {
                "id": "first",
                "mediaType": "image/tiff",
                "origin": "https://example.org/images/first.tiff"
            }
        },
        {
            "canvasPainting": {
                "canvasId": "gamma",
                "canvasOrder": 2
            },
            "asset": {
                "id": "second",
                "mediaType": "image/tiff",
                "origin": "https://example.org/images/second.tiff"
            }
        }
    ]
}
```

this will create the following in DB:

| manifest_id | canvas_id | canvas_original_id                   | asset_id     | canvas_order | choice_order |
| ----------- | --------- | ------------------------------------ | ------------ | ------------ | ------------ |
| ghi3        | alpha     | `null`                               | 99/10/first  | 0            | `null`       |
| ghi3        | beta      | https://customer.example/canvas/beta | `null`       | 1            | `null`       |
| ghi3        | beta      | https://customer.example/canvas/beta | `null`       | 2            | `null`       |
| ghi3        | gamma     | `null`                               | 99/10/second | 3            | `null`       |