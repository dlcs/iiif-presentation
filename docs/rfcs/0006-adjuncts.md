# Adjuncts

Protagonist has support for adding adjuncts to Assets, generated Manifests will then output these on Asset Canvases. How can we leverage this to add adjuncts to IIIF Presentation resources; such as Manifests, Collections, Canvases, Ranges etc - ultimately any valid resource with an `id`?

> [!NOTE]
> The initial implementation in IIIF Presentation will support adding Canvas (via assets) and Manifest/Collection level adjuncts _only_.

The high level approach will be:
1. When sending a payload to IIIF Presentation, an `adjuncts` property can be added to any supporting resource.
2. If added to `asset` property, adjuncts will be ingested to that asset as normal. Else..
3. Presentation will create a 'stub' Asset in DLCS to serve as a placeholder to add adjuncts to.
4. Generated NamedQuery Manifests will output 'stub' Assets alongside normal assets, IIIF-Presentation will need to arrange them appropriately.
5. Requests with show-extras headers will output `adjuncts` at relevant location.

More details of these steps are below:

## IIIF-Presentation 'adjuncts' Property

> When sending a payload to IIIF Presentation, an `adjuncts` property can be added to any supporting property.

This section looks at how we can support `adjuncts` property when creating a resource and how these will be handled internally.

### Asset (Canvas) Level

Protagonist already supports asset level adjuncts. By supporting an `adjuncts` property in IIIF Presentation we're allowing consumers to register Assets and their Adjuncts in a single payload. e.g.

```json
{
    "type": "Manifest",
    "parent": "https://{{presentationUrl}}/{{customerId}}/parent",
    "slug": "child",
    "paintedResources": [
        {
            "asset": {
                "id": "one",
                "origin": "https://origin.example/image",
                "mediaType": "image/jpeg",
                "adjuncts": [ // new property
                    { 
                        "id": "mets.xml",
                        "externalId": "https://hosted.example/image/mets.xml",
                        "@type": "Dataset",
                        "mediaType": "text/xml",
                        "iiifLink": "seeAlso"
                    },
                    {
                        "id": "annotation-page",
                        "origin": "https://origin.example/annotation-page",
                        "@type": "AnnotationPage",
                        "mediaType": "application/json",
                        "label": { "en": [ "Line-level annotations" ] },
                        "iiifLink": "annotations"
                    }
                ]
            }
        }
    ]
}
```

The above payload is an instruction to ingest asset `one`, containing 2 adjuncts: `mets.xml` and `annotation-page`. These are Canvas level adjuncts.

It is not possible to do this in a single operation - the Asset needs to be ingested first and then subsequent API call(s) made to ingest adjuncts.

> [!NOTE]
> See Protagonist RFC-013 for suggested updates on how Protagonist can better support this.

> [!CAUTION]
> Without those suggested changes this could result in a lot of API calls.
> If ingesting 100 Assets, each with 5 Adjuncts - this would be 101 API calls. 
> 1x POST to create batch and then 100 POST requests to create per-adjunct Assets

### Manifest/Collection Level

Allow users to supply an `adjuncts` property to the Manifest/Collection. This will allow consumers to associate any supported types (`annotations`, `seeAlso` or `rendering`) at the top level. A sample payload would look like:

```json
{
    "type": "Manifest",
    "parent": "https://{{presentationUrl}}/{{customerId}}/parent",
    "slug": "child",
    "adjuncts": [ // new property
        { 
            "id": "mets.xml",
            "externalId": "https://hosted.example/manifest/mets.xml",
            "@type": "Dataset",
            "mediaType": "text/xml",
            "iiifLink": "seeAlso"
        },
        {
            "id": "annotation-page",
            "origin": "https://origin.example/annotation-page",
            "@type": "AnnotationPage",
            "mediaType": "application/json",
            "label": { "en": [ "Manifest-level annotations" ] },
            "iiifLink": "annotations"
        }
    ]
}
```

This would result in a 'stub' Asset being created (see below).

### Other resources

> [!WARNING]
> This will be supported eventually, it won't be implemented in initial implementation but demonstrates how it could work.

Below shows an example payload of adding `adjuncts` to a Range.

