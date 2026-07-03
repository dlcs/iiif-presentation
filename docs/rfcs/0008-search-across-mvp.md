# Search Across MVP

Manifests can utilise [Text Services](0007-text-services.md) and pipelines to allow searching within an individual Manifest via the [IIIF Content Search API](https://iiif.io/api/search/2.0/). This document
outlines how we can support a customer/tenant searching across multiple resources.

> [!NOTE]
> This document outlines a simple MVP approach initially - this will be further expanded and enhanced in the future.

## Outline

A high level overview of the approach is:

* Search will be run against the root collection, available to authenticated users only.
* The capability will be advertised as a new `"service"` on the root collection.
* It will search all label values of every resource in the root collection, regardless of nesting depth.
* A synthetic collection will be returned supporting paging.

### Search Endpoint

> * Search will be run against the root collection.

Search will be available at a new endpoint: `GET /{customerId}/collections/{collectionId}/search`. Search criteria and paging controls will be specified by query parameters.

For the initial MVP:
* The only accepted `{collectionId}` will be "root", searching across _all_ Manifests and Collections in the root collection - regardless of depth.
* `label` query parameter will contain the search criteria.

In addition to the above we can support standard query parameters available for all storage collections, namely `page`, `pageSize`, `orderBy`, `orderByDescending`

Example queries
* `https://presentation-api.example/99/collections/root/search?label=medicine`
* `https://presentation-api.example/99/collections/root/search?label=19th+century+novels&pageSize=10&page=2`

### New Search Service 

> * The capability will be advertised as a new `"service"` on the root collection.

Any storage-collections that support search-across will have a `"service"` block added. The custom IIIF-Presentation `@context` will define this, e.g.

```jsonc
{
    "@context": [
        "http://tbc.org/iiif-repository/1/context.json",
        "http://iiif.io/api/presentation/3/context.json"
    ],
    "id": "https://presentation-api.example/99/collections/root",
    "type": "Collection",
    "totals": {
        "childStorageCollections": 10,
        "childIIIFCollections": 5,
        "childManifests": 65
    },
    "behavior": [
        "public-iiif",
        "storage-collection"
    ],
    "service": [
        {
            "id": "https://presentation-api.example/99/collections/root/search",
            "type": "IIIFCS-Search",
            "profile": "level0"
        }
    ]
}
```

Where:
* `"id"` is the endpoint for searching
* `"type"` defines the custom IIIFCS Search
* `"level0"` outlines that this is basic label search only.

### Label Search

> * It will search all label values of every resource in the root collection, regardless of nesting depth.

Manifest and Collection `"label"` is stored in DB as jsonb. The provided `?label=` search term will be used to find matching label **values**, regardless of language.

Searches should be case-insensitive and not rely on the order that values appear in labels. If multiple values are supplied, boolean _AND_ logic is used.

E.g. searching "Hunter Thompson" should find matches like _"Hunter Thompson"_, _"Thompson, Hunter"_ and _"Hunter S. Thompson"_ but not _"Emma Thompson"_ or _"Photograph of a hunter"_.

#### Implementation

> [!WARNING]
> The `collection` and `manifest` have different underlying data-types. 
> 
> The former is `jsonb`, the latter `text` - they should both be the same type (`jsonb`).

> [!NOTE]
> The below are query examples and might not prove to be the most efficient.

##### Simple

An initial, simple implementation of search can use basic `ILIKE` across all values per label. If search terms are multi-word they are split into individual tokens so word order doesn't matter, e.g.

```sql
SELECT DISTINCT 'collection' AS source_type, c.*
FROM collections c,
     LATERAL jsonb_each(c.label) AS kv(lang, arr),
     LATERAL jsonb_array_elements_text(arr) AS val
WHERE val ILIKE '%hunter%'
  AND val ILIKE '%thompson%'
UNION ALL
SELECT DISTINCT 'manifest' AS source_type, m.*
FROM manifests m,
     LATERAL jsonb_each(m.label) AS kv(lang, arr),
     LATERAL jsonb_array_elements_text(arr) AS val
WHERE val ILIKE '%hunter%'
  AND val ILIKE '%thompson%'
ORDER BY created --?orderBy/orderByDescending
LIMIT 100; --?pageSize
```

Downside of this approach is that indexes won't be used so performance will need to be monitored. A standard jsonb GIN index would not accelerate `ILIKE` search as it only supports exact key/value queries.

#### Advanced

A more advanced approach would be to use trigram word search [`pg_trgm`](https://www.postgresql.org/docs/14/pgtrgm.html), or Levenshtein [`fuzzystrmatch`](https://www.postgresql.org/docs/14/fuzzystrmatch.html) matching, both can have different weightings to improve search results. The former seems like it would be a better fit as it would handle missing/reordered tokens better. This, in addition to a [GIN index](https://www.postgresql.org/docs/14/gin.html), should provide more efficient querying.

Both `pg_trgm` and `fuzzystrmatch` work on `text`, rather than `jsonb` data. To handle `jsonb` we would need to pre-process, one option would be to create a new generated column containing the flattened values. This new column can be indexed to allow for efficient searching, it would also improve the above `ILIKE` queries if we wanted to keep this same approach.

We may also want to look at [`unaccent`](https://www.postgresql.org/docs/14/unaccent.html) extension to handle labels containing accented characters.

### Return Object

> * A synthetic collection will be returned supporting paging.

The return type will continue to be a Collection with the custom IIIF-Presentation properties (`slug`, `parent` etc). The `"items"` property will contain search results 
with `"id"`, `"label"`, `"type"` and `"behavior"` for Collections. The `"view"` property will contain a `"PartialCollectionView"` to indicate whether there are more results available.

```jsonc
{
    "@context": [
        "http://tbc.org/iiif-repository/1/context.json",
        "http://iiif.io/api/presentation/3/context.json"
    ],
    "id": "https://presentation-api.example/99/collections/root",
    "type": "Collection",
    "behavior": [
        "public-iiif",
        "storage-collection"
    ],
    "service": [
        {
            "id": "https://presentation-api.example/99/collections/root/search",
            "type": "IIIFCS-Search",
            "profile": "level0"
        }
    ],
    "items": [ // search-results
        {
            "id": "https://presentation-api.example/99/collections/j470l40y3v195536nt0",
            "type": "Collection",
            "label": {
                "en": [
                    "Text I was looking for"
                ]
            },
            "behavior": [
                "public-iiif",
                "storage-collection"
            ]
        },
        {
            "id": "https://presentation-api.example/99/manifests/890seiouhdsdss",
            "type": "Manifest",
            "label": {
                "en": [
                    "More text"
                ]
            }
        }
    ],
    "view": {
        "@id": "https://presentation-api.example/99/collections/root/search?label=text&page=1&pageSize=100",
        "@type": "PartialCollectionView",
        "page": 1,
        "pageSize": 100,
        "totalPages": 1
    },
}
```

#### Future Improvements / Other Notes

General implementation notes
* We should impose a sensible minimum number of characters (e.g. 3) before a search is run to avoid searches for `"a"` returning large numbers of results.
* Searches made with less than minimum number of characters return a 400.
* Searches that find no results return a 200 with an empty collection.

Possible future improvements that can be explored:
* Searching within any storage-collection. Add the new `"service"` to any storage-collection to support query from that level down.
  * This could result in fairly expensive queries as we'd need to work out all descendants for each node and filter these from queries.
* Searching beyond `"label"`. IIIF `"metadata"` or IIIF-Presentation `tags` could be used.
* More complex matching, see [Advanced](#advanced) section, above.
* Alternative technology to handle more complex searches, such as Typesense, OpenSearch, ElasticSearch etc.