# Batch Completion Handling

see [Revisit Batches](https://github.com/dlcs/protagonist/pull/929/files#diff-0d380a6db64580407caa9f4adf06d996bda15e74e4781bc048b2ebf4da8e7b74) for the RFC on the Protagonist side of this work

Once a batch is completed by Protagonist, there are a series of steps that need to be completed that update the manifest with information from the completed images.

This RFC outlines how the following will be accomplished:

- Retrieve details from the DLCS Orchestrator
- Update the manifest and database with details from the DLCS

## Full process

This is an example of how this batch completion process works end to end

```mermaid
---
title: Batch completion process end to end
---
sequenceDiagram
    actor User
    participant P as Presentation
    participant D as IIIF cloud services
    participant PU as Publisher
    participant SU as Subscriber
    participant S as Manifest storage
    User ->+ P: Presentation request wih assets
    P -> P: Generate DLCS asset requests and initial manifest
    P -->> S: Store manifest
    P -->>+ D: Upload assets in batch request
    D -->> P: Response
    P ->>- User: Presentation request Response
    D --> D: Complete images
    D -->>- PU: Notify batch completion
    PU -->> SU: Publish message
    loop Until message recieved
    P -->> SU: Listen for batch completion
    end
    SU -->> P: Get batch completion
    P -->>+ D: Retrieve asset details
    D -->>- P: 
    P -->> S: Update stored manifest
    P --> P: Update database
    User ->>+ P: Request IIIF manifest
    P ->>- User: Response
```

## Batch completion process

Updating the details of a batch requires a call to orchestrator to add the `Image-Service` and thumbnails.  Additionally, details are retrieved from an initial S3 staging location, which includes details of the manifest that are not saved in the database or are not returned by the DLCS (such as `homepage`)

This process can be demonstrated below:

```mermaid
---
title: Batch completion process
---
sequenceDiagram
    participant NH as Notification Handler 
    participant P as Background Handler
    participant SS as S3 Staging
    participant DO as DLCS Orchestrator
    participant SL as S3 live
    NH ->> P: Notify batch completion
    P -> P: Check all batches are complete for the manifest
    P ->> SS: retrieve skeleton manifest 
    P ->>+ DO: Named query projection on the batch
    DO -->>- P: Get batch details from named query
    P --> P: Update manifest
    P -->> SL: Update S3
    P --> P: Update database
    P -->> SS: delete skeleton manifest
```

Addditional processing within batch completion is to generate IIIF multiple choice constructs from painted resources and merging thumbnails.

see [this protagonist RFC](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/019-presentation-dlcs.md) for details of how the querying assets work in protagonist
