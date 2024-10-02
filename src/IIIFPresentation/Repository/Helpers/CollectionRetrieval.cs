﻿using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Models.Database.Collections;

namespace Repository.Helpers;

public static class CollectionRetrieval
{
    public static string RetrieveFullPathForCollection(Collection collection, PresentationContext dbContext)
    {
        var query = $@"
WITH RECURSIVE parentsearch AS (
 select
    id,
    parent,
    customer_id,
    created,
    modified,
    created_by,
    modified_by,
    is_public,
    is_storage_collection,
    items_order,
    label,
    locked_by,
    tags,
    thumbnail,
    use_path,
    slug,
    0 AS generation_number
 FROM collections
 WHERE id = '{collection.Id}'
 UNION
 SELECT
    child.id,
    child.parent,
    child.customer_id,
    child.created,
    child.modified,
    child.created_by,
    child.modified_by,
    child.is_public,
    child.is_storage_collection,
    child.items_order,
    child.label,
    child.locked_by,
    child.tags,
    child.thumbnail,
    child.use_path,
    child.slug,
    generation_number+1 AS generation_number
 FROM collections child
     JOIN parentsearch ps ON child.id=ps.parent
     WHERE generation_number <= 1000
)
SELECT * FROM parentsearch ORDER BY generation_number DESC
";
        var parentCollections = dbContext.Collections
            .FromSqlRaw(query)
            .OrderBy(i => i.CustomerId)
            .ToList();

        if (parentCollections.Count >= 1000)
        {
            throw new PresentationException("Parent to child relationship exceeds 1000 records");
        }
        
        var fullPath = string.Join('/', parentCollections
            .Where(parent => !string.IsNullOrEmpty(parent.Parent))
            .Select(parent => parent.Slug));
        
        return fullPath;
    }
}