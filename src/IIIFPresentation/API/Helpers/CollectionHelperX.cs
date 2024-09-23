﻿using API.Converters;
using API.Features.Storage.Helpers;
using Core.Helpers;
using Models.Database.Collections;

namespace API.Helpers;

/// <summary>
/// Collection of helpers to generate paths etc. for collections
/// </summary>
public static class CollectionHelperX
{
    public static string GenerateHierarchicalCollectionId(this Collection collection, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{collection.CustomerId}{(string.IsNullOrEmpty(collection.FullPath) ? string.Empty : $"/{collection.FullPath}")}";
    
    public static string GenerateFlatCollectionId(this Collection collection, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{collection.CustomerId}/collections/{collection.Id}";
    
    public static string GenerateFlatCollectionParent(this Collection collection, UrlRoots urlRoots) =>
        $"{urlRoots.BaseUrl}/{collection.CustomerId}/collections/{collection.Parent}";
    
    public static string GenerateFlatCollectionViewId(this Collection collection, UrlRoots urlRoots, 
        int currentPage, int pageSize, string orderQueryParam) =>
        $"{collection.GenerateFlatCollectionId(urlRoots)}?page={currentPage}&pageSize={pageSize}{orderQueryParam}";

    public static Uri GenerateFlatCollectionViewNext(this Collection collection, UrlRoots urlRoots,
        int currentPage, int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page={currentPage + 1}&pageSize={pageSize}{orderQueryParam}");
    
    public static Uri GenerateFlatCollectionViewPrevious(this Collection collection, UrlRoots urlRoots, 
        int currentPage, int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page={currentPage - 1}&pageSize={pageSize}{orderQueryParam}");
    
    public static Uri GenerateFlatCollectionViewFirst(this Collection collection, UrlRoots urlRoots, 
        int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page=1&pageSize={pageSize}{orderQueryParam}");
    
    public static Uri GenerateFlatCollectionViewLast(this Collection collection, UrlRoots urlRoots, 
        int lastPage, int pageSize, string orderQueryParam) =>
        new(
            $"{collection.GenerateFlatCollectionId(urlRoots)}?page={lastPage}&pageSize={pageSize}{orderQueryParam}");
    
    public static string GenerateFullPath(this Collection collection, string itemSlug) => 
        $"{(collection.Parent != null ? $"{collection.Slug}/" : string.Empty)}{itemSlug}";
}
