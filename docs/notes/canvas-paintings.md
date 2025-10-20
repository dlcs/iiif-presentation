# `CanvasPainting`

The `CanvasPainting` table in the database models contains the relationship between content resources on a Manifest.

> [!NOTE]
> These notes are from early prototyping/working through process and will likely need updated as we implement

## Logic

Below are a variety of scenarios and how they'd end up being saved in the database.

Some overall notes:
* There can be multiple `CanvasPainting` objects with the same `canvas_id` for the same `manifest_id` (e.g. if there are multiple items painted onto the same canvas)
* Unique constraints in database will ensure that we don't have duplicate canvases with duplicate order/choice/manifest etc

### POST manifest w/ external items:

- store incoming id as `canvas_original_id`
- create `canvas_id` (alphanumeric but surfaced in id) if it's not using a recognised host

### PUT manifest w/ external items:

- drive Create/Update/Delete of `CanvasPainting` based on `canvas_original_id`
- create `canvas_id` (alphanumeric but surfaced in id) if required

### POST manifest w/ assets items:

- store the incoming asset as `asset_id`
- as above re: `canvas_id`

### PUT manifest w/ assets items:

- drive Create/Update/Delete based on `asset_id` or `canvas_id`
- as above re: `canvas_id`

### POST/PUT manifest w/ mixture of assets and items

