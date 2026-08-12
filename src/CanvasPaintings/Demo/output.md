# Cookbook recipes


## Simple Manifest - Image
https://iiif.io/api/cookbook/recipe/0001-mvm-image/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                             | ChoiceOrder | ExternalAssetId                                                               | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------ | ----------- | ----------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | gmzvbvev |             | 0           | https://iiif.io/api/cookbook/recipe/0001-mvm-image/canvas/p1 |             | http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png |       | b49x77xs   | 1800         | 1200        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/b49x77xs",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "gmzvbvev",
        "canvasOrder": 0,
        "externalAssetId": "http://iiif.io/api/presentation/2.1/example/fixtures/resources/page1-full.png"
      }
    }
  ]
}
```




## Simple Manifest - Audio
https://iiif.io/api/cookbook/recipe/0002-mvm-audio/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                          | ChoiceOrder | ExternalAssetId                                                                 | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | meb983yx |             | 0           | https://iiif.io/api/cookbook/recipe/0002-mvm-audio/canvas |             | https://fixtures.iiif.io/audio/indiana/mahler-symphony-3/CD1/medium/128Kbps.mp4 |       | uakqs4gg   |              |             |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/uakqs4gg",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "meb983yx",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/audio/indiana/mahler-symphony-3/CD1/medium/128Kbps.mp4"
      }
    }
  ]
}
```




## Simple Manifest - Video
https://iiif.io/api/cookbook/recipe/0003-mvm-video/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                          | ChoiceOrder | ExternalAssetId                                                                            | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------ | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | s6p6vug7 |             | 0           | https://iiif.io/api/cookbook/recipe/0003-mvm-video/canvas |             | https://fixtures.iiif.io/video/indiana/lunchroom_manners/high/lunchroom_manners_1024kb.mp4 |       | hd6uqysj   | 360          | 480         |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/hd6uqysj",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "s6p6vug7",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/lunchroom_manners/high/lunchroom_manners_1024kb.mp4"
      }
    }
  ]
}
```




## Image and Canvas with Differing Dimensions
https://iiif.io/api/cookbook/recipe/0004-canvas-size/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                               | ChoiceOrder | ExternalAssetId                                                            | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | -------------------------------------------------------------- | ----------- | -------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | yzj3fpjq |             | 0           | https://iiif.io/api/cookbook/recipe/0004-canvas-size/canvas/p1 |             | https://fixtures.iiif.io/video/indiana/donizetti-elixir/act1-thumbnail.png |       | w44jscje   | 360          | 640         |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/w44jscje",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "yzj3fpjq",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/donizetti-elixir/act1-thumbnail.png"
      }
    }
  ]
}
```




## Support Deep Viewing with Basic Use of a IIIF Image Service
https://iiif.io/api/cookbook/recipe/0005-image-service/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                 | ChoiceOrder | ExternalAssetId                                                                                                   | Label                           | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ------------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | bxqsv2t4 |             | 0           | https://iiif.io/api/cookbook/recipe/0005-image-service/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg | Canvas with a single IIIF image | xcahk3zw   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/xcahk3zw",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "bxqsv2t4",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Canvas with a single IIIF image"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Internationalization and Multi-language Values
https://iiif.io/api/cookbook/recipe/0006-text-language/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                 | ChoiceOrder | ExternalAssetId                                                                                                          | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------ | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | vn5fvuxt |             | 0           | https://iiif.io/api/cookbook/recipe/0006-text-language/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/329817fc8a251a01c393f517d8a17d87-Whistlers_Mother/full/max/0/default.jpg |       | cqtvesc7   | 991          | 1114        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/cqtvesc7",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "vn5fvuxt",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/329817fc8a251a01c393f517d8a17d87-Whistlers_Mother/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Displaying Multiple Values with Language Maps
https://iiif.io/api/cookbook/recipe/0118-multivalue/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                             | ChoiceOrder | ExternalAssetId                                                                                                              | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------ | ----------- | ---------------------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | qn5rbgfg |             | 0           | https://iiif.io/api/cookbook/recipe/0118-multivalue/canvas/1 |             | https://upload.wikimedia.org/wikipedia/commons/thumb/1/1b/Whistlers_Mother_high_res.jpg/1114px-Whistlers_Mother_high_res.jpg |       | p7p76pwx   |              |             |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/p7p76pwx",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "qn5rbgfg",
        "canvasOrder": 0,
        "externalAssetId": "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1b/Whistlers_Mother_high_res.jpg/1114px-Whistlers_Mother_high_res.jpg"
      }
    }
  ]
}
```




## Metadata on any Resource
https://iiif.io/api/cookbook/recipe/0029-metadata-anywhere/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                     | ChoiceOrder | ExternalAssetId                                                                                                     | Label                        | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | -------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------- | ---------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | wwrqqyjm |             | 0           | https://iiif.io/api/cookbook/recipe/0029-metadata-anywhere/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg | Painting under natural light | cf9hua55   | 1271         | 2000        |        |           |
|         | j9w2qdv6 |             | 1           | https://iiif.io/api/cookbook/recipe/0029-metadata-anywhere/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/max/0/default.jpg    | X-ray view of painting       | cf9hua55   | 1271         | 2000        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/cf9hua55",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "wwrqqyjm",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Painting under natural light"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "j9w2qdv6",
        "canvasOrder": 1,
        "label": {
          "en": [
            "X-ray view of painting"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Linking to Structured Metadata
https://iiif.io/api/cookbook/recipe/0053-seeAlso/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                           | ChoiceOrder | ExternalAssetId                                                                                                                      | Label       | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------ | ----------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | b65g4dka |             | 0           | https://iiif.io/api/cookbook/recipe/0053-seeAlso/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001/full/max/0/default.jpg | front cover | mx79qyfq   | 4823         | 3497        |        |           |
|         | qrd7btgd |             | 1           | https://iiif.io/api/cookbook/recipe/0053-seeAlso/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_002/full/max/0/default.jpg | pages 1–2   | mx79qyfq   | 4804         | 6062        |        |           |
|         | m8kw5k2p |             | 2           | https://iiif.io/api/cookbook/recipe/0053-seeAlso/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_003/full/max/0/default.jpg | pages 3–4   | mx79qyfq   | 4776         | 6127        |        |           |
|         | hhjzyj9r |             | 3           | https://iiif.io/api/cookbook/recipe/0053-seeAlso/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_004/full/max/0/default.jpg | pages 5–6   | mx79qyfq   | 4751         | 6124        |        |           |
|         | gwqxjmb3 |             | 4           | https://iiif.io/api/cookbook/recipe/0053-seeAlso/canvas/p5 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_005/full/max/0/default.jpg | back cover  | mx79qyfq   | 4808         | 3510        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/mx79qyfq",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "b65g4dka",
        "canvasOrder": 0,
        "label": {
          "en": [
            "front cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "qrd7btgd",
        "canvasOrder": 1,
        "label": {
          "en": [
            "pages 1\u20132"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_002/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "m8kw5k2p",
        "canvasOrder": 2,
        "label": {
          "en": [
            "pages 3\u20134"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_003/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "hhjzyj9r",
        "canvasOrder": 3,
        "label": {
          "en": [
            "pages 5\u20136"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_004/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "gwqxjmb3",
        "canvasOrder": 4,
        "label": {
          "en": [
            "back cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_005/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Providing Alternative Representations
https://iiif.io/api/cookbook/recipe/0046-rendering/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                             | ChoiceOrder | ExternalAssetId                                                                                                                      | Label       | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------ | ----------- | ------------------------------------------------------------------------------------------------------------------------------------ | ----------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | s2apt3xc |             | 0           | https://iiif.io/api/cookbook/recipe/0046-rendering/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001/full/max/0/default.jpg | front cover | zvpzxv96   | 4823         | 3497        |        |           |
|         | pwy8hjhp |             | 1           | https://iiif.io/api/cookbook/recipe/0046-rendering/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_002/full/max/0/default.jpg | pages 1–2   | zvpzxv96   | 4804         | 6062        |        |           |
|         | ms7yxdjq |             | 2           | https://iiif.io/api/cookbook/recipe/0046-rendering/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_003/full/max/0/default.jpg | pages 3–4   | zvpzxv96   | 4776         | 6127        |        |           |
|         | kypz577q |             | 3           | https://iiif.io/api/cookbook/recipe/0046-rendering/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_004/full/max/0/default.jpg | pages 5–6   | zvpzxv96   | 4751         | 6124        |        |           |
|         | c497xbam |             | 4           | https://iiif.io/api/cookbook/recipe/0046-rendering/canvas/p5 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_005/full/max/0/default.jpg | back cover  | zvpzxv96   | 4808         | 3510        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/zvpzxv96",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "s2apt3xc",
        "canvasOrder": 0,
        "label": {
          "en": [
            "front cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "pwy8hjhp",
        "canvasOrder": 1,
        "label": {
          "en": [
            "pages 1\u20132"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_002/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "ms7yxdjq",
        "canvasOrder": 2,
        "label": {
          "en": [
            "pages 3\u20134"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_003/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "kypz577q",
        "canvasOrder": 3,
        "label": {
          "en": [
            "pages 5\u20136"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_004/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "c497xbam",
        "canvasOrder": 4,
        "label": {
          "en": [
            "back cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_005/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Image in Annotations
https://iiif.io/api/cookbook/recipe/0377-image-in-annotation/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                      | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | uzqm3m7b |             | 0           | https://iiif.io/api/cookbook/recipe/0377-image-in-annotation/canvas-1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | q5xq48dh   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/q5xq48dh",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "uzqm3m7b",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Begin playback at a specific point - Time-based media
https://iiif.io/api/cookbook/recipe/0015-start/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                               | ChoiceOrder | ExternalAssetId                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | -------------------------------------------------------------- | ----------- | --------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | ga7678gj |             | 0           | https://iiif.io/api/cookbook/recipe/0015-start/canvas/segment1 |             | https://fixtures.iiif.io/video/indiana/30-minute-clock/medium/30-minute-clock.mp4 |       | auczxcxz   |              |             |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/auczxcxz",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "ga7678gj",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/30-minute-clock/medium/30-minute-clock.mp4"
      }
    }
  ]
}
```




