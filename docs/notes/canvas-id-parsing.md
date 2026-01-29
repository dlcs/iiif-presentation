# Canvas Id parsing

When parsing payloads, the `canvasId` can be used to generate a `paintedResource` `id` record.  This can be accepted either directly from a `paintedResource` `canvasId` and also from `items` `id` of a canvas.  In addition to the 2 locations that we can generate a `canvasId` from, we can accept either a short form (i.e.: just the id itself) or alternatively from a recognised URI.  Finally, this `canvasId` can be used to join a `canvas` declared in `items` with a `canvasPainting` record in order to decorate an asset from the DLCS with additional IIIF.   This document is set to explain the various ways this `canvasId` can be parsed from a payload.

> [!NOTE]
> - when talking about `canvas`, `items` is also used to mean "a group of canvases"
> - `paintedResource` means taken from the payload
> - `canvasPainting` means the database record

## Accepted formats

As mentioned above, there are a few formats that will be accepted 


| Format      | Example     |
| ------------- | ------------- |
| short canvas | `someId` |
| general API URL | `https://presentation-api.com/1/canvases/someId` |
| customer specific API URL | `https:/customer-base.com/canvases/someId` |


## Basic process

When we receive a canvas id, a series of actions happen to see if we can work out the canvas id based on the given value.  This can be quite complex to follow the logic, so the below flowcharts show the steps taken. Given there is a difference between how the canvas id is processed from items versus painted resources 

```mermaid
---
title: Items
---
flowchart TD
    first[Canvas provided]
    second{is the canvas id null}
    third[generate the canvas id]
    fourth{is the canvas id a URI}
    fifth{any invalid characters}
    sixth[use the provided canvas id]
    seventh{is this a recognised host}
    eighth[parse path with rewrites]
    ninth{is the path parsed}
    tenth[throw an error]
    eleventh{matches painted resource}
    twelfth{matches painted resource}

    first --> second
    second -- yes --> third
    second -- no --> fourth
    fourth -- no --> eleventh
    eleventh -- yes --> fifth
    eleventh -- no --> tenth
    fifth -- no --> sixth
    fifth -- yes --> third
    fourth -- yes --> seventh
    seventh -- no --> third
    seventh -- yes --> eighth
    eighth --> ninth
    ninth -- no --> third
    ninth -- yes --> twelfth
    twelfth -- no --> third
    twelfth -- yes --> fifth
```

```mermaid
---
title: Painted Resources
---
flowchart TD
    first[PaintedResource CanvasPainting provided]
    second{is the canvas id null}
    third[generate the canvas id]
    fourth{is the canvas id a URI}
    fifth{any invalid characters}
    sixth[use the provided canvas id]
    seventh[parse path with rewrites]
    eighth{is the path parsed}
    ninth[throw an error]

    first --> second
    second -- yes --> third
    second -- no --> fourth
    fourth -- no --> fifth
    fifth -- no --> sixth
    fifth -- yes --> ninth
    fourth -- yes --> seventh
    subgraph no check for recognised host
        seventh 
    end
    seventh --> eighth
    eighth -- yes --> fifth
    eighth -- no --> ninth
```

There is a slight difference between the id being parsed from `items` versus `paintedResource`, in that `items` has an additional check for "is a recognised host".  This is because `items` needs to be slightly tighter than `canvasPaintings` as the `canvasId` will be _generated_ for the `canvasPainting` table, but the id in the `items`will be left alone, with a `canvasOriginalId` added to the `canvasPainting` record.  This ultimately helps to avoid rejecting payloads that are purely IIIF that have been copied around from another customer.  This check is essentially checking that the passed URL is either the general API URL or the customer specific URL.

The "is recognised host" check depends on the below settings to recognise a host:

```json
"PresentationApiUrl": "https://presentation-api.com",
    "CustomerPresentationApiUrl": {
      "1": "https:/customer-base.com", // this matches based on the customer id
    }
```
this is then combined with settings from `PathRules` to parse a URI:

```json
"PathRules": {
      "Defaults": {
        "Canvas": "/{customerId}/canvases/{resourceId}"
      },
      "Overrides": {
        "https:/customer-base.com": {
          "Canvas": "/canvases/{resourceId}"
        }
    }
}
```

