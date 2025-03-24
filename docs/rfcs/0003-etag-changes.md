# Etag changes

This document contains details of how Etags can be mopdified to work better

For context, the current Etag implementation works as follows:

* Resources that return etags are marked with `[ETagCaching]` attribute.
  * This is a `ActionFilterAttribute` that generates the ETag by hashing the response body.
  * After generation the ETag is cached in-memory by request path.
* On subsequent requests the above cache is checked:
  * If not found, the request is rejected
  * If non-matching, request is rejected
  * If matching, request is processed

## Issues

While the current Etag implementation allows for easily adding and removing Etags from resources, there are several issues with the current approach:

- It doesn't scale
  - Resources being tracked in one instance will not be seen in the cache of another instance from scaling.  This would also be non-obvious to a user
- Updating older resources require that a GET be performed first
  - This adds the existing resource to the cache for usage
- Tracking doesn't survive restarts
- Etags change more often than needed
  - When PUTting the same resource, the Etag does change even if the underlying resource doesn't change

## Fixes

### Database storage

Solving this means that hashes should be generated up front and then shared between instances.  This likely means storing a value in the database that can be used to check the hash against.  This would aleviate issues with scaling, Etag values needing to be retrieved beforehand and Etag values not survivng restarts.

Doing this would mean an additional value being added to the database called `etag` that allows for the Etag to be stored.  Given that there are multiple resource types that require updating (manifests and collections) with their own table, this column would likely be best going onto the shared `hierarchy` table to reduce duplication.  Another potential solution is to have another table containing the Etag, but this is probably overkill for thisd single resource.

### Distrtibuted cache

This change would require that a call is made to the database to retrieve the Etag value, which would be slower than the current solution when checking the Etag.  A way to mitigate this would be to use an `IDistributedCache` that can be shared between instances, which would avoid the scaling issues.

In order to use `IDistributedCache`, a backing cache needs to be selected, which could be Redis.

#### Issues

While using a distributed cache there are some problems that could make using one unviable:

- Redis is expensive in AWS
- The record is retrieved anyway on GET, meaning that there'd be essentially no efficiency savings here
- PUT/POST would benefit, but these operations are far less common and only completed by system administrators


### Generating the checksum

If the checksum is now being generarted upfront instead of using the GET response to generate an Etag, there are some other ways of doing this:

#### Using the hash
This is the same way that it currently works, which is fine, but could mean there are differences between a checksum generated from a hierarchical PUT/POST and a flat PUT/POST

There's an issue with using the file to generate the hash in that PUT/POST can occur on the hierarchical path and the flat path which has different styles of request body.  This means that potentially updating a resource could have 2 different file hashes if using the request.  This can also happen with variations of `publicId`, `slug` and `parent`. Given these are ultimately the same request, it would be better for a single value to control the Etag on all of these requests and responses. This could be done using the response instead of the request, but there are issues with this around properties such as `ingesting` which will change based on assets. However, this would likely make for a better value when PATCH is integrated.

##### Questions

- Is it worth worrying about differences in the request or response?  It feels like just using the latest PUT/POST would be unique enough
 
#### Leveraging S3

- S3 has the ability to generate a file hash we could take advantage of
- This has issues with storage collections, as these aren't stored in S3 - we could use a different system for generating this hash, but then that means there is a different code journey in this specific scenario, which has caused issues with other parts of the presentation API
- The S3 document updates at some point in the future when there are Assets attached to a manifest (i.e.: staging to live) - dealing with this would add complexity

#### Generated Etag

The final option is to not use the request/response to generate an Etag, but instead to use something like the id generator to generate a random identifier for an etag.  This could avoid issues with differences in request/response but create potential problems around collisions.  However, this might not be an issue as it's unlikely to occur and even if it doid, there would be a very low chance of using these commmon identifiers to update different resources

### Other considerations

based on the [API scratchpad tests](https://github.com/dlcs/iiif-presentation-tests/blob/04213f185bf4fb370855e7e37be27ee4587234bf/tests/apiscratch/t0071-update-managed-asset-manifest.spec.ts#L60-L62) shows that editable resources (i.e.: PUT/POST) should not respond with Etags and instead would require a GET to retrieve the Etag.  This would have implications on the automated tests.

The presentation API [sends Cache-Control: no-cache headers](https://github.com/dlcs/iiif-presentation/issues/140) to stop clients from caching values and then serving old Etags.  As it stands, this shouldn't have an impact on how the Etags work