## Audio Presentation with Accompanying Image
https://iiif.io/api/cookbook/recipe/0014-accompanyingcanvas/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                      | ChoiceOrder | ExternalAssetId                                                                 | Label                               | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------- | ----------------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | pcdkxs9a |             | 0           | https://iiif.io/api/cookbook/recipe/0014-accompanyingcanvas/canvas/p1 |             | https://fixtures.iiif.io/audio/indiana/mahler-symphony-3/CD1/medium/128Kbps.mp4 | Gustav Mahler, Symphony No. 3, CD 1 | pt5paz78   |              |             |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/pt5paz78",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "pcdkxs9a",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Gustav Mahler, Symphony No. 3, CD 1"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/audio/indiana/mahler-symphony-3/CD1/medium/128Kbps.mp4"
      }
    }
  ]
}
```




## Simplest Annotation
https://iiif.io/api/cookbook/recipe/0266-full-canvas-annotation/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                         | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------ | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | p6ns44xc |             | 0           | https://iiif.io/api/cookbook/recipe/0266-full-canvas-annotation/canvas-1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | ug322g6r   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/ug322g6r",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "p6ns44xc",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Using Caption and Subtitle Files in Multiple Languages with Video Content
https://iiif.io/api/cookbook/recipe/0074-multiple-language-captions/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                           | ChoiceOrder | ExternalAssetId                                                               | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | -------------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | gg54hhta |             | 0           | https://iiif.io/api/cookbook/recipe/0074-multiple-language-captions/canvas |             | https://fixtures.iiif.io/video/europeana/Per_voi_signore_Modelli_francesi.mp4 |       | uwaqg2nf   | 384          | 288         |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/uwaqg2nf",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "gg54hhta",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/europeana/Per_voi_signore_Modelli_francesi.mp4"
      }
    }
  ]
}
```




## Addressing a Spatial Region
https://iiif.io/api/cookbook/recipe/0299-region/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                          | ChoiceOrder | ExternalAssetId                                                                                                      | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | a9f2gksd |             | 0           | https://iiif.io/api/cookbook/recipe/0299-region/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/4ce82cef49fb16798f4c2440307c3d6f-newspaper-p2/full/max/0/default.jpg |       | g26xea2t   | 4999         | 3536        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/g26xea2t",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "a9f2gksd",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4ce82cef49fb16798f4c2440307c3d6f-newspaper-p2/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Multiple Volumes in a Single Bound Volume
https://iiif.io/api/cookbook/recipe/0031-bound-multivolume/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                     | ChoiceOrder | ExternalAssetId                                                                                                            | Label                     | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | -------------------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------- | ------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | yp8qz3m2 |             | 0           | https://iiif.io/api/cookbook/recipe/0031-bound-multivolume/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-1_frontcover/full/max/0/default.jpg       | Front cover               | qhwpycaj   | 7230         | 5428        |        |           |
|         | n5dgvy85 |             | 1           | https://iiif.io/api/cookbook/recipe/0031-bound-multivolume/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-2_insidefrontcover/full/max/0/default.jpg | Inside front cover        | qhwpycaj   | 7230         | 5428        |        |           |
|         | x94sc4hd |             | 2           | https://iiif.io/api/cookbook/recipe/0031-bound-multivolume/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-3_titlepage1/full/max/0/default.jpg       | Vol. 1 title page         | qhwpycaj   | 7230         | 5428        |        |           |
|         | cbzydye3 |             | 3           | https://iiif.io/api/cookbook/recipe/0031-bound-multivolume/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-4_titlepage1_verso/full/max/0/default.jpg | Vol. 1 title page (verso) | qhwpycaj   | 7230         | 5428        |        |           |
|         | rjpde5ez |             | 4           | https://iiif.io/api/cookbook/recipe/0031-bound-multivolume/canvas/p5 |             | https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-5_titlepage2/max/full/0/default.jpg       | Vol. 2 title page         | qhwpycaj   | 7230         | 5428        |        |           |
|         | jvpnvqqn |             | 5           | https://iiif.io/api/cookbook/recipe/0031-bound-multivolume/canvas/p6 |             | https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-6_titlepage2_verso/max/full/0/default.jpg | Vol. 2 title page (verso) | qhwpycaj   | 7230         | 5428        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/qhwpycaj",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "yp8qz3m2",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Front cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-1_frontcover/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "n5dgvy85",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Inside front cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-2_insidefrontcover/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "x94sc4hd",
        "canvasOrder": 2,
        "label": {
          "en": [
            "Vol. 1 title page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-3_titlepage1/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "cbzydye3",
        "canvasOrder": 3,
        "label": {
          "en": [
            "Vol. 1 title page (verso)"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-4_titlepage1_verso/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "rjpde5ez",
        "canvasOrder": 4,
        "label": {
          "en": [
            "Vol. 2 title page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-5_titlepage2/max/full/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "jvpnvqqn",
        "canvasOrder": 5,
        "label": {
          "en": [
            "Vol. 2 title page (verso)"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/15f769d62ca9a3a2deca390efed75d73-6_titlepage2_verso/max/full/0/default.jpg"
      }
    }
  ]
}
```




## Locate a Manifest on a Web Map
https://iiif.io/api/cookbook/recipe/0154-geo-extension/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                | ChoiceOrder | ExternalAssetId                                                                                                 | Label           | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------- | ----------- | --------------------------------------------------------------------------------------------------------------- | --------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | q2vea8az |             | 0           | https://iiif.io/api/cookbook/recipe/0154-geo-extension/canvas/1 |             | https://iiif.io/api/image/3.0/example/reference/28473c77da3deebe4375c3a50572d9d3-laocoon/full/max/0/default.jpg | Front of Bronze | jqgh6yeu   | 3000         | 2315        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/jqgh6yeu",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "q2vea8az",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Front of Bronze"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/28473c77da3deebe4375c3a50572d9d3-laocoon/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Composition from Multiple Images
https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel                                                              | CanvasOrder | CanvasOriginalId                                                                    | ChoiceOrder | ExternalAssetId                                                                                                                   | Label                                                                    | ManifestId | StaticHeight | StaticWidth | Target                                                                                                      | Thumbnail |
| ------- | -------- | ------------------------------------------------------------------------ | ----------- | ----------------------------------------------------------------------------------- | ----------- | --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ | ---------- | ------------ | ----------- | ----------------------------------------------------------------------------------------------------------- | --------- |
|         | ay3cftyt |                                                                          | 0           | https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux/full/max/0/default.jpg           | f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné] | kn7dz8hb   | 5412         | 7216        |                                                                                                             |           |
|         | ay3cftyt | f. 033v-034r [Chilpéric Ier tue Galswinthe, se remarie et est assassiné] | 1           | https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux_miniature/full/max/0/default.jpg | Miniature [Chilpéric Ier tue Galswinthe, se remarie et est assassiné]    | kn7dz8hb   | 2414         | 2138        | https://iiif.io/api/cookbook/recipe/0036-composition-from-multiple-images/canvas/p1#xywh=3949,994,1091,1232 |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/kn7dz8hb",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "ay3cftyt",
        "canvasOrder": 0,
        "label": {
          "none": [
            "f. 033v-034r [Chilp\u00E9ric Ier tue Galswinthe, se remarie et est assassin\u00E9]"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "ay3cftyt",
        "canvasOrder": 1,
        "label": {
          "fr": [
            "Miniature [Chilp\u00E9ric Ier tue Galswinthe, se remarie et est assassin\u00E9]"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/899da506920824588764bc12b10fc800-bnf_chateauroux_miniature/full/max/0/default.jpg",
        "canvasLabel": {
          "none": [
            "f. 033v-034r [Chilp\u00E9ric Ier tue Galswinthe, se remarie et est assassin\u00E9]"
          ]
        }
      }
    }
  ]
}
```




## Providing Access to Transcript Files of A/V Content
https://iiif.io/api/cookbook/recipe/0017-transcription-av/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                 | ChoiceOrder | ExternalAssetId                                                                | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------ | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | kxkq4wmy |             | 0           | https://iiif.io/api/cookbook/recipe/0017-transcription-av/canvas |             | https://fixtures.iiif.io/video/indiana/volleyball/high/volleyball-for-boys.mp4 |       | srrcvy3f   | 1080         | 1920        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/srrcvy3f",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "kxkq4wmy",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/volleyball/high/volleyball-for-boys.mp4"
      }
    }
  ]
}
```




## Table of Contents for Multiple A/V Files on Multiple Canvases
https://iiif.io/api/cookbook/recipe/0065-opera-multiple-canvases/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                          | ChoiceOrder | ExternalAssetId                                                                          | Label        | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------- | ------------ | ---------- | ------------ | ----------- | ------ | --------- |
|         | wd7bg89x |             | 0           | https://iiif.io/api/cookbook/recipe/0065-opera-multiple-canvases/canvas/1 |             | https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_1.mp4 | Atto Primo   | dygj3mf5   | 1080         | 1920        |        |           |
|         | zwdhf69t |             | 1           | https://iiif.io/api/cookbook/recipe/0065-opera-multiple-canvases/canvas/2 |             | https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_2.mp4 | Atto Secondo | dygj3mf5   | 1080         | 1920        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/dygj3mf5",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "wd7bg89x",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Atto Primo"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_1.mp4"
      }
    },
    {
      "canvasPainting": {
        "canvas": "zwdhf69t",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Atto Secondo"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_2.mp4"
      }
    }
  ]
}
```




