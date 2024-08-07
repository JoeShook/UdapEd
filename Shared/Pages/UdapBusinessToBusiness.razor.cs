﻿#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using IdentityModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;
using Udap.Common.Extensions;
using Udap.Model;
using UdapEd.Shared.Components;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Pages;

public partial class UdapBusinessToBusiness
{
    [CascadingParameter]
    public CascadingAppState AppState { get; set; } = null!;

    private ClientRegistration? ClientSelectedInUi { get; set; }
    public string ScopeOverride { get; set; } = String.Empty;

    private ErrorBoundary? ErrorBoundary { get; set; }

#if ANDROID || IOS || MACCATALYST || WINDOWS
    [Inject] IExternalWebAuthenticator ExternalWebAuthenticator { get; set; } = null!;
#endif

    [Inject] IAccessService AccessService { get; set; } = null!;
    [Inject] NavigationManager NavManager { get; set; } = null!;
    
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private string? _signingAlgorithm;

    public string SigningAlgorithm
    {
        get
        {
            if (_signingAlgorithm == null && AppState.UdapClientCertificateInfo?.PublicKeyAlgorithm == "RS")
            {
                _signingAlgorithm = UdapConstants.SupportedAlgorithm.RS256;
            }
            if (_signingAlgorithm == null && AppState.UdapClientCertificateInfo?.PublicKeyAlgorithm == "ES")
            {
                _signingAlgorithm = UdapConstants.SupportedAlgorithm.ES256;
            }

            return _signingAlgorithm ?? "Unknown";
        }
        set
        {
            _signingAlgorithm = value;
        }
    }

    private string LoginRedirectLinkText { get; set; } = "Login Redirect";
    
    private string? TokenRequest1 { get; set; }
    private string? TokenRequest2 { get; set; }
    private string? TokenRequest3 { get; set; }
    private string? TokenRequestScope { get; set; }
    private string? TokenRequest4 { get; set; }
    
    private AuthorizationCodeRequest? _authorizationCodeRequest;
    private AuthorizationCodeRequest? AuthorizationCodeRequest {
        get
        {
            if (_authorizationCodeRequest == null)
            {
                _authorizationCodeRequest = AppState.AuthorizationCodeRequest;
            }
            return _authorizationCodeRequest;
        }
        set
        {
            _authorizationCodeRequest = value;
            AppState.SetProperty(this, nameof(AppState.AuthorizationCodeRequest), value);
        }
    }

    private string? _accessToken;

    private string? AccessToken
    {
        get { return _accessToken ??= AppState.AccessTokens?.Raw; }
        set => _accessToken = value;
    }

    private void Reset()
    {
        _signingAlgorithm = null;
        StateHasChanged();
    }

    /// <summary>
    /// Method invoked when the component is ready to start, having received its
    /// initial parameters from its parent in the render tree.
    /// Override this method if you will perform an asynchronous operation and
    /// want the component to refresh when that operation is completed.
    /// </summary>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing any asynchronous operation.</returns>
    protected override async Task OnInitializedAsync()
    {
        await ResetSoftwareStatement();
        
        await base.OnInitializedAsync();
    }

    protected override void OnParametersSet()
    {
        ErrorBoundary?.Recover();
    }