- create canvas id based on the standard asset and external items from above
- weave the created canvas painting records together based on canvas order and choice
- drive Create/Update/Delete as above
- more information on examples can be found [here](https://github.com/dlcs/iiif-presentation/blob/develop/docs/rfcs/0005-mixed-manifests.md#3-mix-of-items-only-and-paintedresources-in-single-transaction)

### POST/PUT manifest w/ matching of assets and items

- requires that the canvas id matches between items and assets
- perform validation to make sure the details match i.e.: no `body` in items and choice/canvas order
- matched items will have only an `assetId` 
- more information on examples can be found [here](https://github.com/dlcs/iiif-presentation/blob/develop/docs/rfcs/0005-mixed-manifests.md#2-ingest-assets-and-decorate-with-json-in-single-transaction)

-----------

# Example 1 - Single Canvas
```json
{
  "items": [
    {
      "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2",
      "type": "Canvas",
      "label": { "none": [ "-" ] },
      "width": 1944,
      "height": 3225,
      "items": [
        {
          "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2/painting",
          "type": "AnnotationPage",
          "items": [
            {
              "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2/painting/anno",
              "type": "Annotation",
              "motivation": "painting",
              "body": {
                "id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2/full/617,1024/0/default.jpg",
                "type": "Image",
                "width": 617,
                "height": 1024,
                "format": "image/jpeg",
                "service": [
                  {
                    "@id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2",
                    "@type": "ImageService2",
                    "profile": "http://iiif.io/api/image/2/level1.json"
                  }
                ]
              },
              "target": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2"
            }
          ]
        }
      ]
    }
  ]
}
```

| PK  | manifest_id | canvas_id | canvas_original_id                                                                     | canvas_order | choice_order |
| --- | ----------- | --------- | -------------------------------------------------------------------------------------- | ------------ | ------------ |
| 1   | abc1        | aaaaaa    | https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2 | 0            | null         |

# Example 2 - 2 basic Canvas
```json
{
  "items": [
    {
      "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2",
      "type": "Canvas",
      "label": { "none": [ "-" ] },
      "width": 1944,
      "height": 3225,
      "items": [
        {
          "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2/painting",
          "type": "AnnotationPage",
          "items": [
            {
              "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2/painting/anno",
              "type": "Annotation",
              "motivation": "painting",
              "body": {
                "id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2/full/617,1024/0/default.jpg",
                "type": "Image",
                "width": 617,
                "height": 1024,
                "format": "image/jpeg",
                "service": [
                  {
                    "@id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2",
                    "@type": "ImageService2",
                    "profile": "http://iiif.io/api/image/2/level1.json"
                  }
                ]
              },
              "target": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2"
            }
          ]
        }
      ]
    },
    {
      "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2",
      "type": "Canvas",
      "label": { "none": [ "-" ] },
      "width": 1837,
      "height": 3020,
      "items": [
        {
          "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2/painting",
          "type": "AnnotationPage",
          "items": [
            {
              "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2/painting/anno",
              "type": "Annotation",
              "motivation": "painting",
              "body": {
                "id": "https://iiif.wellcomecollection.org/image/b24861108_0002.jp2/full/617,1024/0/default.jpg",
                "type": "Image",
                "width": 617,
                "height": 1024,
                "format": "image/jpeg",
                "service": [
                  {
                    "@id": "https://iiif.wellcomecollection.org/image/b24861108_0002.jp2",
                    "@type": "ImageService2",
                    "profile": "http://iiif.io/api/image/2/level1.json"
                  }
                ]
              },
              "target": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2"
            }
          ]
        }
      ]
    }
  ]
}
```

| PK  | manifest_id | canvas_id | canvas_original_id                                                                     | canvas_order | choice_order |
| --- | ----------- | --------- | -------------------------------------------------------------------------------------- | ------------ | ------------ |
| 1   | abc1        | aaaaab    | https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2 | 0            | null         |
| 2   | abc1        | aaaaac    | https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2 | 1            | null         |

## Example 2 - 2 Canvas, 1 with choice
```json
{
  "items": [
    {
      "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2",
      "type": "Canvas",
      "label": {
        "none": [
          "-"
        ]
      },
      "width": 1944,
      "height": 3225,
      "items": [
        {
          "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2/painting",
          "type": "AnnotationPage",
          "items": [
            {
              "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2/painting/anno",
              "type": "Annotation",
              "motivation": "painting",
              "body": {
                "type": "Choice",
                "items": [
                  {
                    "id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2/full/617,1024/0/default.jpg",
                    "type": "Image",
                    "width": 617,
                    "height": 1024,
                    "format": "image/jpeg",
                    "service": [
                      {
                        "@id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2",
                        "@type": "ImageService2",
                        "profile": "http://iiif.io/api/image/2/level1.json"
                      }
                    ]
                  },
                  {
                    "id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2/full/617,1024/0/default.jpg",
                    "type": "Image",
                    "width": 617,
                    "height": 1024,
                    "format": "image/jpeg",
                    "service": [
                      {
                        "@id": "https://iiif.wellcomecollection.org/image/b24861108_0001.jp2",
                        "@type": "ImageService2",
                        "profile": "http://iiif.io/api/image/2/level1.json"
                      }
                    ]
                  }
                ]
              },
              "target": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2"
            }
          ]
        }
      ]
    },
    {
      "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2",
      "type": "Canvas",
      "label": {
        "none": [
          "-"
        ]
      },
      "width": 1837,
      "height": 3020,
      "items": [
        {
          "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2/painting",
          "type": "AnnotationPage",
          "items": [
            {
              "id": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2/painting/anno",
              "type": "Annotation",
              "motivation": "painting",
              "body": {
                "id": "https://iiif.wellcomecollection.org/image/b24861108_0002.jp2/full/617,1024/0/default.jpg",
                "type": "Image",
                "width": 617,
                "height": 1024,
                "format": "image/jpeg",
                "service": [
                  {
                    "@id": "https://iiif.wellcomecollection.org/image/b24861108_0002.jp2",
                    "@type": "ImageService2",
                    "profile": "http://iiif.io/api/image/2/level1.json"
                  }
                ]
              },
              "target": "https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2"
            }
          ]
        }
      ]
    }
  ]
}
```

| PK  | manifest_id | canvas_id | canvas_original_id                                                                     | canvas_order | choice_order |
| --- | ----------- | --------- | -------------------------------------------------------------------------------------- | ------------ | ------------ |
| 1   | abc1        | aaaaac    | https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2 | 0            | 1            |
| 2   | abc1        | aaaaac    | https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0001.jp2 | 0            | 2            |
| 3   | abc1        | aaaaad    | https://iiif.wellcomecollection.org/presentation/b24861108/canvases/b24861108_0002.jp2 | 1            | null         |

# Example 4 - Single Asset
```json
{
    "paintedResources": [
        {
            "asset": {
                "id": "one",
                "origin": "http://example.site/image.jpg",
                "mediaType": "image/jpeg"
            }
        }
    ]
}
```

| PK  | manifest_id | canvas_id | asset_id | canvas_order | choice_order |
| --- | ----------- | --------- | -------- | ------------ | ------------ |
| 1   | def1        | aaaaae    | 2/2/one  | 0            | null         |

# Example 5 - Multiple Assets
```json
{
    "paintedResources": [
        {
            "asset": {
                "id": "one",
                "origin": "http://example.site/image.jpg",
                "mediaType": "image/jpeg"
            }
        },
        {
            "asset": {
                "id": "two",
                "origin": "http://example.site/image2.jpg",
                "mediaType": "image/jpeg"
            }
        }
    ]
}
```

| PK  | manifest_id | canvas_id | asset_id | canvas_order | choice_order |
| --- | ----------- | --------- | -------- | ------------ | ------------ |
| 1   | def1        | aaaaae    | 2/2/one  | 0            | null         |
| 2   | def1        | aaaaaf    | 2/2/two  | 1            | null         |

# Example 5 - Multiple Assets with Choices
```json
{
    "paintedResources": [
        {
            "asset": {
                "id": "1b",
                "origin": "http://example.site/image.jpg",
                "mediaType": "image/jpeg"
            },
            "canvasPainting": {
                "canvasOrder": 1,
                "choiceOrder": 2
            }
        },
        {
            "asset": {
                "id": "1a",
                "origin": "http://example.site/image2.jpg",
                "mediaType": "image/jpeg"
            },
            "canvasPainting": {
                "canvasOrder": 1,
                "choiceOrder": 1
            }
        },
        {
            "asset": {
                "id": "2",
                "origin": "http://example.site/image3.jpg",
                "mediaType": "image/jpeg"
            },
            "canvasPainting": {
                "canvasOrder": 2
            }
        }
    ]
}
```

| PK  | manifest_id | canvas_id | asset_id | canvas_order | choice_order |
| --- | ----------- | --------- | -------- | ------------ | ------------ |
| 1   | def1        | aaaaag    | 2/2/1a   | 0            | 1            |
| 2   | def1        | aaaaag    | 2/2/1b   | 0            | 2            |
| 3   | def1        | aaaaah    | 2/2/2    | 1            | null         |

# Example 6 - Mixed Manifest

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
            "id": "alpha",
            "type": "Canvas",
            "rendering": [
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
            "seeAlso": [
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
        },
        {
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
                                "width": 120
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

# Example 7 - Matched Manifest

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
            "id": "alpha", 
            "type": "Canvas",
            "rendering": [ 
                { 
                    "id": "https://customer.example/svg/first",
                    "type": "Image",
                    "label": { "en": ["SVG XML for page text"] },
                    "format": "image/svg+xml"
                }
            ],
            "seeAlso": [
                { 
                    "id": "https://customer.example/text/alto/alpha",
                    "type": "Dataset",
                    "profile": "http://www.loc.gov/standards/alto/v3/alto.xsd",
                    "label": { "none": ["METS-ALTO XML"] },
                    "format": "application/xml+alto"
                }
            ]
        },
        {
            "id": "beta",
            "type": "Canvas",
            "metadata": [
                { 
                    "label": { "en": ["Something"] },
                    "value": { "en": ["Another thing"] }
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

| manifest_id | canvas_id | canvas_original_id | asset_id     | canvas_order | choice_order |
| ----------- | --------- | ------------------ | ------------ | ------------ | ------------ |
| def2        | alpha     | `null`             | 99/10/first  | 0            | `null`       |
| def2        | beta      | `null`             | 99/10/second | 1            | `null`       |