## Table of Contents for A/V Content
https://iiif.io/api/cookbook/recipe/0026-toc-opera/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                            | ChoiceOrder | ExternalAssetId                                                                    | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ----------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | dgz862cu |             | 0           | https://iiif.io/api/cookbook/recipe/0026-toc-opera/canvas/1 |             | https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low.mp4 |       | gay322wf   | 1080         | 1920        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/gay322wf",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "dgz862cu",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low.mp4"
      }
    }
  ]
}
```




## Table of Contents for Book Chapters
https://iiif.io/api/cookbook/recipe/0024-book-4-toc/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                              | ChoiceOrder | ExternalAssetId                                                                                                                          | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | hj89cgr6 |             | 0           | https://iiif.io/api/cookbook/recipe/0024-book-4-toc/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-1-21198-zz001d8m41_774608_master/full/max/0/default.jpg | f. 1r | vau8dhp9   | 2504         | 1768        |        |           |
|         | cbktaf55 |             | 1           | https://iiif.io/api/cookbook/recipe/0024-book-4-toc/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-2-21198-zz001d8m5j_774612_master/full/max/0/default.jpg | f. 1v | vau8dhp9   | 2512         | 1792        |        |           |
|         | w6ww5syy |             | 2           | https://iiif.io/api/cookbook/recipe/0024-book-4-toc/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-3-21198-zz001d8tm5_775004_master/full/max/0/default.jpg | f. 2r | vau8dhp9   | 2456         | 1792        |        |           |
|         | x8fr2kd9 |             | 3           | https://iiif.io/api/cookbook/recipe/0024-book-4-toc/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-4-21198-zz001d8tnp_775007_master/full/max/0/default.jpg | f. 2v | vau8dhp9   | 2440         | 1760        |        |           |
|         | cbhugcs8 |             | 4           | https://iiif.io/api/cookbook/recipe/0024-book-4-toc/canvas/p5 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-5-21198-zz001d8v6f_775077_master/full/max/0/default.jpg | f. 3r | vau8dhp9   | 2416         | 1776        |        |           |
|         | q5mgq35y |             | 5           | https://iiif.io/api/cookbook/recipe/0024-book-4-toc/canvas/p6 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-6-21198-zz001d8v7z_775085_master/full/max/0/default.jpg | f. 3v | vau8dhp9   | 2416         | 1776        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/vau8dhp9",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "hj89cgr6",
        "canvasOrder": 0,
        "label": {
          "en": [
            "f. 1r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-1-21198-zz001d8m41_774608_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "cbktaf55",
        "canvasOrder": 1,
        "label": {
          "en": [
            "f. 1v"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-2-21198-zz001d8m5j_774612_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "w6ww5syy",
        "canvasOrder": 2,
        "label": {
          "en": [
            "f. 2r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-3-21198-zz001d8tm5_775004_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "x8fr2kd9",
        "canvasOrder": 3,
        "label": {
          "en": [
            "f. 2v"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-4-21198-zz001d8tnp_775007_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "cbhugcs8",
        "canvasOrder": 4,
        "label": {
          "en": [
            "f. 3r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-5-21198-zz001d8v6f_775077_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "q5mgq35y",
        "canvasOrder": 5,
        "label": {
          "en": [
            "f. 3v"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-6-21198-zz001d8v7z_775085_master/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Locate Multiple Canvases on a Web Map
https://iiif.io/api/cookbook/recipe/0240-navPlace-on-canvases/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                       | ChoiceOrder | ExternalAssetId                                                                                                           | Label           | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------- | --------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | gcrg4ufr |             | 0           | https://iiif.io/api/cookbook/recipe/0240-navPlace-on-canvases/canvas/1 |             | https://iiif.io/api/image/3.0/example/reference/28473c77da3deebe4375c3a50572d9d3-laocoon/full/max/0/default.jpg           | Front of Bronze | z34wurvx   | 3000         | 2315        |        |           |
|         | wjw4bkxb |             | 1           | https://iiif.io/api/cookbook/recipe/0240-navPlace-on-canvases/canvas/2 |             | https://iiif.io/api/image/3.0/example/reference/58763298b61c2a99f78ff94d8364c639-laocoon_1946_18_1/full/max/0/default.jpg | Painting        | z34wurvx   | 3259         | 4096        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/z34wurvx",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "gcrg4ufr",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Front of Bronze"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/28473c77da3deebe4375c3a50572d9d3-laocoon/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "wjw4bkxb",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Painting"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/58763298b61c2a99f78ff94d8364c639-laocoon_1946_18_1/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Simple Annotation — Tagging
https://iiif.io/api/cookbook/recipe/0021-tagging/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                           | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | zwex88nk |             | 0           | https://iiif.io/api/cookbook/recipe/0021-tagging/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | n54p5584   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/n54p5584",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "zwex88nk",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Annotating a specific point of an image
https://iiif.io/api/cookbook/recipe/0135-annotating-point-in-canvas/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                                | ChoiceOrder | ExternalAssetId                                                                                                  | Label                              | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------- | ---------------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | q9j9ryx6 |             | 0           | https://iiif.io/api/cookbook/recipe/0135-annotating-point-in-canvas/canvas.json |             | https://iiif.io/api/image/3.0/example/reference/43153e2ec7531f14dd1c9b2fc401678a-88695674/full/max/0/default.jpg | Chesapeake and Ohio Canal Pamphlet | rdqkk5yk   | 7072         | 5212        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/rdqkk5yk",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "q9j9ryx6",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Chesapeake and Ohio Canal Pamphlet"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/43153e2ec7531f14dd1c9b2fc401678a-88695674/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Embedded or referenced Annotations
https://iiif.io/api/cookbook/recipe/0269-embedded-or-referenced-annotations/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                                     | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------------------ | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | sctgj4m6 |             | 0           | https://iiif.io/api/cookbook/recipe/0269-embedded-or-referenced-annotations/canvas-1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | eqkaqy6g   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/eqkaqy6g",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "sctgj4m6",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Missing Images in a Sequence
https://iiif.io/api/cookbook/recipe/0283-missing-image/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                 | ChoiceOrder | ExternalAssetId                                                                                                                          | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | t5jqj6xq |             | 0           | https://iiif.io/api/cookbook/recipe/0283-missing-image/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-1-21198-zz001d8m41_774608_master/full/max/0/default.jpg | f. 1r | e4zn99ax   | 2504         | 1768        |        |           |
|         | pz9dyasb |             | 1           | https://iiif.io/api/cookbook/recipe/0283-missing-image/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-3-21198-zz001d8tm5_775004_master/full/max/0/default.jpg | f. 2r | e4zn99ax   | 2456         | 1792        |        |           |
|         | mpzkpg7b |             | 2           | https://iiif.io/api/cookbook/recipe/0283-missing-image/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-4-21198-zz001d8tnp_775007_master/full/max/0/default.jpg | f. 2v | e4zn99ax   | 2440         | 1760        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/e4zn99ax",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "t5jqj6xq",
        "canvasOrder": 0,
        "label": {
          "en": [
            "f. 1r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-1-21198-zz001d8m41_774608_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "pz9dyasb",
        "canvasOrder": 1,
        "label": {
          "en": [
            "f. 2r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-3-21198-zz001d8tm5_775004_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "mpzkpg7b",
        "canvasOrder": 2,
        "label": {
          "en": [
            "f. 2v"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/d3bbf5397c6df6b894c5991195c912ab-4-21198-zz001d8tnp_775007_master/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Load a Preview Image Before the Main Content
https://iiif.io/api/cookbook/recipe/0013-placeholderCanvas/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                            | ChoiceOrder | ExternalAssetId                                                                    | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | k5xffet6 |             | 0           | https://iiif.io/api/cookbook/recipe/0013-placeholderCanvas/canvas/donizetti |             | https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low.mp4 |       | x3bqymu2   | 360          | 640         |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/x3bqymu2",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "k5xffet6",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low.mp4"
      }
    }
  ]
}
```




