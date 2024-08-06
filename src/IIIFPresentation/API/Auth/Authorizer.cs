namespace API.Auth;

public static class Authorizer
{
    public static bool CheckAuthorized(HttpRequest request)
    {
        return request.Headers.Authorization.Count > 0;
    }
}