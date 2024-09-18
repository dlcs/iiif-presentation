namespace API.Features.Storage.Helpers;

public static class OrderByHelper
{
    public static List<string> AllowedOrderByFields =>
    [
        "id",
        "slug",
        "created"
    ];
}