```json
{
  "type": "Manifest",
  "parent": "https://{{presentationUrl}}/{{customerId}}/parent",
  "slug": "child",
  "items": [..],
  "structures": [
    {
      "id": "https://iiif.io/api/cookbook/recipe/0024-book-4-toc/range/r0",
      "type": "Range",
      "label": { "en": [ "Table of Contents" ] },
      "adjuncts": [ // new property
        {
          "id": "mets.xml",
          "externalId": "https://hosted.example/manifest/mets.xml",
          "@type": "Dataset",
          "mediaType": "text/xml",
          "iiifLink": "seeAlso"
        }
      ],
      "items": [..]
    }
  ]
}
```

### Manifest Payloads

To avoid needing to repeatedly supply all adjuncts in payloads we can accept different `"adjuncts"` values:

* `"adjuncts": null` or omitting the `"adjuncts"` property entirely won't result in any adjuncts being removed from an asset.
* `"adjuncts": []` will result in all adjuncts being removed from the relevant resource - whether they are added via IIIF Presentation or directly in Protagonist. The consumer is explicitly stating they want adjuncts to be empty.
* `"adjuncts": [{...}]` will result in the final Asset having these adjuncts only, any not included in the list will be removed.

The above applies for `"adjuncts"` specified at any level.

## Storage and Tracking

When an asset is removed from a Manifest the asset is _not_ deleted from Protagonist; instead the `manifests` property is updated, effectively removing the link - the asset still exists in Protagonist and would need manual intervention to delete it. If, for some reason, the `manifests` link is maintained it's not necessarily an issue. IIIF-Presentation maintains a list of which assets are in which Manifests via the `CanvasPaintings` table, so when constructing the final manifest only the "known" assets from the returned NamedQuery are used - any extraneous assets are ignored. See [Protagonist RFC-019](https://github.com/dlcs/protagonist/blob/develop/docs/rfcs/019-presentation-dlcs.md#updating-resources) for more information on this mechanism. 

Adjuncts will act differently to assets in 2 respects:
1. The final IIIF resource will render _all_ adjuncts for an asset, not only those that have been added via the IIIF-Presentation API. Adjuncts that are added directly to Protagonist would also be reflected in the final Manifest/Collection _but the Manifest/Collection won't automatically update when an adjunct is added directly to Protagonist, there'll need to be an action in IIIF-Presentation to refresh it_.
2. If an adjunct is removed from an asset via IIIF-Presentation, it will be completely removed from Protagonist. This doesn't need to happen immediately at request time and doesn't need to wait for the final resource to be completed. This could lead to a period when adjuncts referenced on an existing resource will return 404 but this will be for a relatively short period.

### Implementation Note

We may need some sort of transient storage to track which adjuncts have been added or removed from a resource. We'll need to determine whether this is worthwhile, unlike `CanvasPaintings` this isn't a source of truth for what to add to the final resource as we are rendering all adjuncts for an asset but it could help to filter out "to be deleted" adjuncts, depending on how we handle deletions (ie are they done in-request, or handled shortly after in a background processor?).

Another consideration is how Collections are handled. To date these will never have any Protagonist interactions, they will only contain standard IIIF but now there's a chance that they will have to load adjuncts.

## "Stub" Assets

> Presentation will create a 'stub' Asset in DLCS to serve as a placeholder to add adjuncts to.

Protagonist already has support for efficiently ingesting, hosting and delivering adjuncts. Duplicating this behaviour in IIIF-Presentation doesn't seem like a good use of time, particularly when factoring in the need to duplicate logic like origin strategies and access control.

