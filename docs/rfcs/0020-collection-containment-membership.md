# IIIF and Storage Collections - containment and membership

> See [t0030-storage-collection-items](https://github.com/dlcs/iiif-presentation-tests/blob/api-scratchpad/tests/apiscratch/t0030-storage-collection-items.spec.ts) and [-create-iiif-collection](https://github.com/dlcs/iiif-presentation-tests/blob/api-scratchpad/tests/apiscratch/t0090---create-iiif-collection.spec.ts) for accompanying Playwright examples.

Our Storage Collections are containers - they _contain_ resources in the same way a directory in a file system contains its files. It's where the files are, it's where they are stored. IIIF-CS is a IIIF Repository, and Storage Collections allow for a hierarchical container structure to organise IIIF resources sensibly, and give those resources nice hierarchical paths.

When we expose a storage collection as a public, vanilla IIIF Collection, its contained resources are exposed as the `items` property. This is like a directory listing of the immediate child Manifests and Collections.

A IIIF Collection [as specified](https://iiif.io/api/presentation/3.0/#51-collection) has no notion of containment - IIIF is not a spec for a repository or platform, it's a data model. The `items` in a IIIF Collection are _members_ - they are references to any IIIF Collection or IIIF Manifest anywhere.

When our storage collection is exposed as a public IIIF Collection with an `items` property, we are saying that its child contained items are the **members** of the IIIF Collection. Client consumers are only interested in this. For presentation purposes, containment is **irrelevant**. 

For content management and URL design, containment is important.

When you create a IIIF Collection in IIIF-CS, you can:

 - POST or PUT child resources into it (**containment** - it acts like a folder)
 - Edit the JSON of its `items` property to assert a **membership** relationship with any IIIF resource on the web (including other resources in IIIF-CS)

The exact same need for both containment and membership in resources that group things together (i.e., directories/folders, storage Collections, IIIF Collections) affects the Linked Data Platform:

https://www.w3.org/TR/ldp/#ldpc

> _The contents of a container is defined by a set of triples in its representation (and state) called the containment triples that follow a fixed pattern. Additional types of containers allow for the set of members of a container to be defined by a set of triples in its representation called the membership triples that follow a consistent pattern._

The Linked Data Platform is complicated and we don't want to do it like that (Fedora implements it, and is one of the reasons why we have created a simplified Storage API to use instead - we have no need for any concept of membership there).

Suppose we create a IIIF Collection to represent a three volume printed edition of _The Lord of the Rings_ (LOTR). Each volume will be a separate Manifest. For both content management and URL design, these three Manifests are contained by the LOTR IIIF Collection:

 - https://iiif.dlcs.io/99/fiction/lotr
 - https://iiif.dlcs.io/99/fiction/lotr/fellowship
 - https://iiif.dlcs.io/99/fiction/lotr/two-towers
 - https://iiif.dlcs.io/99/fiction/lotr/return

We have created these three Manifests and put them into the LOTR Collection.
The platform generates an `items` property on the public collection for us.

Then we want to add a fourth volume to the public IIIF `items`, but we don't want to host it, we don't need or want to create and store a fourth Manifest, it already exists somewhere else:

https://example.org/fiction/the-last-ringbearer

In Linked Data Platform terms, we want to be able to say (in pseudo-RDF)

```
<lotr> <contains> <fellowship>
<lotr> <contains> <two-towers>
<lotr> <contains> <return>

<lotr> <hasMember> <fellowship>
<lotr> <hasMember> <two-towers>
<lotr> <hasMember> <return>
<lotr> <hasMember> <the-last-ringbearer>
```

## `containedItems` to indicate containment

When a Storage Collection or a IIIF Collection is requested with the `X-IIIF-CS-Show-Extras` header, it has both a `containedItems` and an `items` property.

`containedItems` conveys the containment relationship, and `items` conveys the membership relationship, just as it does in regular IIIF.

This means that the `items` property of an API Storage Collection or an API IIIF Collection is always the `items` property of its public IIIF representation.

It also means that API clients wishing to allow users to **browse the repository storage** just follow `containedItems` and render them as browseable "folders". This is not the same as navigating the IIIF Collections as you would in a viewer, following membership links.

The `items` property of a Storage Collection is _always_ generated dynamically and has the same references in it as the `containedItems` property.

> [!CAUTION]
> I am suggesting that `items` has vanilla IIIF reference objects, and `containedItems` has API objects with show-extras properties. So even though for a Storage Collection they have the same list of Manifests and/or Collections, the JSON is different; `containedItems` has fields like `publicId`, but `items` is the public facing vanilla IIIF.

The `items` property of a IIIF Collection is generated dynamically as you add more child resources to it as a container, but is **independently editable**.

For both types of Collection, `containedItems` is dynamically generated from the database only. It is not editable as JSON, only through PUT, POST and DELETE operations that change its contained resources.

For a IIIF Collection, we want two things simultaneously that MAY be at odds.

 * we want the `items` property to reflect whatever we want it to, including externally hosted resources, or IIIF resources from elsewhere in our repository
 * we need to know the containment parent-child relationship so we can see what's in the repository (e.g., navigate a tree in the portal) and have nice hierarchical paths for stored resources in Collections

For the first three volumes of LOTR at least, the `items` from containment is in agreement with the membership `items`, even though the JSON is different (and their `id` values are different (?) - public vs flat?)

For a IIIF Collection, the `items` property is editable as JSON and can be modified without necessarily conflicting with `containedItems`:

* You can let the platform add items to it as you add more child resources to the Collection.
* You can save the IIIF Collection without an `items` property at all and it will leave it untouched.
* You can save the IIIF Collection with an `items` property that doesn't change its members (you can re-order them though)
* You can even add arbitrary extra valid IIIF - the JSON is what gets saved - as long as the members of `items` match the contained child IIIF resources (regardless of order there is a 1:1 match).

However if you POST an explicit `items` property that conflicts:

 * it references a IIIF resource that is not a repository/containment child of the Collection
 * it omits one or more of the contained child items

...then we say the `items` and `containedItems` have **diverged**. I don't think we need to explicitly track this though, just notice it when a Manifest or Collection gets saved.

> [!NOTE]
> The `containedItems` property is generated by the hierarchical DB relationship. And the `items` property is just another "JSON is King" thing to be respected and stored whether we understand all of it or not. As long as it's a valid list, we can still append to it when a new containment relationship is added.

There is some redundancy in that Storage Collections have `items` and `containedItems` that assert the same set of resources, but there is still a useful difference - one is the public `items` and one is not.

> [!NOTE]
> I have a vague feeling that this might do away with the distinction between IIIF and Storage Collections...? Everything is a IIIF Collection and the two properties are in sync - until they aren't. `items` becomes just another JSON property that we persist, except that we attempt to non-destructively edit it when there is a containment change (addition or deletion via PUT, POST or DELETE of a resource).

The `containedItems` property should use the thumbnail property stored in the DB to generate containment navigation UI in the portal (and other management tools that speak the Extras API). This is why the thumbnail in the DB is a direct URL of an image rather than a service reference. No need for a thumbnail image service:


```json
{
   "containedItems": [
      {
         "id": "https://iiif.dlcs.io/7/manifests/abcd",
         "type": "Manifest",
         "label": { "en": [ "Fellowship"] },
         "thumbnail": [ { "id": "https://dlcs.io/thumbs/7/99/fs/full/!100,100/0/default.jpg", "type": "Image" } ]
      }
   ]
}
```

(The string "https://dlcs.io/thumbs/7/99/fs/full/!100,100/0/default.jpg" is what we stored in the DB for this Manifest, it just happens to be an Image Service URI). That's all it ever needs, I think, to drive repository-browsing UI. That thumb is the exact string from the DB row, no need to compute it or look it up. Public IIIF never sees this.

For API callers who stick to vanilla IIIF only (no HTTP operations on flat API, pure vanilla IIIF payloads) this model _just works_ - although they never see a `containedItems` property, their `items` will be auto-generated as far as possible, and they can modify the JSON if they want to.



Public view of Storage Collection `items` are always just expanded versions of the above (e.g., with image service on the thumbnail, maybe...) (???)

Public view of IIIF Collections `items` are sometimes expanded versions of the above, and will be until you do something to change it, but could be absolutely any list of Manifests and Collections anywhere:

 * things that are actually contained too (most common)
 * external things
 * things that are managed by IIIF-CS but are in a different location in the repository (member, but not contained)

> items == membership
> containedItems == repository structure/storage

The `containedItems` JSON above is always entirely generate-able from the DB

Also, the Manifest Editor is not actually interested in `containedItems`, that's not what someone is editing when working on a Manifest. `containedItems` is modified by adding or deleting other IIIF resources _in the repository_ - which may or may not be something you do from the ME and is anyway a secondary function. In the Portal, it's the primary function, with simplified Manifest-building a convenience.

## Difficulties

After I've edited the items JSON and added the external reference to `<last-ringbearer>`, my containedItems and items have diverged.
if I then POST this for storage (containment) in the LOTR collection:

```
POST /.../lotr
{ "label": { "en": [ "Official Vol 4" ],  ... (rest of Manifest) }
```

* Does IIIF-CS tack this onto the end of items (new membership assertion)
* ...or does it say "no, they have diverged, it's your responsibility now"

> [INFO]
> Current conclusion - it adds it to the end. And similarly if you DELETE a contained resource, the server will modify the parent container's `items` property to remove it (because it no longer exists).

--- 

I have no evidence for this ofc because we have nobody using it - but I suspect it will be rare for a IIIF Coll in the platform to run into this. Either they'll be like our 3-vol LOTR and the membership is the same as the containment, or they'll be ad-hoc collections - bookmarks, curated sets, whatever - that have no containedItems at all.)

eg at Wellcome - they'll be Multiple Volume printed books, OR they will be "books by Darwin" - just a set of references.

They wouldn't be a set of refs BUT I also just happened to store this copy of The Descent of Man directly in there. You could do that but it would be strange.

The trouble is, I don't think we can just say "if it's going to be rare then don't allow it".
IIIF Colls in DLCS really are both membership sets and containment sets, and that is super-powerful, so we need defined behaviour when being both.
