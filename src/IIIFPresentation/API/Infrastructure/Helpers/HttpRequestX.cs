﻿using API.Auth;

namespace API.Infrastructure.Helpers;

public static class HttpRequestX
{
    private static readonly KeyValuePair<string, string> AdditionalPropertiesHeader = new ("X-IIIF-CS-Show-Extras", "All");

    public static bool ShowExtraProperties(this HttpRequest request)
    {
        return request.Headers.FirstOrDefault(x => x.Key == AdditionalPropertiesHeader.Key).Value == AdditionalPropertiesHeader.Value &&
               Authorizer.CheckAuthorized(request);
    }
    
    public static bool HasShowExtraHeader(this HttpRequest request)
    {
        return request.Headers.FirstOrDefault(x => x.Key == AdditionalPropertiesHeader.Key).Value ==
               AdditionalPropertiesHeader.Value;
    }
}