## Simple Manifest - Book
https://iiif.io/api/cookbook/recipe/0009-book-1/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                          | ChoiceOrder | ExternalAssetId                                                                                                                             | Label        | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------- | ------------ | ---------- | ------------ | ----------- | ------ | --------- |
|         | k9dmdamz |             | 0           | https://iiif.io/api/cookbook/recipe/0009-book-1/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f18/full/max/0/default.jpg | Blank page   | n5pebw5j   | 4613         | 3204        |        |           |
|         | h3tv22ua |             | 1           | https://iiif.io/api/cookbook/recipe/0009-book-1/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f19/full/max/0/default.jpg | Frontispiece | n5pebw5j   | 4612         | 3186        |        |           |
|         | gqkgakev |             | 2           | https://iiif.io/api/cookbook/recipe/0009-book-1/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f20/full/max/0/default.jpg | Title page   | n5pebw5j   | 4613         | 3204        |        |           |
|         | y5ak58c2 |             | 3           | https://iiif.io/api/cookbook/recipe/0009-book-1/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f21/full/max/0/default.jpg | Blank page   | n5pebw5j   | 4578         | 3174        |        |           |
|         | c7b7u2sr |             | 4           | https://iiif.io/api/cookbook/recipe/0009-book-1/canvas/p5 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f22/full/max/0/default.jpg | Bookplate    | n5pebw5j   | 4632         | 3198        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/n5pebw5j",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "k9dmdamz",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Blank page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f18/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "h3tv22ua",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Frontispiece"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f19/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "gqkgakev",
        "canvasOrder": 2,
        "label": {
          "en": [
            "Title page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f20/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "y5ak58c2",
        "canvasOrder": 3,
        "label": {
          "en": [
            "Blank page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f21/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "c7b7u2sr",
        "canvasOrder": 4,
        "label": {
          "en": [
            "Bookplate"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f22/full/max/0/default.jpg"
      }
    }
  ]
}
```




## HTML in Annotations
https://iiif.io/api/cookbook/recipe/0019-html-in-annotations/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                      | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | ukmuwnt4 |             | 0           | https://iiif.io/api/cookbook/recipe/0019-html-in-annotations/canvas-1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | fkehde4t   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/fkehde4t",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "ukmuwnt4",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Linking to Web Page of an Object
https://iiif.io/api/cookbook/recipe/0047-homepage/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                           | ChoiceOrder | ExternalAssetId                                                                                                      | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | rr4zcwzw |             | 0           | https://iiif.io/api/cookbook/recipe/0047-homepage/canvas/1 |             | https://iiif.io/api/image/3.0/example/reference/28473c77da3deebe4375c3a50572d9d3-laocoon/full/!500,500/0/default.jpg | Front | ax2g3wge   | 3000         | 2315        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/ax2g3wge",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "rr4zcwzw",
        "canvasOrder": 0,
        "label": {
          "none": [
            "Front"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/28473c77da3deebe4375c3a50572d9d3-laocoon/full/!500,500/0/default.jpg"
      }
    }
  ]
}
```




## Rights statement
https://iiif.io/api/cookbook/recipe/0008-rights/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                          | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | upz2j3ce |             | 0           | https://iiif.io/api/cookbook/recipe/0008-rights/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | k5swf7mg   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/k5swf7mg",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "upz2j3ce",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Acknowledge Content Contributors
https://iiif.io/api/cookbook/recipe/0234-provider/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                            | ChoiceOrder | ExternalAssetId                                                                                                                           | Label                      | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ----------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | phbret57 |             | 0           | https://iiif.io/api/cookbook/recipe/0234-provider/canvas/p0 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001_full/full/max/0/default.jpg | front cover with color bar | zu9vtszz   | 5312         | 4520        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/zu9vtszz",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "phbret57",
        "canvasOrder": 0,
        "label": {
          "en": [
            "front cover with color bar"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001_full/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Redirecting from one Canvas to another resource (Hotspot linking)
https://iiif.io/api/cookbook/recipe/0022-linking-with-a-hotspot/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                          | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | jfbx5cgw |             | 0           | https://iiif.io/api/cookbook/recipe/0022-linking-with-a-hotspot/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | ugpjgndv   | 3024         | 4032        |        |           |
|         | uv6zdbk7 |             | 1           | https://iiif.io/api/cookbook/recipe/0022-linking-with-a-hotspot/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-fountain/full/max/0/default.jpg  |       | ugpjgndv   | 4032         | 3024        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/ugpjgndv",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "jfbx5cgw",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "uv6zdbk7",
        "canvasOrder": 1,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-fountain/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Table of Contents for Multiple A/V Files on a Single Canvas
https://iiif.io/api/cookbook/recipe/0064-opera-one-canvas/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                   | ChoiceOrder | ExternalAssetId                                                                          | Label | ManifestId | StaticHeight | StaticWidth | Target                                                                         | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------ | ----------- | ---------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------------------------------------------------------------------------------ | --------- |
|         | neezc3y4 |             | 0           | https://iiif.io/api/cookbook/recipe/0064-opera-one-canvas/canvas/1 |             | https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_1.mp4 |       | j8ndptxe   | 1080         | 1920        | https://iiif.io/api/cookbook/recipe/0064-opera-one-canvas/canvas/1#t=0,3971.24 |           |
|         | neezc3y4 |             | 1           | https://iiif.io/api/cookbook/recipe/0064-opera-one-canvas/canvas/1 |             | https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_2.mp4 |       | j8ndptxe   | 1080         | 1920        | https://iiif.io/api/cookbook/recipe/0064-opera-one-canvas/canvas/1#t=3971.24   |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/j8ndptxe",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "neezc3y4",
        "canvasOrder": 0,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_1.mp4"
      }
    },
    {
      "canvasPainting": {
        "canvas": "neezc3y4",
        "canvasOrder": 1,
        "externalAssetId": "https://fixtures.iiif.io/video/indiana/donizetti-elixir/vae0637_accessH264_low_act_2.mp4"
      }
    }
  ]
}
```




## Multiple Choice of Images in a Single View (Canvas)
https://iiif.io/api/cookbook/recipe/0033-choice/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                          | ChoiceOrder | ExternalAssetId                                                                                                     | Label         | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------- | ------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | aknd24ma |             | 0           | https://iiif.io/api/cookbook/recipe/0033-choice/canvas/p1 | 1           | https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg | Natural Light | t2vwdmbn   | 1271         | 2000        |        |           |
|         | aknd24ma |             | 0           | https://iiif.io/api/cookbook/recipe/0033-choice/canvas/p1 | 2           | https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/max/0/default.jpg    | X-Ray         | t2vwdmbn   | 1271         | 2000        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/t2vwdmbn",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "aknd24ma",
        "canvasOrder": 0,
        "choiceOrder": 1,
        "label": {
          "en": [
            "Natural Light"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "aknd24ma",
        "canvasOrder": 0,
        "choiceOrder": 2,
        "label": {
          "en": [
            "X-Ray"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Foldouts, Flaps, and Maps
https://iiif.io/api/cookbook/recipe/0035-foldouts/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                           | ChoiceOrder | ExternalAssetId                                                                                                            | Label                   | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------- | ----------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | bmpq42h4 |             | 0           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/1 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-1_frontcover/full/max/0/default.jpg       | Front cover             | b873x2qy   | 4429         | 2533        |        |           |
|         | cs7fkget |             | 1           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/2 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-2_insidefrontcover/full/max/0/default.jpg | Inside front cover      | b873x2qy   | 4315         | 2490        |        |           |
|         | gjhhas76 |             | 2           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/3 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-3_foldout-folded/full/max/0/default.jpg   | Foldout, folded         | b873x2qy   | 4278         | 2197        |        |           |
|         | r7yppr8s |             | 3           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/4 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-4_foldout/full/max/0/default.jpg          | Foldout, unfolded       | b873x2qy   | 1968         | 3688        |        |           |
|         | hjcwsq3n |             | 4           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/5 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-3_foldout-rotated/full/max/0/default.jpg  | Foldout, folded (recto) | b873x2qy   | 1968         | 3688        |        |           |
|         | x3fdfxn4 |             | 5           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/6 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-5_titlepage/full/max/0/default.jpg        | Title page              | b873x2qy   | 4315         | 2490        |        |           |
|         | tvm9kmts |             | 6           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/7 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-6_titlepage-recto/full/max/0/default.jpg  | Back of title page      | b873x2qy   | 4315         | 2490        |        |           |
|         | qe5bzwpg |             | 7           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/8 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-8_insidebackcover/full/max/0/default.jpg  | Inside back cover       | b873x2qy   | 4315         | 2490        |        |           |
|         | nvurzczf |             | 8           | https://iiif.io/api/cookbook/recipe/0035-foldouts/canvas/9 |             | https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-9_backcover/full/max/0/default.jpg        | Back cover              | b873x2qy   | 4315         | 2490        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/b873x2qy",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "bmpq42h4",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Front cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-1_frontcover/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "cs7fkget",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Inside front cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-2_insidefrontcover/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "gjhhas76",
        "canvasOrder": 2,
        "label": {
          "en": [
            "Foldout, folded"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-3_foldout-folded/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "r7yppr8s",
        "canvasOrder": 3,
        "label": {
          "en": [
            "Foldout, unfolded"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-4_foldout/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "hjcwsq3n",
        "canvasOrder": 4,
        "label": {
          "en": [
            "Foldout, folded (recto)"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-3_foldout-rotated/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "x3fdfxn4",
        "canvasOrder": 5,
        "label": {
          "en": [
            "Title page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-5_titlepage/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "tvm9kmts",
        "canvasOrder": 6,
        "label": {
          "en": [
            "Back of title page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-6_titlepage-recto/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "qe5bzwpg",
        "canvasOrder": 7,
        "label": {
          "en": [
            "Inside back cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-8_insidebackcover/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "nvurzczf",
        "canvasOrder": 8,
        "label": {
          "en": [
            "Back cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/0a469c27256eda739d43124cc448a3ba-9_backcover/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Tagging with an External Resource
https://iiif.io/api/cookbook/recipe/0258-tagging-external-resource/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                             | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | d9y5zz85 |             | 0           | https://iiif.io/api/cookbook/recipe/0258-tagging-external-resource/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | ewz2pcwz   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/ewz2pcwz",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "d9y5zz85",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Annotate specific images or layers
https://iiif.io/api/cookbook/recipe/0326-annotating-image-layer/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                          | ChoiceOrder | ExternalAssetId                                                                                                        | Label         | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------- | ------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | cyxgmsx7 |             | 0           | https://iiif.io/api/cookbook/recipe/0326-annotating-image-layer/canvas/p1 | 1           | https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg    | Natural Light | j5qrtyvs   | 1271         | 2000        |        |           |
|         | cyxgmsx7 |             | 0           | https://iiif.io/api/cookbook/recipe/0326-annotating-image-layer/canvas/p1 | 2           | https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/2000,1271/0/default.jpg | X-ray         | j5qrtyvs   | 1271         | 2000        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/j5qrtyvs",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "cyxgmsx7",
        "canvasOrder": 0,
        "choiceOrder": 1,
        "label": {
          "en": [
            "Natural Light"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-natural/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "cyxgmsx7",
        "canvasOrder": 0,
        "choiceOrder": 2,
        "label": {
          "en": [
            "X-ray"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/421e65be2ce95439b3ad6ef1f2ab87a9-dee-xray/full/2000,1271/0/default.jpg"
      }
    }
  ]
}
```




