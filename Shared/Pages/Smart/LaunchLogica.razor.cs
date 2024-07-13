using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;
using Org.BouncyCastle.Pkcs;
using UdapEd.Shared.Components;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Smart;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Http;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace UdapEd.Shared.Pages.Smart;

public partial class LaunchLogica
{
    // private string _requestClientId = "udaped-client";
    private readonly string _requestClientId = "12377c4d-72f8-4ff5-93d2-ff0ffd26317b";
    // private string? _launchEhrOpenidProfilePatient = "launch-ehr openid profile patient/*.*";
    private string? _launchEhrOpenidProfilePatient = "patient/*.read launch openid";
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] private NavigationManager NavManager { get; set; } = null!;
    [Inject] private IDiscoveryService MetadataService { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject] private HttpClient HttpClient { get; set; } = null!;
    public string SmartSessionCapabilityStatement { get; set; }
    public string SmartSessionWellknownMetadata { get; set; }
    public string CapabilityStatement { get; set; }
    public string SmartMetadata { get; set; }
    public string AuthCode { get; set; }
    public string StateCode { get; set; }
    private string RedirectUrl { get; set; }
    public string Token { get; set; }

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

        var uri = NavManager.ToAbsoluteUri(NavManager.Uri);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        var launch = queryParams.GetValueOrDefault("launch");
        var code = queryParams.GetValueOrDefault("code");

        if (!launch.IsNullOrEmpty())
        {
            SmartSessionCapabilityStatement = await GetFhirMetadata();
            SmartSessionWellknownMetadata = await GetSmartWellknownMetadata();
        }

        if (!code.IsNullOrEmpty())
        {
            AuthCode = code;
            StateCode = queryParams.GetValueOrDefault("state");
            RedirectUrl = uri.AbsolutePath;
        }
    }

    

    private async Task<string> GetFhirMetadata()
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
            CapabilityStatement = JsonSerializer.Serialize(capabilityStatement, new JsonSerializerOptions { WriteIndented = true });

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
            //var clients = AppState.ClientRegistrations?.Registrations.Where(r => r.Value?.ResourceServer == iss);
            //Pick your client
            //first one for now.
            //var client = clients?.FirstOrDefault().Value;

            var client = new ClientRegistration();
            client.ClientId = _requestClientId;
            client.Scope = _launchEhrOpenidProfilePatient;

            var builder = new QueryStringBuilder(oAuthUris.Authorization)
                .Add("client_id", client?.ClientId ?? string.Empty)
                .Add("response_type", "code")
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
                // CapabilityStatement = capabilityStatement,
                AuthCodeUrlWithQueryString = HttpUtility.UrlDecode(builder.Build(false))
            };

            AppState.SetProperty(this, nameof(AppState.SmartSession), session, true, false);

            var smartContext = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            return smartContext.Replace("\\u002B", "+").Replace("\\u0026", "\r\n\t&").Replace("?", "\r\n\t?");
        }

        return uri.Query.Replace("&", "&\r\n");
    }

    private async Task<string> GetSmartWellknownMetadata()
    {
        var uri = NavManager.ToAbsoluteUri(NavManager.Uri);

        if (!string.IsNullOrEmpty(uri.Query))
        {
            var queryParams = QueryHelpers.ParseQuery(uri.Query);
            var iss = queryParams.GetValueOrDefault("iss").FirstOrDefault();
            var metadataUrl = $"{iss}/.well-known/smart-configuration";
            var launch = queryParams.GetValueOrDefault("launch");

            if (iss.IsNullOrEmpty())
            {
                throw new MissingFieldException("Missing iis parameter");
            }

            if (launch.IsNullOrEmpty())
            {
                throw new MissingFieldException("Missing launch parameter");
            }

            var smartMetadata = await MetadataService.GetSmartMetadata(metadataUrl, default);
            SmartMetadata = JsonSerializer.Serialize(smartMetadata, new JsonSerializerOptions { WriteIndented = true });

            if (smartMetadata == null)
            {
                throw new MissingFieldException($"Missing SMART Metadata at {metadataUrl}");
            }

            if (smartMetadata.authorization_endpoint.IsNullOrEmpty() || smartMetadata.token_endpoint.IsNullOrEmpty())
            {
                return "Missing OAuthUris";
            }

            var redirectUri = $"{NavManager.BaseUri}smart/launchBP";
            var state = Guid.NewGuid().ToString();

            // find client registered for the launching EHR
            //var clients = AppState.ClientRegistrations?.Registrations.Where(r => r.Value?.ResourceServer == iss);
            //Pick your client
            //first one for now.
            //var client = clients?.FirstOrDefault().Value;

            var client = new ClientRegistration();
            client.ClientId = _requestClientId;
            client.Scope = _launchEhrOpenidProfilePatient;
            var (challenge, verifier) = Pkce.Generate();

            var builder = new QueryStringBuilder(smartMetadata.authorization_endpoint)
                .Add("client_id", client?.ClientId ?? string.Empty)
                .Add("response_type", "code")
                .Add("scope", client?.Scope ?? string.Empty)
                .Add("redirect_uri", "http://localhost:5171/Smart/launchBP")
                .Add("aud", iss!)
                .Add("launch", launch!)
                .Add("state", state)
                .Add("code_challenge", challenge)
                .Add("code_challenge_method", "S256");

            var session = new SmartSession(state)
            {
                ServiceUri = iss,
                RedirectUri = redirectUri,
                TokenUri = smartMetadata.token_endpoint,
                // CapabilityStatement = capabilityStatement,
                AuthCodeUrlWithQueryString = builder.Build()
            };

            AppState.SetProperty(this, nameof(AppState.SmartSession), session, true, false);

            var smartContext = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            return smartContext.Replace("\\u002B", "+").Replace("\\u0026", "\r\n\t&").Replace("?", "\r\n\t?");
        }

        return uri.Query.Replace("&", "&\r\n");
    }

    private async Task SmartAuth()
    {
        Console.WriteLine(AppState.SmartSession?.AuthCodeUrlWithQueryString);
        //Authorize
        await JSRuntime.InvokeVoidAsync("open", AppState.SmartSession?.AuthCodeUrlWithQueryString, "_self");
    }

    private async Task RequestToken()
    {
        var request = new ProtocolRequest();
        request.RequestUri = new Uri(AppState.SmartSession?.TokenUri);
        request.ClientId = _requestClientId;
        request.ClientCredentialStyle = ClientCredentialStyle.PostBody;
        request.Parameters.AddRequired(OidcConstants.TokenRequest.Code, AuthCode);
        request.Parameters.AddRequired(OidcConstants.TokenRequest.GrantType, "authorization_code");
        request.Parameters.AddRequired(OidcConstants.TokenRequest.RedirectUri, "http://localhost:5171/Smart/launchBP");
        request.Parameters.AddRequired(OidcConstants.TokenRequest.CodeVerifier, "joe");
        request.Prepare();
        request.Method = HttpMethod.Post;

        var response = await ((HttpMessageInvoker)HttpClient).SendAsync(request, default).ConfigureAwait(false);

        var tokenResponse = await ProtocolResponse.FromHttpResponseAsync<TokenResponse>(response).ConfigureAwait(false);

        var tokenResponseModel = new TokenResponseModel
        {
            Raw = tokenResponse.Json.AsJson(),
            IsError = tokenResponse.IsError,
            Error = tokenResponse.Error,
            AccessToken = tokenResponse.AccessToken,
            IdentityToken = tokenResponse.IdentityToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            Scope = tokenResponse.Raw,
            TokenType = tokenResponse.TokenType,
            Headers = JsonSerializer.Serialize(
                tokenResponse.HttpResponse.Headers,
                new JsonSerializerOptions { WriteIndented = true })
        };

        AppState.SetProperty(this, nameof(AppState.AccessTokens), tokenResponseModel, true, false);
        AppState.SetProperty(this, nameof(AppState.BaseUrl), AppState.SmartSession?.ServiceUri);
        //Token = JsonExtensions.FormatJson(await response.Content.ReadAsStringAsync());
        Token = JsonExtensions.FormatJson(JsonSerializer.Serialize(tokenResponse));

        if (!tokenResponseModel.IsError)
        {
            await HttpClient.PutAsJsonAsync("Metadata/Token", tokenResponseModel.AccessToken);
        }


        // var fhirClient = new HttpClient();
        // fhirClient.SetBearerToken(tokenResponseModel.AccessToken);
        //
        // var joe = fhirClient.GetStringAsync(new Uri("https://fhir.test.localhost/Patient"));
    }

}


