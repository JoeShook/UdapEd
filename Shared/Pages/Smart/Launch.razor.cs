using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using UdapEd.Shared.Components;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Smart;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Http;

namespace UdapEd.Shared.Pages.Smart;

public partial class Launch
{
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] private NavigationManager NavManager { get; set; } = null!;
    [Inject] private IDiscoveryService MetadataService { get; set; } = null!;
    public string Metadata { get; set; }
    public string SmartMetadata { get; set; }

    /// <summary>
    /// Method invoked when the component is ready to start, having received its
    /// initial parameters from its parent in the render tree.
    /// Override this method if you will perform an asynchronous operation and
    /// want the component to refresh when that operation is completed.
    /// </summary>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing any asynchronous operation.</returns>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        Metadata = await GetMetadata();
    }

    private async Task<string> GetMetadata()
    {
        var uri = NavManager.ToAbsoluteUri(NavManager.Uri);

        if (!string.IsNullOrEmpty(uri.Query))
        {
            var queryParams = QueryHelpers.ParseQuery(uri.Query);
            var iss = queryParams.GetValueOrDefault("iss");
            var metadataUrl = $"{iss}/metadata";
            var launch = queryParams.GetValueOrDefault("launch");

            if (iss.IsNullOrEmpty())
            {
                throw new MissingFieldException("Missing iis parameter");
            }

            if (launch.IsNullOrEmpty())
            {
                throw new MissingFieldException("Missing launch parameter");
            }

            var capabilityStatement = await MetadataService.GetCapabilityStatement(metadataUrl, default);

            if (capabilityStatement == null)
            {
                throw new MissingFieldException($"Missing CapabilityStatement at {metadataUrl}");
            }

            var oAuthUris = capabilityStatement.GetOAuthUris();

            if (oAuthUris.Authorization.IsNullOrEmpty() || oAuthUris.Token.IsNullOrEmpty())
            {
                return "Missing OAuthUris";
                // var smartConfig = await MetadataService.GetSmartMetadata(iss!, default);
                // oAuthUris = smartConfig.GetOAuthUris();
            }

            var redirectUri = NavManager.BaseUri;
            var state = Guid.NewGuid().ToString();

            // find client registered for the launching EHR
            var clients = AppState.ClientRegistrations?.Registrations.Where(r => r.Value?.ResourceServer == iss);
            //Pick your client
            //first one for now.
            var client = clients?.FirstOrDefault().Value;
            
            var builder = new QueryStringBuilder(oAuthUris.Authorization)
                .Add("response_type", "code")
                .Add("client_id", client?.ClientId ?? string.Empty)
                .Add("scope", client?.Scope ?? string.Empty)
                .Add("redirect_uri", redirectUri)
                .Add("aud", iss!)
                .Add("launch", launch!)
                .Add("state", state);

            var session = new SmartSession(state)
            {
                ServiceUri = iss,
                RedirectUri = redirectUri,
                TokenUri = oAuthUris.Token,
                CapabilityStatement = capabilityStatement,
                AuthCodeUrlWithQueryString = builder.Build()
            };


            var loginCallbackResult = new LoginCallBackResult
            {
                Code = queryParams.GetValueOrDefault("iss"),
                Scope = queryParams.GetValueOrDefault("scope"),
                State = queryParams.GetValueOrDefault("state"),
                SessionState = queryParams.GetValueOrDefault("session_state"),
                Issuer = queryParams.GetValueOrDefault("iss")
            };

            AppState.SetProperty(this, nameof(AppState.SmartSession), session, true, false);
        }

        return uri.Query.Replace("&", "&\r\n");
    }
}