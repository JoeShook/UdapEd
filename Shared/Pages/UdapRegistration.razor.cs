#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;
using System.Text.Json.Nodes;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Udap.Model;
using Udap.Model.Registration;
using UdapEd.Shared.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Registration;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Pages;

public partial class UdapRegistration
{
    private string? SubjectAltName { get; set; }
    private string? _signingAlgorithm;

    private bool TieredOauth
    {
        get => _tieredOauth;
        set
        {
            _tieredOauth = value;
            if (value)
            {
                OpenIdScope = true;
            }
        }
    }

    private bool OpenIdScope { get; set; } = true;
    private bool SmartLaunch { get; set; }
    private bool SmartV1Scopes { get; set; } = true;
    private bool SmartV2Scopes { get; set; }
    public string? IdP { get; set; }
    private string? _requestBody;
    private bool _missingScope;
    private bool _cancelRegistration;
    private UdapDynamicClientRegistrationDocument? _udapDcrDocument;
    private string _localRegisteredClients = string.Empty;
    private string? ScopeLevel { get; set; }
    
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
            Console.WriteLine(value);
            _signingAlgorithm = value;
        }
    }
    [CascadingParameter]
    public CascadingAppState AppState { get; set; } = null!;

    [CascadingParameter] 
    public MainLayout Layout { get; set; } = null!;

    ErrorBoundary? ErrorBoundary { get; set; }
    [Inject] IRegisterService RegisterService { get; set; } = null!;

    [Inject] NavigationManager NavigationManager { get; set; } = null!;

    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    public bool ExactlyFiveMinExp { get; set; } = false;

    private string RawSoftwareStatementError { get; set; } = string.Empty;

    private string _beforeEncodingHeader = string.Empty;
    private string SoftwareStatementBeforeEncodingHeader
    {
        get
        {
            if (!string.IsNullOrEmpty(_beforeEncodingHeader))
            {
                return _beforeEncodingHeader;
            }

            if (AppState.SoftwareStatementBeforeEncoding?.Header == null)
            {
                return _beforeEncodingHeader;
            }

            string? jsonHeader = null;

            try
            {
                jsonHeader = JsonNode.Parse(AppState.SoftwareStatementBeforeEncoding.Header)
                    ?.ToJsonString(
                        // new JsonSerializerOptions()
                        // {
                        //     WriteIndented = true
                        // }
                    ).Replace("\\u002B", "+");
            }
            catch
            {
                // ignored
            }

            return jsonHeader ?? string.Empty;
        }

        set => _beforeEncodingHeader = value;
    }

    
    private string _beforeEncodingStatement = string.Empty;
    private string SoftwareStatementBeforeEncodingSoftwareStatement
    {
        get
        {
            if (!string.IsNullOrEmpty(_beforeEncodingStatement))
            {
                return _beforeEncodingStatement;
            }

            if (AppState.SoftwareStatementBeforeEncoding?.SoftwareStatement == null)
            {
                return _beforeEncodingStatement;
            }

            string? jsonStatement = null;

            try{
                jsonStatement = JsonNode.Parse(AppState.SoftwareStatementBeforeEncoding.SoftwareStatement)
                ?.ToJsonString(new JsonSerializerOptions()
                {
                    WriteIndented = true
                });
            }
            catch
            {
                // ignored
            }

            return jsonStatement ?? string.Empty;
        }

        set => _beforeEncodingStatement = value;
    }

    private const string VALID_STYLE = "pre udap-indent-1";
    private const string INVALID_STYLE = "pre udap-indent-1 jwt-invalid";
    public string ValidRawSoftwareStatementStyle { get; set; } = VALID_STYLE;

    private void Reset()
    {
        _signingAlgorithm = null;
        StateHasChanged();
    }

    private void PersistSoftwareStatement()
    {
        try
        {
            _udapDcrDocument = JsonSerializer
                .Deserialize<UdapDynamicClientRegistrationDocument>(_beforeEncodingStatement);
            var beforeEncodingScope = _udapDcrDocument?.Scope;

            var rawStatement = new RawSoftwareStatementAndHeader
            {
                Header = SoftwareStatementBeforeEncodingHeader,
                SoftwareStatement = _beforeEncodingStatement,
                Scope = beforeEncodingScope
            };

            ValidRawSoftwareStatementStyle = VALID_STYLE;
            AppState.SetProperty(this, nameof(AppState.SoftwareStatementBeforeEncoding), rawStatement);
        }
        catch
        {
            ValidRawSoftwareStatementStyle = INVALID_STYLE;
        }
    }

    private string? _registrationResult;
    private bool _tieredOauth;

    private string RegistrationResult
    {
        get
        {
            if (!string.IsNullOrEmpty(_registrationResult))
            {
                return _registrationResult;
            }

            if (AppState.RegistrationDocument == null)
            {
                return _registrationResult ?? string.Empty;
            }

            return JsonSerializer.Serialize(AppState
                .RegistrationDocument, new JsonSerializerOptions { WriteIndented = true });
        }
        set => _registrationResult = value;
    }


    private Oauth2FlowEnum Oauth2Flow
    {
        get => AppState.Oauth2Flow;
        set => AppState.SetProperty(this, nameof(AppState.Oauth2Flow), value);
    }

    
    private string RequestBody
    {
        get
        {
            if (!string.IsNullOrEmpty(_requestBody))
            {
                return _requestBody;
            }

            if (AppState.UdapRegistrationRequest == null)
            {
                return _requestBody ?? string.Empty;
            }

            return JsonSerializer.Serialize(
                AppState.UdapRegistrationRequest, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        set => _requestBody = value;
    }

    


    private async Task BuildRawSoftwareStatement()
    {
        _cancelRegistration = false;
        SetRawMessage("Loading ...");

        await Task.Delay(150);

        if (AppState.Oauth2Flow == Oauth2FlowEnum.client_credentials)
        {
            await BuildRawSoftwareStatementForClientCredentials();
        }
        else
        {
            await BuildRawSoftwareStatementForAuthorizationCode(
                RegisterService.GetScopesForAuthorizationCode(
                    AppState.MetadataVerificationModel?.UdapServerMetaData?.ScopesSupported, 
                    TieredOauth, 
                    OpenIdScope,
                    ScopeLevel, 
                    SmartLaunch,
                    SmartV1Scopes,
                    SmartV2Scopes));
        }
    }
    private async Task BuildRawCancelSoftwareStatement()
    {
        _cancelRegistration = true;
        SetRawMessage("Loading ...");

        await Task.Delay(150);

        if (AppState.Oauth2Flow == Oauth2FlowEnum.client_credentials)
        {
            await BuildRawSoftwareStatementForClientCredentials(true);
        }
        else
        {
            await BuildRawSoftwareStatementForAuthorizationCode(null, true);
        }
    }

    private async Task BuildRawSoftwareStatementForClientCredentials(bool cancelRegistration = false)
    {
        try
        {
            var dcrBuilder = cancelRegistration ? 
                UdapDcrBuilderForClientCredentialsUnchecked.Cancel() : 
                UdapDcrBuilderForClientCredentialsUnchecked.Create();

            dcrBuilder.WithAudience(AppState.MetadataVerificationModel?.UdapServerMetaData?.RegistrationEndpoint)
                .WithExpiration(TimeSpan.FromMinutes(5))
                .WithJwtId()
                .WithClientName(UdapEdConstants.CLIENT_NAME)
                .WithContacts(new HashSet<string>
                {
                    "mailto:Joseph.Shook@Surescripts.com", "mailto:JoeShook@gmail.com"
                })
                .WithTokenEndpointAuthMethod(UdapConstants.RegistrationDocumentValues.TokenEndpointAuthMethodValue)
                .WithScope(RegisterService.GetScopesForClientCredentials(
                    AppState.MetadataVerificationModel?.UdapServerMetaData?.ScopesSupported,
                    SmartV1Scopes,
                    SmartV2Scopes));

            dcrBuilder.Document.Subject = SubjectAltName;
            dcrBuilder.Document.Issuer = SubjectAltName;
            
            var request = dcrBuilder.Build();

            if (request.Scope == null && request.GrantTypes?.Count > 0)
            {
                _missingScope = true;
            }
            
            var statement = await RegisterService
                .BuildSoftwareStatementForClientCredentials(request, _signingAlgorithm);
            if (statement != null)
            {
                SetRawStatement(statement.Header, statement.SoftwareStatement);
                await AppState.SetPropertyAsync(this, nameof(AppState.SoftwareStatementBeforeEncoding), statement);
            }
        }
        catch (Exception ex)
        {
            SetRawMessage(string.Empty);
            await ResetSoftwareStatement();
            RawSoftwareStatementError = ex.Message;
        }
    }
    
    private async Task BuildRawSoftwareStatementForAuthorizationCode(string? scope, bool cancelRegistration = false)
    {
        try
        {
            var dcrBuilder = cancelRegistration ? 
                UdapDcrBuilderForAuthorizationCodeUnchecked.Cancel() : 
                UdapDcrBuilderForAuthorizationCodeUnchecked.Create();

            var redirectUrl = Oauth2Flow == Oauth2FlowEnum.authorization_code_b2b ?  "udapBusinessToBusiness" : "udapConsumer";
            var redirectUrls = new List<string> { $"{NavigationManager.BaseUri}{redirectUrl}" };
            if (TieredOauth)
            {
                redirectUrls.Add($"{NavigationManager.BaseUri}udapTieredOAuth");
            }

            dcrBuilder.WithAudience(AppState.MetadataVerificationModel?.UdapServerMetaData?.RegistrationEndpoint)
                .WithExpiration(TimeSpan.FromMinutes(5))
                .WithJwtId()
                .WithClientName(UdapEdConstants.CLIENT_NAME)
                .WithContacts(new HashSet<string>
                {
                    "mailto:Joseph.Shook@Surescripts.com", "mailto:JoeShook@gmail.com"
                })
                .WithTokenEndpointAuthMethod(UdapConstants.RegistrationDocumentValues.TokenEndpointAuthMethodValue)
                .WithResponseTypes(new HashSet<string> { "code" })
                .WithRedirectUrls(redirectUrls)
                .WithScope(scope);

            dcrBuilder.Document.Subject = SubjectAltName;
            dcrBuilder.Document.Issuer = SubjectAltName;

            var request = dcrBuilder.Build();

            if (request.Scope == null && request.GrantTypes?.Count > 0)
            {
                _missingScope = true;
            }

            var statement = await RegisterService.BuildSoftwareStatementForAuthorizationCode(request, _signingAlgorithm);
            if (statement?.Header != null)
            {
                SetRawStatement(statement.Header, statement.SoftwareStatement);
                statement.Scope = scope;
            }

            await AppState.SetPropertyAsync(this, nameof(AppState.SoftwareStatementBeforeEncoding), statement);
        }
        catch (Exception ex)
        {
            SetRawMessage(ex.Message);
            await ResetSoftwareStatement();
        }
    }


    private void SetRawMessage(string message)
    {
        RawSoftwareStatementError = string.Empty;
        SoftwareStatementBeforeEncodingHeader = message;
        SoftwareStatementBeforeEncodingSoftwareStatement = string.Empty;
    }

    private void SetRawStatement(string header, string softwareStatement = "")
    {
        
        var jsonHeader = JsonNode.Parse(header)
            ?.ToJsonString(new JsonSerializerOptions()
            {
                WriteIndented = true
            }).Replace("\\u002B", "+");
        
        var jsonStatement = JsonNode.Parse(softwareStatement)
            ?.ToJsonString(new JsonSerializerOptions()
            {
                WriteIndented = true,
                
            });

        if (jsonStatement != null)
        {
            _udapDcrDocument = JsonSerializer.Deserialize<UdapDynamicClientRegistrationDocument>(jsonStatement);
        }
        
        SoftwareStatementBeforeEncodingHeader = jsonHeader ?? string.Empty;
        SoftwareStatementBeforeEncodingSoftwareStatement = jsonStatement ?? string.Empty;
        
    }

    private async Task ResetSoftwareStatement()
    {
        SetRawMessage(string.Empty);
        await AppState.SetPropertyAsync(this, nameof(AppState.SoftwareStatementBeforeEncoding), null);
        _requestBody = null;
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapRegistrationRequest), null);
        _registrationResult = null;
        await AppState.SetPropertyAsync(this, nameof(AppState.RegistrationDocument), null);
    }

    private async Task BuildRequestBody()
    {
        RequestBody = "Loading ...";
        await Task.Delay(50);

        if (AppState.Oauth2Flow == Oauth2FlowEnum.client_credentials)
        {
            await BuildRequestBodyForClientCredentials();
        }
        else
        {
            await BuildRequestBodyForAuthorizationCode();
        }

        RegistrationResult = string.Empty;
        await AppState.SetPropertyAsync(this, nameof(AppState.RegistrationDocument), null);
    }

    private async Task BuildRequestBodyForClientCredentials()
    {
        var registerRequest = await RegisterService
            .BuildRequestBodyForClientCredentials(
                AppState.SoftwareStatementBeforeEncoding,
                _signingAlgorithm);

        await AppState.SetPropertyAsync(this, nameof(AppState.UdapRegistrationRequest), registerRequest);

        RequestBody = JsonSerializer.Serialize(
            registerRequest,
            new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task BuildRequestBodyForAuthorizationCode()
    {
        var registerRequest = await RegisterService
            .BuildRequestBodyForAuthorizationCode(
                AppState.SoftwareStatementBeforeEncoding,
                _signingAlgorithm);

        await AppState.SetPropertyAsync(this, nameof(AppState.UdapRegistrationRequest), registerRequest);

        RequestBody = JsonSerializer.Serialize(
            registerRequest,
            new JsonSerializerOptions { WriteIndented = true });
    }


    private async Task PerformRegistration()
    {
        RegistrationResult = "Loading ...";
        await Task.Delay(50);

        var registrationRequest = new RegistrationRequest
        {
            RegistrationEndpoint = AppState.MetadataVerificationModel?.UdapServerMetaData?.RegistrationEndpoint,
            UdapRegisterRequest = AppState.UdapRegistrationRequest
        };

        var resultModel = await RegisterService.Register(registrationRequest);

        if (resultModel == null)
        {
            RegistrationResult = "Internal failure. Check the logs.";
            return;
        }

        if (resultModel.HttpStatusCode.IsSuccessful() && resultModel.Result != null)
        {
            RegistrationResult = $"HTTP/{resultModel.Version} {(int)resultModel.HttpStatusCode} {resultModel.HttpStatusCode}" +
                                 $"{Environment.NewLine}{Environment.NewLine}"; 
            RegistrationResult += JsonSerializer.Serialize(
                resultModel.Result,
                new JsonSerializerOptions { WriteIndented = true });

            if (_cancelRegistration)
            { // cancel registration
                AppState.ClientRegistrations?.CancelRegistration(resultModel.Result);
            }
            else if (AppState is { BaseUrl: not null, ClientRegistrations: not null })
            {
                if (resultModel.Result.ClientId != null)
                {
                    var registration = AppState.ClientRegistrations.SetRegistration(resultModel.Result, _udapDcrDocument, Oauth2Flow, AppState.BaseUrl);
                    AppState.ClientRegistrations.SelectedRegistration = registration;
                }

                await AppState.SetPropertyAsync(this, nameof(AppState.ClientRegistrations), AppState.ClientRegistrations);
            }

            await AppState.SetPropertyAsync(this, nameof(AppState.RegistrationDocument), resultModel.Result);
        }
        else
        {
            RegistrationResult = $"HTTP/{resultModel.Version} {(int)resultModel.HttpStatusCode} {resultModel.HttpStatusCode}" +
                                 $"{Environment.NewLine}{Environment.NewLine}";
            RegistrationResult += resultModel.ErrorMessage ?? string .Empty;
            
            await AppState.SetPropertyAsync(this, nameof(AppState.RegistrationDocument), null);
        }
    }
    
    protected override void OnParametersSet()
    {
        ErrorBoundary?.Recover();
    }
    
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && AppState.UdapClientCertificateInfo?.SubjectAltNames != null && AppState.UdapClientCertificateInfo.SubjectAltNames.Any())
        {
            SubjectAltName = AppState.UdapClientCertificateInfo?.SubjectAltNames.First();
            StateHasChanged();
        }

        return base.OnAfterRenderAsync(firstRender);
    }

    private async Task ResetLocalRegisteredClients()
    {
        bool confirmed = await JsRuntime.InvokeAsync<bool>("confirm", "Would you like to remove all local registered clients?");
        if (!confirmed)
        {
            return;
        }
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientRegistrations), null);
    }

    private async Task SaveLocalRegisteredClients()
    {
        bool confirmed = await JsRuntime.InvokeAsync<bool>("confirm", "Would you like to update registered clients?");
        if (!confirmed)
        {
            return;
        }
        var localRegisteredClients = JsonSerializer.Deserialize<ClientRegistrations>(_localRegisteredClients);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientRegistrations), localRegisteredClients);
    }

    private string LocalRegisteredClients
    {
        get
        {
            if (string.IsNullOrEmpty(_localRegisteredClients))
            {
                _localRegisteredClients = AppState.ClientRegistrations.AsJson();
            }

            return _localRegisteredClients;
        }
        set => _localRegisteredClients = value;
    }

    private IEnumerable<ClientRegistration?>? CurrentClientRegistrations
    {
        get
        {
            return AppState.ClientRegistrations?.Registrations
                .Where(r => r.Value != null &&
                            AppState.UdapClientCertificateInfo != null &&
                            AppState.UdapClientCertificateInfo.SubjectAltNames.Contains(r.Value.SubjAltName) &&
                            AppState.BaseUrl == r.Value.ResourceServer)
                .Select(r => r.Value);
        }
    }

    private async Task SaveScopesToClient()
    {
        if (AppState.ClientRegistrations.SelectedRegistration != null)
        {

            _udapDcrDocument = JsonSerializer.Deserialize<UdapDynamicClientRegistrationDocument>(_beforeEncodingStatement);
            var beforeEncodingScope = _udapDcrDocument?.Scope;
            Console.WriteLine(beforeEncodingScope);


            AppState.ClientRegistrations.SelectedRegistration.Scope = beforeEncodingScope;
            await AppState.SetPropertyAsync(this, nameof(AppState.ClientRegistrations), AppState.ClientRegistrations);
        }
    }
}
