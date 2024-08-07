using UdapEd.Shared.Search;

namespace UdapEd.Shared.Services.Search
{
    public interface ICapabilityLookup
    {
        List<ResourceToSearchParamMap> SearchParamMap { get; set; }
        Dictionary<string, List<string>> IncludeMap { get; set; }
        Dictionary<string, List<string>> ReverseIncludeMap { get; set; }

        Task Build();
    }
}
