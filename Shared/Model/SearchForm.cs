namespace UdapEd.Shared.Model;

public class SearchForm
{
    public required string Url { get; set; }
    public required string Resource { get; set; }
    public required IList<string> FormUrlEncoded { get; set; }
}