    /// <summary>
    /// GET /authorize?
    ///     response_type=code&
    ///     state=client_random_state&
    ///     client_id=clientIDforResourceHolder&
    ///     scope= resource_scope1+resource_scope2&
    ///     redirect_uri=https://client.example.net/clientredirect HTTP/1.1
    /// Host: resourceholder.example.com
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private async Task BuildAuthCodeRequest()
    {
        AccessToken = string.Empty;
        await AppState.SetPropertyAsync(this, nameof(AppState.AccessTokens), null, true, false);
        AuthorizationCodeRequest = new AuthorizationCodeRequest
        {
            RedirectUri = "Loading..."
        };

        await AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationCodeRequest), AuthorizationCodeRequest, true, false);
        await Task.Delay(250);

        AuthorizationCodeRequest = new AuthorizationCodeRequest
        {
            ResponseType = "response_type=code",
            State = $"state={CryptoRandom.CreateUniqueId()}",
            ClientId = $"client_id={AppState.ClientRegistrations?.SelectedRegistration?.ClientId}",
            Scope = $"scope={AppState.ClientRegistrations?.SelectedRegistration?.Scope?.Replace("udap", "").Replace("  ", " ")}",
            RedirectUri = $"redirect_uri={NavManager.Uri.RemoveQueryParameters().ToPlatformScheme()}",
            Aud = $"aud={AppState.BaseUrl}"
        };

        await AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationCodeRequest), AuthorizationCodeRequest, true, false);

        BuildAuthorizeLink();
    }

    private string AuthCodeRequestLink { get; set; } = string.Empty;
    
    private void BuildAuthorizeLink()
    {
        var sb = new StringBuilder();
        sb.Append(@AppState.MetadataVerificationModel?.UdapServerMetaData?.AuthorizationEndpoint);
        if (@AppState.AuthorizationCodeRequest != null)
        {
            sb.Append("?").Append(@AppState.AuthorizationCodeRequest.ResponseType);
            sb.Append("&").Append(@AppState.AuthorizationCodeRequest.State);
            sb.Append("&").Append(@AppState.AuthorizationCodeRequest.ClientId);
            sb.Append("&").Append(@AppState.AuthorizationCodeRequest.Scope);
            sb.Append("&").Append(@AppState.AuthorizationCodeRequest.RedirectUri);
            sb.Append("&").Append(@AppState.AuthorizationCodeRequest.Aud);
        }

        AuthCodeRequestLink = sb.ToString();
        StateHasChanged();
    }

    private async Task GetAccessCode()
    {
        LoginRedirectLinkText = "Loading...";
        AppState.SetProperty(this, nameof(AppState.AccessCodeRequestResult), null);

        //UI has been changing properties so save it but don't rebind
        AppState.SetProperty(this, nameof(AppState.AuthorizationCodeRequest), AuthorizationCodeRequest, true, false);
        var url = new RequestUrl(AppState.MetadataVerificationModel?.UdapServerMetaData?.AuthorizationEndpoint!);

        var accessCodeRequestUrl = url.AppendParams(
            AppState.AuthorizationCodeRequest?.ClientId,
            AppState.AuthorizationCodeRequest?.ResponseType,
            AppState.AuthorizationCodeRequest?.State,
            AppState.AuthorizationCodeRequest?.Scope,
            AppState.AuthorizationCodeRequest?.RedirectUri,
            AppState.AuthorizationCodeRequest?.Aud);

        //
        // Builds an anchor href link the user clicks to initiate a user login page at the authorization server
        //
        var loginLink = await AccessService.Get(accessCodeRequestUrl);
        
        AppState.SetProperty(this, nameof(AppState.AccessCodeRequestResult), loginLink);
        LoginRedirectLinkText = "Login Redirect";
    }
    
    public string LoginCallback(bool reset = false)
    {
        if (reset)
        {
            return string.Empty;
        }

        var uri = NavManager.ToAbsoluteUri(NavManager.Uri);

        if (!string.IsNullOrEmpty(uri.Query))
        {
            var queryParams = QueryHelpers.ParseQuery(uri.Query);

            var loginCallbackResult = new LoginCallBackResult
            {
                Code = queryParams.GetValueOrDefault("code"),
                Scope = queryParams.GetValueOrDefault("scope"),
                State = queryParams.GetValueOrDefault("state"),
                SessionState = queryParams.GetValueOrDefault("session_state"),
                Issuer = queryParams.GetValueOrDefault("iss")
            };

            AppState.SetProperty(this, nameof(AppState.LoginCallBackResult), loginCallbackResult, true, false);
        }

        return uri.Query.Replace("&", "&\r\n");
    }

    private bool _callBackEntryScrolled = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {   
        var uri = NavManager.ToAbsoluteUri(NavManager.Uri);
        if (!_callBackEntryScrolled && !string.IsNullOrEmpty(uri.Query))
        {
            var queryParams = QueryHelpers.ParseQuery(uri.Query);
            if (!queryParams.GetValueOrDefault("code").ToString().IsNullOrEmpty())
            {
                var success = await JSRuntime.InvokeAsync<bool>("UdapEd.scrollTo", "CallBackEntry");
                _callBackEntryScrolled = success;
            }
        }
        
    }

    private async Task ResetSoftwareStatement()
    {
        TokenRequest1 = string.Empty;
        TokenRequest2 = string.Empty;
        TokenRequest3 = string.Empty;
        TokenRequest4 = string.Empty;
        await AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationCodeRequest), null);
        LoginCallback(true);
        StateHasChanged();
    }

    private async Task BuildAccessTokenRequest ()
    {
        await ResetSoftwareStatement();
        TokenRequest1 = "Loading ...";
        await Task.Delay(50);

        
        if (string.IsNullOrEmpty(AppState.ClientRegistrations?.SelectedRegistration?.ClientId))
        {
            TokenRequest1 = "Missing ClientId";
            return;
        }

        if (string.IsNullOrEmpty(AppState.MetadataVerificationModel?.UdapServerMetaData?.TokenEndpoint))
        {
            TokenRequest1 = "Missing TokenEndpoint";
            return;
        }

        if (AppState.Oauth2Flow == Oauth2FlowEnum.authorization_code)
        {
            Console.WriteLine("Why");
            var tokenRequestModel = new AuthorizationCodeTokenRequestModel
            {
                ClientId = AppState.ClientRegistrations?.SelectedRegistration?.ClientId,
                TokenEndpointUrl = AppState.MetadataVerificationModel?.UdapServerMetaData?.TokenEndpoint,
            };

            tokenRequestModel.RedirectUrl = NavManager.Uri.RemoveQueryParameters();
            

            if (AppState.LoginCallBackResult?.Code != null)
            {
                tokenRequestModel.Code = AppState.LoginCallBackResult?.Code!;
            }
            
            var requestToken = await AccessService
                .BuildRequestAccessTokenForAuthCode(tokenRequestModel, _signingAlgorithm);
            
            await AppState.SetPropertyAsync(this, nameof(AppState.AuthorizationCodeTokenRequest), requestToken);

            if (AppState.AuthorizationCodeTokenRequest == null)
            {
                TokenRequest1 = "Could not build an access token request";
                TokenRequest2 = string.Empty;
                TokenRequest3 = string.Empty;
                TokenRequest4 = string.Empty;

                return;
            }

            BuildAccessTokenRequestVisualForAuthorizationCode();
        }
        else  //client_credentials
        {
            var tokenRequestModel = new ClientCredentialsTokenRequestModel
            {
                ClientId = AppState.ClientRegistrations?.SelectedRegistration?.ClientId,
                TokenEndpointUrl = AppState.MetadataVerificationModel?.UdapServerMetaData?.TokenEndpoint,
                Scope = ScopeOverride.IsNullOrEmpty() ? TokenRequestScope?.Replace("scope=", "").TrimEnd('&').Trim() : ScopeOverride
            };

            var requestToken = await AccessService
                .BuildRequestAccessTokenForClientCredentials(tokenRequestModel, _signingAlgorithm);

            await AppState.SetPropertyAsync(this, nameof(AppState.ClientCredentialsTokenRequest), requestToken);

            BuildAccessTokenRequestVisualForClientCredentials();
        }
    }

    private void BuildAccessTokenRequestVisualForClientCredentials()
    {
        var sb = new StringBuilder();
        sb.AppendLine("POST /token HTTP/1.1");
        sb.AppendLine("Content-Type: application/x-www-form-urlencoded");
        sb.AppendLine($"Host: {AppState.MetadataVerificationModel?.UdapServerMetaData?.AuthorizationEndpoint}");
        sb.AppendLine("Content-type: application/x-www-form-urlencoded");
        sb.AppendLine();
        sb.AppendLine("grant_type=client_credentials&");
        TokenRequest1 = sb.ToString();

        sb = new StringBuilder();
        sb.AppendLine($"client_assertion_type={OidcConstants.ClientAssertionTypes.JwtBearer}&");
        TokenRequest2 = sb.ToString();

        TokenRequest3 = $"client_assertion={AppState.ClientCredentialsTokenRequest?.ClientAssertion?.Value}&";
        TokenRequestScope = $"scope={(ScopeOverride.IsNullOrEmpty() ? AppState.ClientCredentialsTokenRequest?.Scope : ScopeOverride)}&";
        sb = new StringBuilder();
        sb.Append($"udap={UdapConstants.UdapVersionsSupportedValue}&\r\n");
        TokenRequest4 = sb.ToString();
    }

    private void BuildAccessTokenRequestVisualForAuthorizationCode()
    {
        if (AppState.LoginCallBackResult == null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("POST /token HTTP/1.1");
        sb.AppendLine($"Host: {AppState.MetadataVerificationModel?.UdapServerMetaData?.AuthorizationEndpoint}");
        sb.AppendLine("Content-type: application/x-www-form-urlencoded");
        sb.AppendLine();
        sb.AppendLine("grant_type=authorization_code&");
        TokenRequest1 = sb.ToString();

        sb = new StringBuilder();
        sb.AppendLine($"code={AppState.AuthorizationCodeTokenRequest?.Code}&");
        sb.AppendLine($"client_assertion_type={OidcConstants.ClientAssertionTypes.JwtBearer}&");
        TokenRequest2 = sb.ToString();

        TokenRequest3 =
            $"client_assertion={AppState.AuthorizationCodeTokenRequest?.ClientAssertion?.Value}&\r\n";

        sb = new StringBuilder();
        sb.AppendLine($"redirect_uri={NavManager.Uri.RemoveQueryParameters()}");
        sb.Append($"udap={UdapConstants.UdapVersionsSupportedValue}");
        TokenRequest4 = sb.ToString();
        
    }

    private async Task GetAccessToken()
    {
        try
        {
            AccessToken = "Loading ...";
            await Task.Delay(150);

            if (AppState.Oauth2Flow == Oauth2FlowEnum.authorization_code)
            {
                if (AppState.AuthorizationCodeTokenRequest == null)
                {
                    AccessToken = "Missing prerequisites.";
                    return;
                }

                var tokenResponse = await AccessService
                    .RequestAccessTokenForAuthorizationCode(
                        AppState.AuthorizationCodeTokenRequest);

                await AppState.SetPropertyAsync(this, nameof(AppState.AccessTokens), tokenResponse);
                await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);

                AccessToken = tokenResponse is { IsError: false } ? tokenResponse.Raw : tokenResponse?.Error;
            }
            else //client_credentials
            {
                if (AppState.ClientCredentialsTokenRequest == null)
                {
                    AccessToken = "Missing prerequisites.";
                    return;
                }

                var tokenResponse = await AccessService
                    .RequestAccessTokenForClientCredentials(
                        AppState.ClientCredentialsTokenRequest);

                await AppState.SetPropertyAsync(this, nameof(AppState.AccessTokens), tokenResponse);
                await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);

                AccessToken = tokenResponse is { IsError: false }
                    ? tokenResponse.Raw 
                    : $"Failed:\r\n\r\n{tokenResponse?.Error}\r\n{tokenResponse?.Headers}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex);
        }
    }

    private async Task LaunchAuthorize()
    {
        BuildAuthorizeLink();
      
#if ANDROID || IOS || MACCATALYST || WINDOWS

        var result = await ExternalWebAuthenticator.AuthenticateAsync(AuthCodeRequestLink, NavManager.Uri.RemoveQueryParameters().ToPlatformScheme());

        var loginCallbackResult = new LoginCallBackResult
        {
            Code = result.Properties.GetValueOrDefault("code"),
            Scope = result.Properties.GetValueOrDefault("scope"),
            State = result.Properties.GetValueOrDefault("state"),
            SessionState = result.Properties.GetValueOrDefault("session_state"),
            Issuer = result.Properties.GetValueOrDefault("iss")
        };
        await AppState.SetPropertyAsync(this, nameof(AppState.LoginCallBackResult), loginCallbackResult, true, false);

        var sb = new StringBuilder();
        foreach (var resultProperty in result.Properties)
        {
            sb.AppendLine($"{resultProperty.Key}={resultProperty.Value}");
        }
        _webAuthenticorResponseProps = sb.ToString();

#else
        await JSRuntime.InvokeVoidAsync("open", @AuthCodeRequestLink, "_self");
#endif

    }

    public string DeviceLoginCallback(bool reset = false)
    {
        if (reset)
        {
            return string.Empty;
        }

        return _webAuthenticorResponseProps;
    }

    private string _webAuthenticorResponseProps = string.Empty;

    private string? GetJwtHeader(string? tokenString)
    {
        if (string.IsNullOrEmpty(tokenString))
        {
            return string.Empty;
        }

        var jwt = new JwtSecurityToken(tokenString);
        return JsonExtensions.FormatJson(Base64UrlEncoder.Decode(jwt.EncodedHeader));
    }

    private IDictionary<string, ClientRegistration?> FilterRegistrations()
    {
        return AppState.ClientRegistrations.Registrations
            .Where(r => r.Value != null && 
                        AppState.UdapClientCertificateInfo != null &&
                        AppState.UdapClientCertificateInfo.SubjectAltNames.Contains(r.Value.SubjAltName) &&
                        AppState.BaseUrl == r.Value.ResourceServer)
            .ToImmutableDictionary();
    }
}