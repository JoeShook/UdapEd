using Hl7.Fhir.Model;
using Newtonsoft.Json;
using System.Web;
using Udap.CdsHooks.Model;
using T = System.Threading.Tasks;

namespace UdapEd.Shared.Services.Cds
{
    public interface IServiceExchange
    {
        T.Task RemapSmartLinksAsync(
            Action<string> dispatch,
            CdsResponse cardResponse,
            string fhirAccessToken,
            string patientId,
            string fhirServerUrl);

        string EncodeUriParameters(string template);

        string CompletePrefetchTemplate(State state, Dictionary<string, string> prefetch);
    }

    public class ServiceExchange : IServiceExchange
    {
        private readonly HttpClient _httpClient;

        public ServiceExchange(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async T.Task RemapSmartLinksAsync(
            Action<string> dispatch,
            CdsResponse cardResponse,
            string fhirAccessToken,
            string patientId,
            string fhirServerUrl)
        {
            var links = cardResponse?.Cards?.SelectMany(card => card.Links ?? new List<CdsLink>())
                .Where(link => link.Type == "smart");

            foreach (var link in links)
            {
                try
                {
                    var newLink = await RetrieveLaunchContextAsync(link, fhirAccessToken, patientId, fhirServerUrl);
                    dispatch(JsonConvert.SerializeObject(newLink));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error retrieving launch context: {e.Message}");
                }
            }
        }

        public string EncodeUriParameters(string template)
        {
            if (!string.IsNullOrEmpty(template) && template.Contains("?"))
            {
                var splitUrl = template.Split('?');
                var queryParams = HttpUtility.ParseQueryString(splitUrl[1]);
                foreach (string param in queryParams)
                {
                    queryParams[param] = Uri.EscapeDataString(queryParams[param]);
                }
                return $"{splitUrl[0]}?{queryParams}";
            }
            return template;
        }

        public string CompletePrefetchTemplate(State state, Dictionary<string, string> prefetch)
        {
            var patient = state.PatientState.CurrentPatient.Id;
            var user = state.PatientState.CurrentUser ?? state.PatientState.DefaultUser;
            var prefetchRequests = new Dictionary<string, string>(prefetch);

            foreach (var prefetchKey in prefetchRequests.Keys.ToList())
            {
                var prefetchTemplate = prefetchRequests[prefetchKey];
                prefetchTemplate = prefetchTemplate.Replace("{context.patientId}", patient)
                                                   .Replace("{context.userId}", user);
                prefetchRequests[prefetchKey] = prefetchTemplate;
            }

            return JsonConvert.SerializeObject(prefetchRequests);
        }

        private async Task<string> RetrieveLaunchContextAsync(CdsLink link, string fhirAccessToken, string patientId, string fhirServerUrl)
        {
            // Implement the logic to retrieve launch context
            // This is a placeholder implementation
            await T.Task.Delay(100); // Simulate async work
            return $"{{\"link\": \"{link.Url}\", \"patientId\": \"{patientId}\"}}";
        }
    }

    public class State
    {
        public PatientState PatientState { get; set; }
    }

    public class PatientState
    {
        public Patient CurrentPatient { get; set; }
        public string CurrentUser { get; set; }
        public string DefaultUser { get; set; }
    }
}
