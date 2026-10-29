using Microsoft.AspNetCore.Http;

namespace SESMT.Tests.Common;

/// <summary>
/// Implementação simples de IRequestCookieCollection para uso em testes
/// unitários (o ASP.NET Core não expõe RequestCookieCollection publicamente).
/// </summary>
internal sealed class FakeRequestCookies : IRequestCookieCollection
{
    private readonly Dictionary<string, string> _cookies;

    public FakeRequestCookies(IDictionary<string, string> cookies)
        => _cookies = new Dictionary<string, string>(cookies, StringComparer.Ordinal);

    public string? this[string key]
        => _cookies.TryGetValue(key, out var value) ? value : null;

    public int Count => _cookies.Count;
    public IEnumerable<string> Keys => _cookies.Keys;

    ICollection<string> IRequestCookieCollection.Keys => throw new NotImplementedException();

    public bool ContainsKey(string key) => _cookies.ContainsKey(key);

    public bool TryGetValue(string key, out string value)
        => _cookies.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _cookies.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
