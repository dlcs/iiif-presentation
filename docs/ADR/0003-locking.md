# Locking
* Status: decided
* Author: Jack Lewis
* Decider: Jack Lewis
* Date: 2026-06-10

## Context and problem statement

When a manifest is ingested through the API there's the possibility of same manifest being "double-submitted" by a caller.  This causes major issues with processing as it can mean batches are double-submitted that means manifests are unable to be processed by the background handler.  [This has occasionally been seen in the live environment](https://github.com/dlcs/iiif-presentation/issues/507).  As such, some way of locking manifests from being processed twice needs to be implemented.

There are a couple of scenarios where this issue can occur:

1. Concurrent requests through the API (essentially execute a second call with the same values while waiting for the first to complete)
2. Resending the same request while the manifest is waiting for processing in the background handler, but a response has been received from the API for the first request

The second is fairly easy to solve by essentially checking if the manifest is still ingesting, and if it is respond with a `409 Conflict` response instead of accepting the manifest.  However, the first is more complex as it requires a solution to lock a manifest from being processed after the first attempt has been made.

## Decision drivers

- Make the locking function reusable le through a library or shared class
- Locking needs to be controlled in a thread-safe manner

## Considered options

- Use a library like `AsyncKeyedLock`
- Create an implementation of a locking library
- Implement REDIS or a shared cache

## Decision outcome

Final decision was to write an implementation of a locking library so that it is internalized within iiif-presentation.  This allowed for greater flexibility than the other options, while being the most targeted approach for our needs.

The specific implementation of locking can be found in the `ManifestLockManager` class.

It was decided to roll our own due to the bug report.  While the bug didn't affect the current use-case and there was a workaround, the potential of adding a library with a known bug could have caused issues in the future if used in that specific case.  By contrast, REDIS was decided not to be used as it provides an additional burden of supporting infrastructure for something that isn't used yet.

### Limitations

It needs to be noted, that this locking functionality is in-memory and thus cannot be extended to multiple running instances of the API, unlike using REDIS.  This means that if the load balancer is set to share load across multiple instances of the API, it's possible for this to happen again due to API 2 not knowing that API 2 has locked the resource for processing.

This functionality was decided to not be required for now as all production instances of the API currently only run a single instance.  However, this decision should be revisited if requirement changes and REDIS or similar should be implemented.

## Pros and cons of the options

### Locking library

- Many users of the library provide better support
- A number of additional functions that could be useful in the future
- Updates are handled by another team
- The specific library initially used had a major bug that meant locking didn't work in a specific case


### Implementation

- Focused only on the current needs of the project
- Can be extended to support REDIS etc. in the future
- flexible for needed future modifications
- Must be maintained by the team (albeit minor)

### REDIS

- Allows for multiple instances of the API to run at the same time
- Most widely used, so fully tested in production
- Most complex implementation due to crossing system boundaries
- Requires maintaining more infrastructure