Additionally, in `items`, if the host is matched, but the resource is not, (for example, the API expects `https://presentation-api.com/{customer}/canvases/someId` and receives `https://presentation-api.com/someId`), the API will fallback to generating an id instead of throwing an error.

> [!NOTE]
> the only way for a `canvasPainting` to have a `canvasOriginalId` is if the `item` is __not__ matched with a `paintedResource` from the payload

> [!NOTE]
> The currently invalid characters are `/`, `,` and `=`

## Matching canvas painting records

In addition to how the id is retrieved from the payload itself, `paintedResource` is matched to a corresponding canvas in `items` when the `id` matches.  As this value in the database is _only_ the id and not a potential full URI, it does mean that the presentation API can match between slightly different values in the payload.  For example, if the canvas in `items` has an id of `https://presentation-api.com/1/canvases/someId`, it would match with a `paintedResource` `canvasId` of `someId`.


## Short canvas id

When using a short canvas id from `items`, there _must_ be a matching `paintedResource` if the canvas is set using a short canvas.  However, a painted resource can be specified with a short canvas without a matching canvas in `items`.  

## Matching examples

A set of worked examples to try and show the final result, based on the flowchart above

| Painted Resource | Item | Result | Additional Information |
| ---------------- | ---- | ------ | ---------------------- |
| `someId` | null | `someId` | |
| `someId` | `someId` | `someId` | |
| null | `someId` | throws error | must match a PR record  |
| `https://presentation-api.com/1/canvases/someId` | null | `someId` | |
| null | `https://presentation-api.com/1/canvases/someId` | generates id | doesn't match a PR record, so we generate |
| `https://presentation-api.com/1/canvases/someId` | `https://presentation-api.com/1/canvases/someId` |  `someId`  | |
| `https:/customer-base.com/canvases/someId` | `https:/customer-base.com/canvases/someId` |  `someId`  | |
| `https://presentation-api.com/1/canvases/invalidCharacter=` | null |  throws error | |
| null | `https://presentation-api.com/1/canvases/invalidCharacter=` |  generates id | |
| `https://presentation-api.com/invalidCanvasId` | null |  throws error  | |
| null | `https://presentation-api.com/invalidCanvasId` | generates id  | |
|  `https://random.co.uk/someCanvasId` | null |  throws error  | |
| null |  `https://random.co.uk/someCanvasId` |  generates id  | |
| `https:/customer-base.com/canvases/someId` | `https://presentation-api.com/1/canvases/someId` |  `someId`  | |
| `https://presentation-api.com/1/canvases/someId` | `https:/customer-base.com/canvases/someId` |  `someId`  | |
| `someId` | `https://presentation-api.com/1/canvases/someId` |  `someId`  | |
| `https://presentation-api.com/1/canvases/someId` | `someId` |  `someId`  | |


## Combined flowchart

Older flowchart containing both flows, left here for posterity

```mermaid
flowchart TD
    first[Canvas provided]
    second{is the canvas id null}
    third[generate the canvas id]
    fourth{is the canvas id a URI}   
    fifth{any invalid characters}
    sixth[use the provided canvas id]
    seventh{where is this canvas id from?}
    eighth{is this a recognised host}
    ninth[parse path with rewrites]
    tenth{is the path parsed}
    eleventh{from items}
    twelfth[throw an error]
    thirteenth{from items}
    fourteenth{matches painted resource}
    fifteenth{from items, <br> no painted resource match}

    first --> second
    second -- yes --> third
    second -- no --> fourth
    fourth -- no --> thirteenth
    thirteenth -- no --> fifth
    thirteenth -- yes --> fourteenth
    fourteenth -- no --> twelfth
    fourteenth -- yes --> fifth
    fifth -- yes --> eleventh
    fifth -- no --> fifteenth
    fifteenth -- yes --> third
    fifteenth -- no --> sixth
    fourth -- yes --> seventh
    seventh -- items --> eighth
    eighth -- no --> third
    eighth -- yes --> ninth
    seventh -- canvasPaintings --> ninth
    ninth --> tenth
    tenth -- no --> eleventh
    tenth -- yes --> fifth
    eleventh -- yes --> third
    eleventh -- no --> twelfth
```