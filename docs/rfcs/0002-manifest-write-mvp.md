# Manifest Create and Update (MVP)

This document outlines Manifest create and update steps for manifests that contain `paintedResources`.

> [!Important]
> This outlines the **MVP** version only, this will be iterated on and updated in future RFC

Differences between MVP and full implementation:
* MVP will _always_ ingest assets to IIIF-CS. This means update for MVP is essentially a recreate operation, there won't be any logic to check whether the provided `paintedResources` have contain reordered canvases, new assets, deleted assets etc
  * This means we can use the `batch` named query parameter as every image in a manifest will be part of a batch that IIIF-P knows about.
* Full implementation will detect changes and only ingest what's required.
  * This means we _can't_ use `batch` and need to use `manifest` named query parameter instead, see [Protagonist RFC 019](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/019-presentation-dlcs.md)
* MVP doesn't support mixing `"paintedResources"` and `"items"` properties 

The below sequence diagrams outline the flow for various stages of processing:

## Manifest Create (MVP)

Basic creation, PUT or POST for a manifest that doesn't exist.

Check for existence omitted for brevity.

```mermaid
---
title: Manifest Create (MVP)
---
sequenceDiagram
    actor U as User
    participant P as IIIF Presentation
    participant D as IIIF CloudServices
    participant DB as Database
    participant S as S3

    U->>+P: PUT|POST /2/manifests/abc123
    P->>+D: POST /customer/2/queue
    D-->>-P: HTTP 200, Hydra Collection
    P->P:Build 'skeleton' content-resources in manifest
    P->>DB: Store paintedResources
    note over D,S: Manifest written to 'staging' area
    P->>S: PUT manifest to /staging/manifests/2/abc123
    P->>+D: GET /customer/2/allImages
    D-->>-P: HTTP 200, Hydra Collection
    note over P: Return PresentationManifest - skeleton and "assets"
    P-->>-U: HTTP 202 Accepted
```

## Manifest Update (MVP)

Update operation. As detailed above this is essentially a 'recreate' operation. 

The key difference is that some `paintedResources` may be deleted from DB as part of update but essentially everything else is the same as update.

```mermaid
---
title: Manifest Update (MVP)
---
sequenceDiagram
    actor User
    participant P as IIIF Presentation
    participant D as IIIF CloudServices
    participant DB as Database
    participant S as S3

    User->>+P: Presentation request with assets
    note right of P: Batch contains ALL assets
    P->>+D: POST /customer/2/queue
    D-->>-P: HTTP 200, Hydra Collection
    P->P:Build 'skeleton' content-resources in manifest
    note over P,DB: Load paintedResources, merge with<br>incoming and allow EF change tracker to<br>determine DB work to do
    DB-->>P: Load paintedResources
    P->>DB: Store paintedResources
    note over D,S: Manifest written to 'staging' area<br>Ensures public GET won't 404
    P->>S: PUT manifest to /staging/manifests/2/{manifest-id}
    P->>+D: GET /customer/2/allImages
    D-->>-P: HTTP 200, Hydra Collection
    note over P: Return PresentationManifest - skeleton and "assets"
    P-->>-User: HTTP 202 Accepted
```

## Batch Completion

The below diagram is simplified as it shows IIIF-CS directly notifying IIIF-P of batch completion, whereas this is done via a queue.

A single Manifest can have 1:n batches associated with it, the final manifest generation only happens when _all_ batches are complete.

```mermaid
---
title: Batch Completion (MVP)
---
sequenceDiagram
    participant D as IIIF CloudServices
    participant P as IIIF Presentation
    participant DB as Database
    participant S as S3

    D->>+P: Batch completed notification
    DB->>P: Load Manifest + related
    
    alt batches outstanding
        P->>DB: Mark batch as complete
    else all batches complete
        P->>D: GET /iiif-resource/2/batch-query/{batchIds}
        D-->>P: NQ IIIF Manifest (content-resources)
        S-->>P: GET manifest /staging/manifests/2/{manifest-id}
        P->P:Merge NQ Manifest and staged manifest
        P->>DB: Mark batch + manifest as complete
        note over P,S: Manifest was read from 'staging' area but written to 'real' location
        P->>S: PUT manifest to /manifests/2/{manifest-id}
        P->>-S: DELETE manifest from /staging/manifests/2/{manifest-id}
    end
```

## GET Request Handling

Shows how GET requests are handled and where manifests are delivered from. 

Passing auth and `X-IIIF-CS-Show-Extras` omited but assumed for API requests.

For clarity:
* `public` == Hierarchical Path
* `API` == Flat Path

### Public Path

The Manifest served is _always_ from the 'real' S3 location. 

This may be the current version, or it may be stale as there's a write operation in flight (in which case the 'staging' manifest will exist).

```mermaid
---
title: GET manifest on public path
---
sequenceDiagram
    actor U as User
    participant P as IIIF Presentation
    participant DB as Database
    participant S as S3

    U->>+P: GET /2/path/to/the-manifest

    DB-->>P: Load manifest
    alt manifest never processed
        P-->>U: HTTP 404
    end
    S-->>P: GET /manifests/2/{manifest-id}
    alt manifest not found
        P-->>U: HTTP 404
    end

    note over P,S: Stored manifest-id is flat so need to rewrite to hierarchical
    P->P: Set manifest Id
    P-->>U: HTTP 200, Manifest
```

### API Path

The Manifest served may be from the 'staged' area if it's in-flight, or from the 'real' S3 location if not.


```mermaid
---
title: GET manifest on API path
---
sequenceDiagram
    actor U as User
    participant P as IIIF Presentation
    participant D as IIIF CloudServices
    participant DB as Database
    participant S as S3

    U->>+P: GET /2/manifests/abc123

    DB-->>P: Load manifest

    P->>+D: GET /customer/2/allImages
    D-->>-P: HTTP 200, Hydra Collection

    alt manifest in flight
        S-->>P: GET /staging/manifests/2/abc123
        P-->>U: HTTP 202, Manifest
    else manifest not in flight
        S-->>P: GET /manifests/2/abc123
        P-->>U: HTTP 200, Manifest
    end
```

## Questions

* Is "staged" appropriate name for prefix? Do we need more than a 'simple' prefix - could there be multiple in-flight? (I don't think so).
* Will this concept of "staged" and not help with eventual lock/unlock?