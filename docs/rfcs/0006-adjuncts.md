# Adjuncts

Protagonist has support for adding adjuncts to Assets, generated Manifests will then output these onto Asset Canvases. How can we leverage this to add adjuncts to IIIF Presentation resources; such as Manifests, Collections, Canvases, Ranges etc - ultimately any valid resource with an `id`?

> [!NOTE]
> The initial implementation in IIIF Presentation will support adding Canvas (via assets) and Manifest/Collection level adjuncts _only_.

The high level approach will be:
1. When sending a payload to IIIF Presentation, an `adjuncts` property can be added to any supporting property.
2. If added to `asset` property, adjuncts will be ingested to that asset as normal. Else..
3. Presentation will create a 'stub' Asset in DLCS to serve as a placeholder to add adjuncts to.
4. Generated NamedQuery Manifests will output 'stub' Assets alongside normal assets, IIIF-Presentation will need to arrange them appropriately.
5. Requests with show-extras headers will output `adjuncts` at relevant location.

More details of these steps are below:

## IIIF-Presentation 'adjuncts' Property

> When sending a payload to IIIF Presentation, an `adjuncts` property can be added to any supporting property.

This section looks at how we can support `adjuncts` property when creating a Manifest and how these will be handled internally.

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

Allow users to supply an `adjuncts` property to the Manifest. This will allow consumers to associate any supported types (`annotations`, `seeAlso` or `rendering`) at the Manifest level. A sample payload would look like:

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

## Storage and Tracking

> [!CAUTION]
> Few unanswered questions here that will need further discussion.

When rendering the final manifest, we need rules for how adjuncts will be rendered. The options are:
1. Render all adjuncts associated with an asset, regardless of how they have been added (ie directly via Protagonist and/or added to Manifest via IIIF Presentation API)?
2. Render only those adjuncts that have been added to IIIF resources via the IIIF Presentation API?

> [!NOTE]
> The decision here will have a bearing on how we handle removing adjuncts, below. 
> 
> Does the decision differ on whether it's a 'real' or 'stub' asset that they are associated with?

