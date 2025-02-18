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
- create `canvas_id` (alphanumeric but surfaced in id)

### PUT manifest w/ external items:

- drive Create/Update/Delete of `CanvasPainting` based on `canvas_original_id`
- create `canvas_id` (alphanumeric but surfaced in id) if required

### POST manifest w/ assets items:

- store the incoming asset as `asset_id`
- as above re: `canvas_id`

### PUT manifest w/ assets items:

- drive Create/Update/Delete based on `asset_id` or `canvas_id`
- as above re: `canvas_id`

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