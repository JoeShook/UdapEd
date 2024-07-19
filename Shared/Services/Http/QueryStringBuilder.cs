using System.Web;

namespace UdapEd.Shared.Services.Http;

public class QueryStringBuilder(string url)
{
    public List<KeyValuePair<string, string>> Params { get; set; } = new List<KeyValuePair<string, string>>();

    public QueryStringBuilder Add(string key, string value)
    {
        Params.Add(new KeyValuePair<string, string>(key, value));
        return this;
    }
    
    public string Build(bool encode = true)
    {
        if (!Params.Any())
        {
            return url;
        }

        var qs = Params.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}");
        var ps = $"{string.Join("&", qs)}";
        var q = $"{url}?{ps}";

        return q;
    }
}
