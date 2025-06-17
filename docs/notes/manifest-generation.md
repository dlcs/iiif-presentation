# Manifest Generation

This doc contains some implementation details around how Manifests are genrrated from NamedQuery. 

This isn't exhaustive, as always the code will reflect what's actually happening.

## CanvasId

Anything that shares a canvasId is on the same canvas.

Items on a single canvas can share a `canvasOrder`, if they're a choice, in which case they'll also have a `choiceOrder`.

Items on a single canvas can have differing `canvasOrder`, if they're composite-canvases (e.g multiple painting annotations on same canvas). These will need to specify a `target` to display correctly.

## Choices

NamedQuery Manifests will always contains 1 asset per `Canvas`. This will be contained in a single `AnnotationPage` and in a single `PaintingAnnotation`.

The `PaintingAnnotation` will generally be a single `Image`, `Sound` or `Video`. 

However, in the case of transcoded timebased media the output could be a `Choice` of n `Sound` or `Video` annotations, 1 per transcode.

For canvasPaintings that share a `canvasOrder` we generate a [`Choice`](https://iiif.io/api/cookbook/recipe/0033-choice/#example). The final manifest will always contain a single `Choice`, if the NQ returns a `Choice` it will be flattened - we will never return a choice of choices. See https://github.com/dlcs/iiif-presentation/issues/308

## Composite Canvas

Any canvasPaintings that share a `canvasId` but not a `canvasOrder` will be rendered as a new painting annotation on the same canvas.

## AssetId identification

The Asset paths for NamedQuerys can be rewritten in accordance with templates, see https://github.com/dlcs/protagonist/issues/983

However the NQ `Canvas.Id` is never rewritten - it will always have a consistent format. Due to this we can use the `Canvas.Id` to find the appropriate canvas for an `AssetId`, regardless of any rewrite rules.

## Label Handling

`CanvasPainting` records have `label` and `canvasLabel` properties. We generally prefer to set the label on the `Canvas` as this is what most viewers will display. The rules for lables are:

* If it's a single item canvas:
  * If we have `label` only - the canvas gets `label`
  * If we have `canvasLabel` only - the canvas gets `canvasLabel`
  * If we have `label` and `canvasLabel` - the canvas gets `canvasLabel` and the IPaintable gets `label`
  * _on any path canvas.label is set_
* If it's a choice or composite canvas
  * If we have `labels` only - this is set on the IPaintable/choice AND canvas gets the first non-null value
  * If we have `canvasLabels` only - the canvas gets the first non-null `canvasLabel`
  * If we have `label` and canvasLabel - the canvas gets the first non-null `canvasLabel` and the IPaintable(s) gets `label`
  * _on any path canvas.label is set - might duplicate what's on first image but that's okay_

## Renderings

Non-image assets may return a `"Rendering"` property on manifest. If this happens, and there's a choice or composite canvas, the `"Rendering"` properties will be concatenated.

See https://github.com/dlcs/protagonist/blob/main/docs/rfcs/020-non-image-iiif.md for more info on NQ handling non-image assets.