## Annotation with a Non-Rectangular Polygon
https://iiif.io/api/cookbook/recipe/0261-non-rectangular-commenting/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                              | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ----------------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | pbv5gqea |             | 0           | https://iiif.io/api/cookbook/recipe/0261-non-rectangular-commenting/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | q9q3rqub   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/q9q3rqub",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "pbv5gqea",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Viewing direction and Its Effect on Navigation
https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/manifest-rtl.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                            | ChoiceOrder | ExternalAssetId                                                                                                                      | Label       | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------ | ----------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | bk3rn2at |             | 0           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001/full/max/0/default.jpg | front cover | k8gmfcxu   | 4823         | 3497        |        |           |
|         | db7t7y78 |             | 1           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_002/full/max/0/default.jpg | pages 1–2   | k8gmfcxu   | 4804         | 6062        |        |           |
|         | r7txjw3h |             | 2           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_003/full/max/0/default.jpg | pages 3–4   | k8gmfcxu   | 4776         | 6127        |        |           |
|         | eb2etyj2 |             | 3           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_004/full/max/0/default.jpg | pages 5–6   | k8gmfcxu   | 4751         | 6124        |        |           |
|         | qkxcxakx |             | 4           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/p5 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_005/full/max/0/default.jpg | back cover  | k8gmfcxu   | 4808         | 3510        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/k8gmfcxu",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "bk3rn2at",
        "canvasOrder": 0,
        "label": {
          "en": [
            "front cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "db7t7y78",
        "canvasOrder": 1,
        "label": {
          "en": [
            "pages 1\u20132"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_002/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "r7txjw3h",
        "canvasOrder": 2,
        "label": {
          "en": [
            "pages 3\u20134"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_003/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "eb2etyj2",
        "canvasOrder": 3,
        "label": {
          "en": [
            "pages 5\u20136"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_004/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "qkxcxakx",
        "canvasOrder": 4,
        "label": {
          "en": [
            "back cover"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_005/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Image Thumbnail for Manifest
https://iiif.io/api/cookbook/recipe/0117-add-image-thumbnail/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                       | ChoiceOrder | ExternalAssetId                                                                                                                           | Label                      | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | pssau5jh |             | 0           | https://iiif.io/api/cookbook/recipe/0117-add-image-thumbnail/canvas/p0 |             | https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001_full/full/max/0/default.jpg | front cover with color bar | j94e68kr   | 5312         | 4520        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/j94e68kr",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "pssau5jh",
        "canvasOrder": 0,
        "label": {
          "en": [
            "front cover with color bar"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/4f92cceb12dd53b52433425ce44308c7-ucla_bib1987273_no001_rs_001_full/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Load Manifest Beginning with a Specific Canvas
https://iiif.io/api/cookbook/recipe/0202-start-canvas/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                | ChoiceOrder | ExternalAssetId                                                                                                                             | Label        | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------- | ------------ | ---------- | ------------ | ----------- | ------ | --------- |
|         | h4ruy2q3 |             | 0           | https://iiif.io/api/cookbook/recipe/0202-start-canvas/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f18/full/max/0/default.jpg | Blank page   | f4r2axb9   | 4613         | 3204        |        |           |
|         | faep376h |             | 1           | https://iiif.io/api/cookbook/recipe/0202-start-canvas/canvas/p2 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f19/full/max/0/default.jpg | Frontispiece | f4r2axb9   | 4612         | 3186        |        |           |
|         | xzab23n4 |             | 2           | https://iiif.io/api/cookbook/recipe/0202-start-canvas/canvas/p3 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f20/full/max/0/default.jpg | Title page   | f4r2axb9   | 4613         | 3204        |        |           |
|         | qyr5vcbp |             | 3           | https://iiif.io/api/cookbook/recipe/0202-start-canvas/canvas/p4 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f21/full/max/0/default.jpg | Blank page   | f4r2axb9   | 4578         | 3174        |        |           |
|         | ck559g9e |             | 4           | https://iiif.io/api/cookbook/recipe/0202-start-canvas/canvas/p5 |             | https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f22/full/max/0/default.jpg | Bookplate    | f4r2axb9   | 4632         | 3198        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/f4r2axb9",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "h4ruy2q3",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Blank page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f18/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "faep376h",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Frontispiece"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f19/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "xzab23n4",
        "canvasOrder": 2,
        "label": {
          "en": [
            "Title page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f20/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "qyr5vcbp",
        "canvasOrder": 3,
        "label": {
          "en": [
            "Blank page"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f21/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "ck559g9e",
        "canvasOrder": 4,
        "label": {
          "en": [
            "Bookplate"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/59d09e6773341f28ea166e9f3c1e674f-gallica_ark_12148_bpt6k1526005v_f22/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Represent Canvas Fragment as a Geographic Area in a Web Mapping Client
https://iiif.io/api/cookbook/recipe/0139-geolocate-canvas-fragment/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                               | ChoiceOrder | ExternalAssetId                                                                                                  | Label                              | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------------ | ----------- | ---------------------------------------------------------------------------------------------------------------- | ---------------------------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | hz7u68j8 |             | 0           | https://iiif.io/api/cookbook/recipe/0139-geolocate-canvas-fragment/canvas.json |             | https://iiif.io/api/image/3.0/example/reference/43153e2ec7531f14dd1c9b2fc401678a-88695674/full/max/0/default.jpg | Chesapeake and Ohio Canal Pamphlet | x8jcp39g   | 7072         | 5212        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/x8jcp39g",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "hz7u68j8",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Chesapeake and Ohio Canal Pamphlet"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/43153e2ec7531f14dd1c9b2fc401678a-88695674/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Multiple Choice of Audio Formats in a Single View (Canvas)
https://iiif.io/api/cookbook/recipe/0434-choice-av/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel               | CanvasOrder | CanvasOriginalId                                            | ChoiceOrder | ExternalAssetId                                          | Label          | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ------------------------- | ----------- | ----------------------------------------------------------- | ----------- | -------------------------------------------------------- | -------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | a56avyyq | Pick one of these formats | 0           | https://iiif.io/api/cookbook/recipe/0434-choice-av/canvas/1 | 1           | https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.m4a  | ALAC           | t64nmv9f   |              |             |        |           |
|         | a56avyyq |                           | 0           | https://iiif.io/api/cookbook/recipe/0434-choice-av/canvas/1 | 2           | https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.mp3  | MP3            | t64nmv9f   |              |             |        |           |
|         | a56avyyq |                           | 0           | https://iiif.io/api/cookbook/recipe/0434-choice-av/canvas/1 | 3           | https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.flac | FLAC           | t64nmv9f   |              |             |        |           |
|         | a56avyyq |                           | 0           | https://iiif.io/api/cookbook/recipe/0434-choice-av/canvas/1 | 4           | https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.ogg  | OGG Vorbis OGG | t64nmv9f   |              |             |        |           |
|         | a56avyyq |                           | 0           | https://iiif.io/api/cookbook/recipe/0434-choice-av/canvas/1 | 5           | https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.mpeg | MPEG2          | t64nmv9f   |              |             |        |           |
|         | a56avyyq |                           | 0           | https://iiif.io/api/cookbook/recipe/0434-choice-av/canvas/1 | 6           | https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.wav  | WAV            | t64nmv9f   |              |             |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/t64nmv9f",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "a56avyyq",
        "canvasOrder": 0,
        "choiceOrder": 1,
        "label": {
          "en": [
            "ALAC"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.m4a",
        "canvasLabel": {
          "en": [
            "Pick one of these formats"
          ]
        }
      }
    },
    {
      "canvasPainting": {
        "canvas": "a56avyyq",
        "canvasOrder": 0,
        "choiceOrder": 2,
        "label": {
          "en": [
            "MP3"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.mp3"
      }
    },
    {
      "canvasPainting": {
        "canvas": "a56avyyq",
        "canvasOrder": 0,
        "choiceOrder": 3,
        "label": {
          "en": [
            "FLAC"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.flac"
      }
    },
    {
      "canvasPainting": {
        "canvas": "a56avyyq",
        "canvasOrder": 0,
        "choiceOrder": 4,
        "label": {
          "en": [
            "OGG Vorbis OGG"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.ogg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "a56avyyq",
        "canvasOrder": 0,
        "choiceOrder": 5,
        "label": {
          "en": [
            "MPEG2"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.mpeg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "a56avyyq",
        "canvasOrder": 0,
        "choiceOrder": 6,
        "label": {
          "en": [
            "WAV"
          ]
        },
        "externalAssetId": "https://fixtures.iiif.io/audio/ucla/egbe-iyawo-ucla.wav"
      }
    }
  ]
}
```




## Book 'behavior' Variations (continuous, individuals)
https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/manifest-continuous.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                   | ChoiceOrder | ExternalAssetId                                                                                                                         | Label             | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------ | ----------- | --------------------------------------------------------------------------------------------------------------------------------------- | ----------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | mvbvf68p |             | 0           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/s1 |             | https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmd9_1300412_master/full/max/0/default.jpg | Section 1 [Recto] | x26g5d58   | 1592         | 11368       |        |           |
|         | rph3k8e5 |             | 1           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/s2 |             | https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmft_1300418_master/full/max/0/default.jpg | Section 2 [Recto] | x26g5d58   | 1536         | 11608       |        |           |
|         | j623nb2b |             | 2           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/s3 |             | https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmgb_1300426_master/full/max/0/default.jpg | Section 3 [Recto] | x26g5d58   | 1504         | 10576       |        |           |
|         | bcctpqwr |             | 3           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/s4 |             | https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmhv_1300436_master/full/max/0/default.jpg | Section 4 [Recto] | x26g5d58   | 1464         | 2488        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/x26g5d58",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "mvbvf68p",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Section 1 [Recto]"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmd9_1300412_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "rph3k8e5",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Section 2 [Recto]"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmft_1300418_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "j623nb2b",
        "canvasOrder": 2,
        "label": {
          "en": [
            "Section 3 [Recto]"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmgb_1300426_master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "bcctpqwr",
        "canvasOrder": 3,
        "label": {
          "en": [
            "Section 4 [Recto]"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/8c169124171e6b2253b698a22a938f07-21198-zz001hbmhv_1300436_master/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Image Rotation Two Ways
https://iiif.io/api/cookbook/recipe/0040-image-rotation-service/manifest-service.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                          | ChoiceOrder | ExternalAssetId                                                                                                                    | Label            | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------- | ---------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | gmpxk9nb |             | 0           | https://iiif.io/api/cookbook/recipe/0040-image-rotation-service/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-0-21198-zz00022840-1-page1/full/max/0/default.jpg | inside cover; 1r | p4672jqy   | 2105         | 1523        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/p4672jqy",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "gmpxk9nb",
        "canvasOrder": 0,
        "label": {
          "en": [
            "inside cover; 1r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-0-21198-zz00022840-1-page1/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Embedding HTML in descriptive properties
https://iiif.io/api/cookbook/recipe/0007-string-formats/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                  | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ----------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | mkj4uj8r |             | 0           | https://iiif.io/api/cookbook/recipe/0007-string-formats/canvas/p1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | vpybm597   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/vpybm597",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "mkj4uj8r",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Linking external Annotations targeting a Canvas to a Manifest
https://iiif.io/api/cookbook/recipe/0306-linking-annotations-to-manifests/manifest.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                                   | ChoiceOrder | ExternalAssetId                                                                                                   | Label | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ---------------------------------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------- | ----- | ---------- | ------------ | ----------- | ------ | --------- |
|         | c5e5znsa |             | 0           | https://iiif.io/api/cookbook/recipe/0306-linking-annotations-to-manifests/canvas-1 |             | https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg |       | tz3ygmuv   | 3024         | 4032        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/tz3ygmuv",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "c5e5znsa",
        "canvasOrder": 0,
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/918ecd18c2592080851777620de9bcb5-gottingen/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Viewing direction and Its Effect on Navigation
https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/manifest-ttb.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                            | ChoiceOrder | ExternalAssetId                                                                                                                  | Label   | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | --------------------------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------------- | ------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | apgke53v |             | 0           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/v1 |             | https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_02/full/max/0/default.jpg | image 1 | mqx9gv5g   | 3152         | 2251        |        |           |
|         | qjsby8fe |             | 1           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/v2 |             | https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_03/full/max/0/default.jpg | image 2 | mqx9gv5g   | 3135         | 2268        |        |           |
|         | pkbdan7n |             | 2           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/v3 |             | https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_04/full/max/0/default.jpg | image 3 | mqx9gv5g   | 3135         | 2274        |        |           |
|         | jbs7ryp2 |             | 3           | https://iiif.io/api/cookbook/recipe/0010-book-2-viewing-direction/canvas/v4 |             | https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_05/full/max/0/default.jpg | image 4 | mqx9gv5g   | 3135         | 2268        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/mqx9gv5g",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "apgke53v",
        "canvasOrder": 0,
        "label": {
          "en": [
            "image 1"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_02/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "qjsby8fe",
        "canvasOrder": 1,
        "label": {
          "en": [
            "image 2"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_03/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "pkbdan7n",
        "canvasOrder": 2,
        "label": {
          "en": [
            "image 3"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_04/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "jbs7ryp2",
        "canvasOrder": 3,
        "label": {
          "en": [
            "image 4"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/9ee11092dfd2782634f5e8e2c87c16d5-uclamss_1841_diary_07_05/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Book 'behavior' Variations (continuous, individuals)
https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/manifest-individuals.json


### canvas_painting rows

| AssetId | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                                   | ChoiceOrder | ExternalAssetId                                                                                                                     | Label            | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ------- | -------- | ----------- | ----------- | ------------------------------------------------------------------ | ----------- | ----------------------------------------------------------------------------------------------------------------------------------- | ---------------- | ---------- | ------------ | ----------- | ------ | --------- |
|         | budzre9h |             | 0           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/v1 |             | https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-0-21198-zz00022840-1-master/full/max/0/default.jpg | inside cover; 1r | ryxfge2g   | 2250         | 3375        |        |           |
|         | j92qvmxr |             | 1           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/v2 |             | https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-1-21198-zz00022882-1-master/full/max/0/default.jpg | 2v, 3r           | ryxfge2g   | 2250         | 3375        |        |           |
|         | kd7cud8v |             | 2           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/v3 |             | https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-2-21198-zz000228b3-1-master/full/max/0/default.jpg | 3v, 4r           | ryxfge2g   | 2250         | 3375        |        |           |
|         | utee4774 |             | 3           | https://iiif.io/api/cookbook/recipe/0011-book-3-behavior/canvas/v4 |             | https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-3-21198-zz000228d4-1-master/full/max/0/default.jpg | 4v, 5r           | ryxfge2g   | 2250         | 3375        |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/ryxfge2g",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "budzre9h",
        "canvasOrder": 0,
        "label": {
          "en": [
            "inside cover; 1r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-0-21198-zz00022840-1-master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "j92qvmxr",
        "canvasOrder": 1,
        "label": {
          "en": [
            "2v, 3r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-1-21198-zz00022882-1-master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "kd7cud8v",
        "canvasOrder": 2,
        "label": {
          "en": [
            "3v, 4r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-2-21198-zz000228b3-1-master/full/max/0/default.jpg"
      }
    },
    {
      "canvasPainting": {
        "canvas": "utee4774",
        "canvasOrder": 3,
        "label": {
          "en": [
            "4v, 5r"
          ]
        },
        "externalAssetId": "https://iiif.io/api/image/3.0/example/reference/85a96c630f077e6ac6cb984f1b752bbf-3-21198-zz000228d4-1-master/full/max/0/default.jpg"
      }
    }
  ]
}
```




## Wunder internal
https://dlcs.io/iiif-resource/wellcome/preview/5/b18035723


### canvas_painting rows

| AssetId                | CanvasId | CanvasLabel | CanvasOrder | CanvasOriginalId                                            | ChoiceOrder | ExternalAssetId | Label     | ManifestId | StaticHeight | StaticWidth | Target | Thumbnail |
| ---------------------- | -------- | ----------- | ----------- | ----------------------------------------------------------- | ----------- | --------------- | --------- | ---------- | ------------ | ----------- | ------ | --------- |
| 2/5/b18035723_0001.JP2 | jmkcg3s4 |             | 0           | https://dlcs.io/iiif-img/2/5/b18035723_0001.JP2/canvas/c/1  |             |                 | Canvas 1  | faahqndv   | 1024         | 742         |        |           |
| 2/5/b18035723_0002.JP2 | zqwdyvez |             | 1           | https://dlcs.io/iiif-img/2/5/b18035723_0002.JP2/canvas/c/2  |             |                 | Canvas 2  | faahqndv   | 1024         | 751         |        |           |
| 2/5/b18035723_0003.JP2 | y739jy99 |             | 2           | https://dlcs.io/iiif-img/2/5/b18035723_0003.JP2/canvas/c/3  |             |                 | Canvas 3  | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0004.JP2 | k958s3kh |             | 3           | https://dlcs.io/iiif-img/2/5/b18035723_0004.JP2/canvas/c/4  |             |                 | Canvas 4  | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0005.JP2 | d5hpss4t |             | 4           | https://dlcs.io/iiif-img/2/5/b18035723_0005.JP2/canvas/c/5  |             |                 | Canvas 5  | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0006.JP2 | g357b4we |             | 5           | https://dlcs.io/iiif-img/2/5/b18035723_0006.JP2/canvas/c/6  |             |                 | Canvas 6  | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0007.JP2 | aa7ektzy |             | 6           | https://dlcs.io/iiif-img/2/5/b18035723_0007.JP2/canvas/c/7  |             |                 | Canvas 7  | faahqndv   | 1024         | 752         |        |           |
| 2/5/b18035723_0008.JP2 | rv7wcb2e |             | 7           | https://dlcs.io/iiif-img/2/5/b18035723_0008.JP2/canvas/c/8  |             |                 | Canvas 8  | faahqndv   | 1024         | 750         |        |           |
| 2/5/b18035723_0009.JP2 | w3tqrx3s |             | 8           | https://dlcs.io/iiif-img/2/5/b18035723_0009.JP2/canvas/c/9  |             |                 | Canvas 9  | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0010.JP2 | su4b6y9f |             | 9           | https://dlcs.io/iiif-img/2/5/b18035723_0010.JP2/canvas/c/10 |             |                 | Canvas 10 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0011.JP2 | r868r98y |             | 10          | https://dlcs.io/iiif-img/2/5/b18035723_0011.JP2/canvas/c/11 |             |                 | Canvas 11 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0012.JP2 | nzf57mrn |             | 11          | https://dlcs.io/iiif-img/2/5/b18035723_0012.JP2/canvas/c/12 |             |                 | Canvas 12 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0013.JP2 | kw7rebjz |             | 12          | https://dlcs.io/iiif-img/2/5/b18035723_0013.JP2/canvas/c/13 |             |                 | Canvas 13 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0014.JP2 | mbs6wkpu |             | 13          | https://dlcs.io/iiif-img/2/5/b18035723_0014.JP2/canvas/c/14 |             |                 | Canvas 14 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0015.JP2 | c7z8txks |             | 14          | https://dlcs.io/iiif-img/2/5/b18035723_0015.JP2/canvas/c/15 |             |                 | Canvas 15 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0016.JP2 | xa57vq7t |             | 15          | https://dlcs.io/iiif-img/2/5/b18035723_0016.JP2/canvas/c/16 |             |                 | Canvas 16 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0017.JP2 | jmnhcf49 |             | 16          | https://dlcs.io/iiif-img/2/5/b18035723_0017.JP2/canvas/c/17 |             |                 | Canvas 17 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0018.JP2 | b2tvjhyj |             | 17          | https://dlcs.io/iiif-img/2/5/b18035723_0018.JP2/canvas/c/18 |             |                 | Canvas 18 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0019.JP2 | uc57htnk |             | 18          | https://dlcs.io/iiif-img/2/5/b18035723_0019.JP2/canvas/c/19 |             |                 | Canvas 19 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0020.JP2 | az3f2jxq |             | 19          | https://dlcs.io/iiif-img/2/5/b18035723_0020.JP2/canvas/c/20 |             |                 | Canvas 20 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0021.JP2 | v9zshwzm |             | 20          | https://dlcs.io/iiif-img/2/5/b18035723_0021.JP2/canvas/c/21 |             |                 | Canvas 21 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0022.JP2 | hcbp23w4 |             | 21          | https://dlcs.io/iiif-img/2/5/b18035723_0022.JP2/canvas/c/22 |             |                 | Canvas 22 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0023.JP2 | e6qvbf6j |             | 22          | https://dlcs.io/iiif-img/2/5/b18035723_0023.JP2/canvas/c/23 |             |                 | Canvas 23 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0024.JP2 | j68fhrkc |             | 23          | https://dlcs.io/iiif-img/2/5/b18035723_0024.JP2/canvas/c/24 |             |                 | Canvas 24 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0025.JP2 | r59v26f2 |             | 24          | https://dlcs.io/iiif-img/2/5/b18035723_0025.JP2/canvas/c/25 |             |                 | Canvas 25 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0026.JP2 | rfpdcerv |             | 25          | https://dlcs.io/iiif-img/2/5/b18035723_0026.JP2/canvas/c/26 |             |                 | Canvas 26 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0027.JP2 | m63ry8vd |             | 26          | https://dlcs.io/iiif-img/2/5/b18035723_0027.JP2/canvas/c/27 |             |                 | Canvas 27 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0028.JP2 | rmjcqwa9 |             | 27          | https://dlcs.io/iiif-img/2/5/b18035723_0028.JP2/canvas/c/28 |             |                 | Canvas 28 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0029.JP2 | cfdwx5f9 |             | 28          | https://dlcs.io/iiif-img/2/5/b18035723_0029.JP2/canvas/c/29 |             |                 | Canvas 29 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0030.JP2 | k7bk3mq5 |             | 29          | https://dlcs.io/iiif-img/2/5/b18035723_0030.JP2/canvas/c/30 |             |                 | Canvas 30 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0031.JP2 | cakfphvs |             | 30          | https://dlcs.io/iiif-img/2/5/b18035723_0031.JP2/canvas/c/31 |             |                 | Canvas 31 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0032.JP2 | su87uqvn |             | 31          | https://dlcs.io/iiif-img/2/5/b18035723_0032.JP2/canvas/c/32 |             |                 | Canvas 32 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0033.JP2 | mtsvw95b |             | 32          | https://dlcs.io/iiif-img/2/5/b18035723_0033.JP2/canvas/c/33 |             |                 | Canvas 33 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0034.JP2 | t556bt2k |             | 33          | https://dlcs.io/iiif-img/2/5/b18035723_0034.JP2/canvas/c/34 |             |                 | Canvas 34 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0035.JP2 | watv7f62 |             | 34          | https://dlcs.io/iiif-img/2/5/b18035723_0035.JP2/canvas/c/35 |             |                 | Canvas 35 | faahqndv   | 1024         | 732         |        |           |
| 2/5/b18035723_0036.JP2 | na7hs4cy |             | 35          | https://dlcs.io/iiif-img/2/5/b18035723_0036.JP2/canvas/c/36 |             |                 | Canvas 36 | faahqndv   | 1024         | 732         |        |           |


### paintedResources property in DLCS Manifest

```json
{
  "id": "https://dlc.services/iiif/99/manifests/faahqndv",
  "type": "Manifest",
  "paintedResources": [
    {
      "canvasPainting": {
        "canvas": "jmkcg3s4",
        "canvasOrder": 0,
        "label": {
          "en": [
            "Canvas 1"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0001.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "zqwdyvez",
        "canvasOrder": 1,
        "label": {
          "en": [
            "Canvas 2"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0002.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "y739jy99",
        "canvasOrder": 2,
        "label": {
          "en": [
            "Canvas 3"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0003.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "k958s3kh",
        "canvasOrder": 3,
        "label": {
          "en": [
            "Canvas 4"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0004.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "d5hpss4t",
        "canvasOrder": 4,
        "label": {
          "en": [
            "Canvas 5"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0005.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "g357b4we",
        "canvasOrder": 5,
        "label": {
          "en": [
            "Canvas 6"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0006.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "aa7ektzy",
        "canvasOrder": 6,
        "label": {
          "en": [
            "Canvas 7"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0007.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "rv7wcb2e",
        "canvasOrder": 7,
        "label": {
          "en": [
            "Canvas 8"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0008.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "w3tqrx3s",
        "canvasOrder": 8,
        "label": {
          "en": [
            "Canvas 9"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0009.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "su4b6y9f",
        "canvasOrder": 9,
        "label": {
          "en": [
            "Canvas 10"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0010.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "r868r98y",
        "canvasOrder": 10,
        "label": {
          "en": [
            "Canvas 11"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0011.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "nzf57mrn",
        "canvasOrder": 11,
        "label": {
          "en": [
            "Canvas 12"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0012.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "kw7rebjz",
        "canvasOrder": 12,
        "label": {
          "en": [
            "Canvas 13"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0013.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "mbs6wkpu",
        "canvasOrder": 13,
        "label": {
          "en": [
            "Canvas 14"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0014.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "c7z8txks",
        "canvasOrder": 14,
        "label": {
          "en": [
            "Canvas 15"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0015.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "xa57vq7t",
        "canvasOrder": 15,
        "label": {
          "en": [
            "Canvas 16"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0016.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "jmnhcf49",
        "canvasOrder": 16,
        "label": {
          "en": [
            "Canvas 17"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0017.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "b2tvjhyj",
        "canvasOrder": 17,
        "label": {
          "en": [
            "Canvas 18"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0018.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "uc57htnk",
        "canvasOrder": 18,
        "label": {
          "en": [
            "Canvas 19"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0019.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "az3f2jxq",
        "canvasOrder": 19,
        "label": {
          "en": [
            "Canvas 20"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0020.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "v9zshwzm",
        "canvasOrder": 20,
        "label": {
          "en": [
            "Canvas 21"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0021.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "hcbp23w4",
        "canvasOrder": 21,
        "label": {
          "en": [
            "Canvas 22"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0022.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "e6qvbf6j",
        "canvasOrder": 22,
        "label": {
          "en": [
            "Canvas 23"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0023.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "j68fhrkc",
        "canvasOrder": 23,
        "label": {
          "en": [
            "Canvas 24"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0024.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "r59v26f2",
        "canvasOrder": 24,
        "label": {
          "en": [
            "Canvas 25"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0025.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "rfpdcerv",
        "canvasOrder": 25,
        "label": {
          "en": [
            "Canvas 26"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0026.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "m63ry8vd",
        "canvasOrder": 26,
        "label": {
          "en": [
            "Canvas 27"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0027.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "rmjcqwa9",
        "canvasOrder": 27,
        "label": {
          "en": [
            "Canvas 28"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0028.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "cfdwx5f9",
        "canvasOrder": 28,
        "label": {
          "en": [
            "Canvas 29"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0029.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "k7bk3mq5",
        "canvasOrder": 29,
        "label": {
          "en": [
            "Canvas 30"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0030.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "cakfphvs",
        "canvasOrder": 30,
        "label": {
          "en": [
            "Canvas 31"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0031.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "su87uqvn",
        "canvasOrder": 31,
        "label": {
          "en": [
            "Canvas 32"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0032.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "mtsvw95b",
        "canvasOrder": 32,
        "label": {
          "en": [
            "Canvas 33"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0033.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "t556bt2k",
        "canvasOrder": 33,
        "label": {
          "en": [
            "Canvas 34"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0034.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "watv7f62",
        "canvasOrder": 34,
        "label": {
          "en": [
            "Canvas 35"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0035.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    },
    {
      "canvasPainting": {
        "canvas": "na7hs4cy",
        "canvasOrder": 35,
        "label": {
          "en": [
            "Canvas 36"
          ]
        }
      },
      "asset": {
        "id": "2/5/b18035723_0036.JP2",
        "mediaType": "example/contentType",
        "origin": "s3://bucket/key"
      }
    }
  ]
}
```



