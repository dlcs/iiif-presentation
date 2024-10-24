﻿using API.Converters;
using API.Helpers;
using Core.Helpers;
using IIIF.Presentation.V3.Content;
using Models.API.Collection;
using Models.Database.General;
using Collection = Models.Database.Collections.Collection;

namespace API.Features.Storage.Helpers;

public static class PresentationCollectionX
{
    /// <summary>
    /// Enriches a presentation collection with additional fields from the database
    /// </summary>
    /// <param name="presentationCollection">The presentation collection to enrich</param>
    /// <param name="collection">The collection to use for enrichment</param>
    /// <param name="urlRoots">URL details</param>
    /// <param name="pageSize">Size of the page</param>
    /// <param name="currentPage">The current page of items</param>
    /// <param name="totalItems">The total number of items</param>
    /// <param name="items">The list of items that use this collection as a </param>
    /// <param name="orderQueryParam">Used to describe the type of ordering done</param>
    /// <returns>An enriched presentation collection</returns>
    public static PresentationCollection EnrichPresentationCollection(this PresentationCollection presentationCollection, 
    Collection collection, UrlRoots urlRoots, int pageSize, int currentPage, 
    int totalItems, List<Hierarchy>? items, string? orderQueryParam = null)
    {
        var totalPages = CollectionConverter.GenerateTotalPages(pageSize, totalItems);

        var orderQueryParamConverted = string.IsNullOrEmpty(orderQueryParam) ? string.Empty : $"&{orderQueryParam}";
        var hierarchy = collection.Hierarchy!.Single(h => h.Canonical);
        
        presentationCollection.Context ??= CollectionConverter.GenerateContext();
        presentationCollection.Behavior ??= CollectionConverter.GenerateBehavior(collection);
        presentationCollection.Slug ??= hierarchy.Slug;
        presentationCollection.ItemsOrder ??= hierarchy.ItemsOrder;
        presentationCollection.Items ??= CollectionConverter.GenerateItems(urlRoots, items);
        presentationCollection.TotalItems = totalItems;

        presentationCollection.Id = collection.GenerateFlatCollectionId(urlRoots);
        presentationCollection.PublicId = collection.GenerateHierarchicalCollectionId(urlRoots);
        presentationCollection.Parent = CollectionConverter.GeneratePresentationCollectionParent(urlRoots, hierarchy);
        presentationCollection.PartOf = CollectionConverter.GeneratePartOf(hierarchy, collection, urlRoots);
        presentationCollection.View = CollectionConverter.GenerateView(collection, urlRoots, pageSize, currentPage,
            totalPages, orderQueryParamConverted);
        presentationCollection.SeeAlso = CollectionConverter.GenerateSeeAlso(collection, urlRoots);
        presentationCollection.Created = collection.Created.Floor(DateTimeX.Precision.Second);
        presentationCollection.Modified = collection.Modified.Floor(DateTimeX.Precision.Second);
        presentationCollection.CreatedBy = collection.CreatedBy;
        presentationCollection.ModifiedBy = collection.ModifiedBy;
        
        return presentationCollection;
    }
    
    /// <summary>
    /// Sets the thumbnail for a presentation collection
    /// </summary>
    /// <param name="collection">The collection to set a thumbnail for</param>
    /// <returns>
    /// A response showing whether there were errors in the conversion, and a string of the converted collection
    /// </returns>
    public static string? GetThumbnail(this PresentationCollection collection)
    {
        if (collection.Thumbnail is List<ExternalResource> thumbnailsAsCollection)
        {
            var thumbnails = thumbnailsAsCollection.OfType<Image>().ToList();
            return thumbnails.GetThumbnailPath();
        }

        return collection.PresentationThumbnail;
    }
}