When adjuncts are removed from a manifest - are they actually deleted from Protagonist, or is it a IIIF-Presentation only delete (ie they still exist but aren't rendered on the final Manifest)?

Regardless of which options we choose from above, I suggest we will need some mechanism to track which adjuncts are associated with each IIIF resource, similar to `CanvasPaintings` table. This will track allow us to determine which are new/updated/deleted etc without making lots of chatty requests to Protagonist.

### Removing Adjuncts

If we have a Manifest that was created with 5 adjuncts, then a subsequent update only contains 2 - how do we remove the 3 adjuncts that are no longer associated with the Manifest?

When an asset is removed from a Manifest the asset is _not_ deleted from Protagonist; instead the `manifests` property is updated, effectively removing the link - the asset still exists in Protagonist and would need manual intervention to delete it. If, for some reason, the `manifests` link is maintained it's not necessarily an issue. IIIF-Presentation maintains a list of which assets are in which Manifests via the `CanvasPaintings` table, so when constructing the final manifest only the "known" assets from the returned NamedQuery are used - any extraneous assets are ignored. See [Protagonist RFC-019](https://github.com/dlcs/protagonist/blob/develop/docs/rfcs/019-presentation-dlcs.md#updating-resources) for more information on this mechanism.

How do we want to handle adjuncts being removed?
1. Issue HTTP DELETE request(s) at request time. When reconciling Protagonist and IIIF-Presentation; we identify which adjuncts need to be removed we delete them immediately. 
   * This could break the _"serve stale Manifest until it's updated"_ logic we use for Manifests, as some rendered links would 404.
   * We run the risk of deleting adjuncts only for the final Manifest to fail, leaving it in an inconsistent state.
   * Would ideally have a bulk delete mechanism to avoid chatty requests.
2. IIIF-Presentation stores which adjuncts are attached to which IIIF resources. When rendering the final Manifest this list is used to decide which adjuncts should be omitted.
   * This can either render _all_ adjuncts, with the exception of those that were removed from previous payload (ie freshly deleted) OR only render adjuncts that we know about.
3. HTTP DELETE after the manifest has finished. Record those adjuncts that have been deleted, and use these records to filter out any adjuncts that come back in the NQ. These can then be removed after completing the final Manifest.

#### Suggestion

Current thinking is the best approach will be (this only addresses adjuncts, assets continue as-is):

1. When a Manifest is created, store adjuncts details in db. Create adjunct batch, on receipt of 'batch completed' finish the Manifest and output all adjuncts.
2. When Manifest updated, store which adjuncts are created and deleted in db. Do not delete the 'deleted' adjuncts yet, we need to know they were removed. Create adjunct batch, on receipt of 'batch completed' notification:
   * Output all adjuncts _unless_ they have just been deleted.
   * HTTP DELETE all those that were just deleted, after successful completion. _This is different behaviour from assets - is that okay?_

## "Stub" Assets

> Presentation will create a 'stub' Asset in DLCS to serve as a placeholder to add adjuncts to.

Protagonist already has support for efficiently ingesting, hosting and delivering adjuncts. Duplicating this behaviour in IIIF-Presentation doesn't seem like a good use of time, particularly when factoring in the need to duplicate logic like origin strategies and access control.

The main problem is that we need an asset to add adjuncts to. Using the above [manifest-level](#manifest-level) example payload, we don't have any assets to add these adjuncts to. All we have is a manifest (and, later, other resources). The solution for this is to add a 'stub' asset - it won't ever contain binary content itself, it's only there to serve as a placeholder to add adjuncts to.

The exact requirements for how this is acheived will be documented in Protagonist RFC-013.

To summarise, we will have an AssetId that is `{customer}/{space}/{asset}`, where:
* Customer is the current customer
* Space is _always_ 0.
* Asset is the identifier of the IIIF resource the adjuncts are being attached to. 

Regardless of what IIIF resource we're creating a stub asset for, we will always need to set the `manifest` column for retrieval later.

### Examples

The initial implementation of adjuncts will only support adding them to Manifest + Collections (and Canvases via Assets) but we should ensure the chosen approach will support further types. The `asset` identifier we use will depend on the type of resource the adjunct is being associated with - we need to be able to identify, for a given stub asset's adjunct, which IIIF resource the adjunct is for. 

Suggested format for the `asset` part of stub asset id is: `{type_prefix}_{resource_identifier}`, where:

| Resource   | `type_prefix` | `resource_identifier`         | Example                                                         | Remarks                                                                     |
| ---------- | ------------- | ----------------------------- | --------------------------------------------------------------- | --------------------------------------------------------------------------- |
| Manifest   | man           | IIIF-Presentation internal id | `99/0/man_bcdloifk0j1zgp1cvlga8v`                               |                                                                             |
| Canvas     | cnv           | IIIF-Presentation internal id | `99/0/cnv_u8f96nwv4cc07sf15iu1ot`                               |                                                                             |
| Collection | col           | IIIF-Presentation internal id | `99/0/col_suo102ig12zhcqe6inw945`                               |                                                                             |
| Range      | rng           | Normalised Id of range        | `99/0/rng_iiif.io_api_cookbook_recipe_0024-book-4-toc_range_r0` | Range id was `https://iiif.io/api/cookbook/recipe/0024-book-4-toc/range/r0` |

> [!NOTE]
> The `type_prefix` is deliberately always 3 characters to make it easy to parse - we don't need to look for everything after the first `_`, it'll always be the 4th character on.

Manifest, Collection and Canvas are first-class citizens in IIIF-Presentation, we have internal identifiers for all of these types that we can use. Outside of these we will need to normalise the incoming `id`. We shouldn't need to reverse engineer the stored asset id to find what the original id was, having a repeatable asymmetric method for taking an `id` and generating the normalised form will be enough to match and `id` an existing assetId. When building a final Manifest we will have the incoming/staged Manifest and NamedQuery results; we need a predictable method of taking ids from the former and finding any representation in the latter.

> [!CAUTION]
> We will need to be aware of asset id length restrictions. Do we need to do something different to normalise? Possibly encode or hash the url?

It likely makes sense to use one of the metadata fields to store the originating `id` value - it's unlikely that we'd need it but it could prove useful in the future. Alternatively we could use this value as a 'fake' `"origin"` if that field will be required.

## NamedQuery Results

> Generated NamedQuery Manifests will output 'stub' Assets alongside normal assets, IIIF-Presentation will need to arrange them appropriately.

A Manifest can have any of: Assets, Asset-level adjuncts and IIIF-level adjuncts. It can also have standard IIIF properties where adjuncts would be added (e.g. a Canvas can have "seeAlso", or a Manifest could have a "rendering").

We have rules for handling Assets and how these are added to the final Manifest.

For adjuncts we need a set of rules to determine how we generate final Manifests from NQs:
* Where do we place asset adjuncts if those assets make up a choice or composite?
* How do we handle the existence of adjunct target properties? Append? Is that safe? Could we end up with duplicates?

## Returned Payloads

> Requests with show-extras headers will output `adjuncts` at relevant location.

If IIIF resources have associated adjuncts then we need to render these if the show-extras headers are supplied.

They will be output to the same location as they were supplied (ie they can't all be in a single "adjuncts" array, must be added at the appropriate level). The `CanvasPaintings`-like table can be used to assist in this construction.

## Other Changes or Considerations

* Collections may require asynchronous processing for adjuncts. This would follow the same rules as Manifests.
* How much asset logic can we use for processing adjuncts? It broadly follows the same steps but may involve fairly deep refactoring.
* If we want to support 'optimised-update' type scenarios then we'll need to handle adjunct batches, as outlined in Protagonist RFC-013. Without those it'll be difficult to identify what manifests require further work.

## Potential Issues

This will require adding new properties to `iiif-net` nuget package. As we are initially adding these at the top level, using `PresentationManifest` and `PresentationCollection` classes should be fine but we may need a more advanced way of handling arbitrary properties at nested levels (see [iiif-net#62](https://github.com/digirati-co-uk/iiif-net/issues/62))

### Examples

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
    ]
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

Database:

| Id              | AssetId           | IIIFResourceId | State   |
| --------------- | ----------------- | -------------- | ------- |
| mets.xml        | 99/0/man_mani1234 | mani1234       | Created |
| page_mets.xml   | 99/101/one        | canva9876      | Created |
| annotation-page | 99/101/one        | canva9876      | Created |

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
    ]
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

Database:
| Id               | AssetId           | IIIFResourceId | State   |
| ---------------- | ----------------- | -------------- | ------- |
| mets.xml         | 99/0/man_mani1234 | mani1234       | Deleted |
| updated_mets.xml | 99/0/man_mani1234 | mani1234       | Created |
| annos            | 99/0/man_mani1234 | mani1234       | Created |
| page_mets.xml    | 99/101/one        | canva9876      | Deleted |
| annotation-page  | 99/101/one        | canva9876      | Updated |

----
When "batch completed" notifications received and Manifest is completed the following HTTP requests will be made to tidy up Manifest

* DELETE `/customers/99/spaces/0/images/man_mani1234/adjuncts/mets.xml`
* DELETE `/customers/99/spaces/101/images/one/adjuncts/page_mets.xml`

Then delete corresponding rows in DB to end up with

| Id               | AssetId           | IIIFResourceId | State   |
| ---------------- | ----------------- | -------------- | ------- |
| updated_mets.xml | 99/0/man_mani1234 | mani1234       | Created |
| annos            | 99/0/man_mani1234 | mani1234       | Created |
| annotation-page  | 99/101/one        | canva9876      | Updated |