The main problem is that we need an asset to add adjuncts to. Using the above [manifest-level](#manifestcollection-level) example payload, we don't have any assets to add these adjuncts to. All we have is a manifest (and, later, other resources). The solution for this is to add a 'stub' asset - it won't ever contain binary content itself, it's only there to serve as a placeholder to add adjuncts to.

The exact requirements for how this is achieved will be documented in Protagonist RFC-013.

To summarise, we will have an AssetId that is `{customer}/{space}/{asset}`, where:
* Customer is the current customer
* Space is _always_ 0.
* Asset is the identifier of the IIIF resource the adjuncts are being attached to. 

Regardless of what IIIF resource we're creating a stub asset for, we will always need to set the `manifest` column for retrieval later.

### Examples

The initial implementation of adjuncts will only support adding them to Manifests or Collections and Canvases via assets but we should ensure the chosen approach will support further types. The `asset` identifier we use will depend on the type of resource the adjunct is being associated with - we need to be able to identify, for a given stub asset's adjunct, which IIIF resource the adjunct is for. 

Suggested format for the `asset` part of stub asset id is: `{type}_{resource_identifier}` or `{type}_{container}_{resource_identifier}`, where

* `type` is the IIIF resource type the adjunct is for
* `container` is used to scope the `resource_identifier` to a specific Manifest or Collection. This is only required for resources that don't have an internal identifier to avoid situations where the same `id` is used in multiple different resources.
* `resource_identifier` is either the internal identifier for the resource, or a normalised representation of the resources `id` uri.

E.g.

| Resource   | `resource_identifier`         | Example                                                                                  | Remarks                                                                                                          |
| ---------- | ----------------------------- | ---------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Manifest   | IIIF-Presentation internal id | `99/0/Manifest_bcdloifk0j1zgp1cvlga8v`                                                   |                                                                                                                  |
| Canvas     | IIIF-Presentation internal id | `99/0/Canvas_u8f96nwv4cc07sf15iu1ot`                                                     |                                                                                                                  |
| Collection | IIIF-Presentation internal id | `99/0/Collection_suo102ig12zhcqe6inw945`                                                 |                                                                                                                  |
| Range      | Normalised Id of range        | `99/0/Range_bcdloifk0j1zgp1cvlga8v_iiif.io_api_cookbook_recipe_0024-book-4-toc_range_r0` | Range id was `https://iiif.io/api/cookbook/recipe/0024-book-4-toc/range/r0` on Manifest `bcdloifk0j1zgp1cvlga8v` |

Manifest, Collection and Canvas are first-class citizens in IIIF-Presentation, we have internal identifiers for all of these types that are unique to a customer so we can use these as is. 
Outside of these we will need to normalise the incoming `id`, applying scope with Manifest or Collection id prefix. 
We shouldn't need to reverse engineer the stored asset id to find what the original id was, having a repeatable deterministic method for taking an `id` and generating the normalised form will be enough. When building a final Manifest we will have the incoming/staged Manifest and NamedQuery results; we need a predictable method of taking ids from the former and finding any representation in the latter.

> [!CAUTION]
> We will need to be aware of asset id length restrictions. Do we need to do something different to normalise? Possibly encode or hash the url?

It could be useful to use one of the metadata fields to store the originating `id` value - it's unlikely that we'd need it but it could prove useful in the future. Alternatively we could use this value as a 'fake' `"origin"` if that field will be required.

## NamedQuery Results

> Generated NamedQuery Manifests will output 'stub' Assets alongside normal assets, IIIF-Presentation will need to arrange them appropriately.

A Manifest or Collection can have any of: Assets, Asset-level adjuncts and IIIF-level adjuncts. It can also have standard IIIF properties where adjuncts would be added (e.g. a Canvas can have "seeAlso", or a Manifest could have a "rendering").

We have rules for handling Assets and how these are added to the final Manifest.

For adjuncts we need a set of rules to determine how we generate final Manifests from NQs:
* Where do we place asset adjuncts if those assets make up a choice or composite?
* How do we handle the existence of adjunct target properties (e.g. if we have 2 `"seeAlso"` adjuncts but there are already 3 `"seeAlso"` resources)? Append? Is that safe? Could we end up with duplicates?

## Returned Payloads

> Requests with show-extras headers will output `adjuncts` at relevant location.

If IIIF resources have associated adjuncts then we need to render these if the show-extras headers are supplied.

They will be output to the same location as they were supplied (ie they can't all be in a single "adjuncts" array, must be added at the appropriate level). The `CanvasPaintings`-like table can be used to assist in this construction.

## Other Changes or Considerations

* Collection changes. To date Collection handling is much simpler than Manifests - they are always JSON only. Now that they may contain adjuncts we will need to extend handling of Collections, e.g.:
  * Asynchronous processing due to adjuncts. This would follow the same rules as Manifests, ie return 202 or 200 on update. Returning ETag or not. Background handler completion.
  * Collections can now include Protagonist interactions - can this easily be refactored from Manifests?
  * Addressed above in [Storage and Tracking](#storage-and-tracking), will we need some form of persistence for Collections?
  * `manifest` value for an asset is currently an Id only. Internal ids could be shared between Manifests and Collections, do we need a prefix to differentiate?
* How much asset logic can we use for processing adjuncts? It broadly follows the same steps but may involve fairly deep refactoring.
* If we want to support 'optimised-update' type scenarios then we'll need to handle adjunct batches, as outlined in Protagonist RFC-013. Without those it'll be difficult to identify what manifests require further work.

## Potential Issues

Full implementation will require adding new properties to `iiif-net` nuget package. As we are initially adding these at the top level, using `PresentationManifest` and `PresentationCollection` classes should be fine but we may need a more advanced way of handling arbitrary properties at nested levels (see [iiif-net#62](https://github.com/digirati-co-uk/iiif-net/issues/62))

## Examples

Create new Manifest, PUT to `https://{{presentationUrl}}/99/parent/child`:

```json
{
    "type": "Manifest",
    "parent": "https://{{presentationUrl}}/99/parent",
    "slug": "child",
    "adjuncts": [
        { 
            "id": "mets.xml",
            "origin": "https://hosted.example/manifest/mets.xml",
            "@type": "Dataset",
            "mediaType": "text/xml",
            "iiifLink": "seeAlso"
        }
    ],
    "paintedResources": [
        {
            "asset": {
                "id": "one",
                "origin": "https://origin.example/image",
                "mediaType": "image/jpeg",
                "adjuncts": [
                    { 
                        "id": "page_mets.xml",
                        "origin": "https://hosted.example/image/mets.xml",
                        "@type": "Dataset",
                        "mediaType": "text/xml",
                        "iiifLink": "seeAlso"
                    },
                    {
                        "id": "annotation-page",
                        "origin": "https://origin.example/annotation-page",
                        "@type": "AnnotationPage",
                        "mediaType": "application/json",
                        "label": { "en": [ "Line-level annotations" ] },
                        "iiifLink": "annotations"
                    }
                ]
            }
        }
    ]
}
```

Which creates manifestId mani1234, canvasId canva9876.

Protagonist:
* Create `99/0/man_mani1234`
* Create adjunct `mets.xml` for `99/0/man_mani1234`
* Create `99/101/one` 
* Create adjunct `page_mets.xml` for `99/101/one`
* Create adjunct `annotation-page` for `99/101/one`

---- 

update same Manifest, PUT to `https://{{presentationUrl}}/99/parent/child`:

```json
{
    "type": "Manifest",
    "parent": "https://{{presentationUrl}}/99/parent",
    "slug": "child",
    "adjuncts": [
        { 
            "id": "updated_mets.xml",
            "origin": "https://hosted.example/manifest/mets.xml",
            "@type": "Dataset",
            "mediaType": "text/xml",
            "iiifLink": "seeAlso"
        },
        {
            "id": "annos",
            "origin": "https://origin.example/annotation-page",
            "@type": "AnnotationPage",
            "mediaType": "application/json",
            "label": { "en": [ "Page-level annotations" ] },
            "iiifLink": "annotations"
        }
    ],
    "paintedResources": [
        {
            "asset": {
                "id": "one",
                "origin": "https://origin.example/image",
                "mediaType": "image/jpeg",
                "adjuncts": [
                    {
                        "id": "annotation-page",
                        "origin": "https://origin.example/annotation-page",
                        "@type": "AnnotationPage",
                        "mediaType": "application/json",
                        "label": { "en": [ "Line-level annotations" ] },
                        "iiifLink": "annotations"
                    }
                ]
            }
        }
    ]
}
```

Protagonist:
* Create adjunct `annos` for `99/0/man_mani1234`
* Create adjunct `updated_mets.xml` for `99/0/man_mani1234`
* Delete adjunct `mets.xml` from `99/0/man_mani1234`
* Delete adjunct `page_mets.xml` from `99/101/one`