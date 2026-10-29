namespace SESMT.Tests.Common;

public static class HttpResponseExtensions
{
    public static string GetAllSetCookies(this HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("; ", values)
            : string.Empty;
}