#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Google.Api.Gax;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;
using Org.BouncyCastle.Ocsp;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Udap.Model;
using Udap.Model.Registration;
using UdapEd.Shared.Components;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Registration;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Pages;

public partial class UdapRegistration : IAsyncDisposable
{
    private string SubjectAltName { get; set; }
    private string? _signingAlgorithm;

    // Expansion panel state
    private bool _regStep2Expanded;
    private bool _regStep3Expanded;
    private bool _regStep4Expanded;

    private bool TieredOauth
    {
        get => AppSharedState.TieredOAuth;
        set
        {
            AppSharedState.TieredOAuth = value;
            if (value)
            {
                OpenIdScope = true;
                RedirectUrls[$"{NavigationManager.BaseUri}udapTieredOAuth"] = true;
            }
            else
            {
                RedirectUrls[$"{NavigationManager.BaseUri}udapTieredOAuth"] = false;
            }
        }
    }

    private bool OpenIdScope { get; set; } = true;
    private bool SmartLaunch { get; set; }
    private bool SmartV1Scopes { get; set; } = true;
    private bool SmartV2Scopes { get; set; }
    private bool DPoPEnabled { get; set; }
    private double _dpopArrowY = -1;
    private bool _dpopArrowVisible;

    private async Task RecalcDPoPArrowPosition()
    {
        if (!DPoPEnabled || !SoftwareStatementBeforeEncodingSoftwareStatement.Contains("dpop_enabled"))
        {
            _dpopArrowY = -1;
            _dpopArrowVisible = false;
            return;
        }

        var pos = await JsRuntime.InvokeAsync<DPoPArrowPosition>(
            "dpopArrow.getPosition", "softwareStatementTextarea", "\"dpop_enabled\"");
        _dpopArrowY = pos.Y;
        _dpopArrowVisible = pos.Visible;
    }

    [JSInvokable]
    public void UpdateDPoPArrowPosition(double y, bool visible)
    {
        _dpopArrowY = y;
        _dpopArrowVisible = visible;
        StateHasChanged();
    }

    private record DPoPArrowPosition(double Y, bool Visible);

    public string? IdP { get; set; }
    private string? _requestBody;
    private bool _missingScope;
    private bool _cancelRegistration;
    private UdapDynamicClientRegistrationDocument? _udapDcrDocument;
    private UdapCertificationAndEndorsementDocument? _udapCertificationDocument;
    private string _localRegisteredClients = string.Empty;

    
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
    [CascadingParameter]
    public CascadingAppState AppState { get; set; } = null!;

    [Inject] 
    public AppSharedState AppSharedState { get; set; } = null!;
    
    [CascadingParameter] 
    public MainLayout Layout { get; set; } = null!;

    ErrorBoundary? ErrorBoundary { get; set; }
    [Inject] IRegisterService RegisterService { get; set; } = null!;
    [Inject] ICertificationService CertificationService { get; set; } = null!;
    [Inject] IInfrastructure Infrastructure { get; set; } = null!;

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
                    ?.ToJsonString(new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    }).Replace("\\u002B", "+");
            }
            catch
            {
                // ignored
            }

            return jsonHeader ?? string.Empty;
        }

        set => _beforeEncodingHeader = value;
    }

    private string _beforeCertificationEncodingHeader = string.Empty;
    private string CertSoftwareStatementBeforeEncodingHeader
    {
        get
        {
            if (!string.IsNullOrEmpty(_beforeCertificationEncodingHeader))
            {
                return _beforeCertificationEncodingHeader;
            }

            if (AppState.CertSoftwareStatementBeforeEncoding?.Header == null)
            {
                return _beforeCertificationEncodingHeader;
            }

            string? jsonHeader = null;

            try
            {
                jsonHeader = JsonNode.Parse(AppState.CertSoftwareStatementBeforeEncoding.Header)
                    ?.ToJsonString(new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    }).Replace("\\u002B", "+");
            }
            catch
            {
                // ignored
            }

            return jsonHeader ?? string.Empty;
        }

        set => _beforeCertificationEncodingHeader = value;
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
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

    private string _beforeEncodingCertStatement = string.Empty;
    private string CertSoftwareStatementBeforeEncodingSoftwareStatement
    {
        get
        {
            if (!string.IsNullOrEmpty(_beforeEncodingCertStatement))
            {
                return _beforeEncodingCertStatement;
            }

            if (AppState.CertSoftwareStatementBeforeEncoding?.SoftwareStatement == null)
            {
                return _beforeEncodingCertStatement;
            }

            string? jsonStatement = null;

            try
            {
                jsonStatement = JsonNode.Parse(AppState.CertSoftwareStatementBeforeEncoding.SoftwareStatement)
                    ?.ToJsonString(new JsonSerializerOptions()
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
            }
            catch
            {
                // ignored
            }

            return jsonStatement ?? string.Empty;
        }

        set => _beforeEncodingCertStatement = value;
    }


    private const string VALID_STYLE = "pre udap-indent-1";
    private const string INVALID_STYLE = "pre udap-indent-1 jwt-invalid";
    public string ValidRawSoftwareStatementStyle { get; set; } = VALID_STYLE;
    public string ValidCertRawSoftwareStatementStyle { get; set; } = VALID_STYLE;

    private void Reset()
    {
        _signingAlgorithm = null;
        _localRegisteredClients = string.Empty;

        if (AppState.UdapClientCertificateInfo?.SubjectAltNames != null && AppState.UdapClientCertificateInfo.SubjectAltNames.Any())
        {
            SubjectAltName = AppState.UdapClientCertificateInfo.SubjectAltNames.First();
        }

        StateHasChanged();
    }

    private void CiertificationReset()
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

    private void PersistCertSoftwareStatement()
    {
        try
        {
            var rawStatement = new RawSoftwareStatementAndHeader
            {
                Header = CertSoftwareStatementBeforeEncodingHeader,
                SoftwareStatement = _beforeEncodingCertStatement
                // Scope = beforeEncodingScope
            };

            ValidCertRawSoftwareStatementStyle = VALID_STYLE;
            AppState.SetProperty(this, nameof(AppState.CertSoftwareStatementBeforeEncoding), rawStatement);
        }
        catch
        {
            ValidCertRawSoftwareStatementStyle = INVALID_STYLE;
        }
    }

    private string? _registrationResult;
    

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
                .RegistrationDocument, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
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
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
        }
        set => _requestBody = value;
    }

    


    private async Task BuildRawSoftwareStatement()
    {
        _certificateViewerForClient?.Reset();
        _certificateViewerForCertification?.Reset();
        _cancelRegistration = false;
        SetRawMessage("Loading ...");
        SetRawCertificationMessage("Loading ...");

        await Task.Delay(100);

        if (ServerRequiresTefcaCertification())
        {
            await Infrastructure.ResolveAiaIntermediates();
            await Infrastructure.ResolveAiaIntermediates("certification");

            _selectedCertificationTemplate = CertificationTemplates.Entries
                .FirstOrDefault(t => t.Name == "TEFCA Basic App") ?? _selectedCertificationTemplate;
        }

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
                    AppSharedState.ScopeLevelSelected, 
                    SmartLaunch,
                    SmartV1Scopes,
                    SmartV2Scopes));
        }

        await BuildRawCertSoftwareStatement();
    }

    private bool ServerRequiresTefcaCertification()
    {
        var required = AppState.MetadataVerificationModel?.UdapServerMetaData?.UdapCertificationsRequired;
        return required != null && required.Contains("https://rce.sequoiaproject.org/udap/profiles/basic-app-certification");
    }

    private void AppendCertToX5cHeader(string? certBase64, ref string headerField)
    {
        if (string.IsNullOrEmpty(certBase64) || string.IsNullOrEmpty(headerField))
        {
            return;
        }

        try
        {
            var headerNode = JsonNode.Parse(headerField);
            var x5cArray = headerNode?["x5c"]?.AsArray();

            if (x5cArray != null)
            {
                x5cArray.Add(certBase64);
                headerField = headerNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                    .Replace("\\u002B", "+");
            }
        }
        catch
        {
            // ignored
        }
    }

    private void OnRegistrationIntermediateAdded(string? certBase64)
    {
        AppendCertToX5cHeader(certBase64, ref _beforeEncodingHeader);
        StateHasChanged();
    }

    private void OnCertificationIntermediateAdded(string? certBase64)
    {
        AppendCertToX5cHeader(certBase64, ref _beforeCertificationEncodingHeader);
        StateHasChanged();
    }

    private async Task BuildRawCancelSoftwareStatement()
    {
        _cancelRegistration = true;
        SetRawMessage("Loading ...");
        SetRawCertificationMessage("Loading ...");

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

            if (DPoPEnabled)
            {
                request["dpop_enabled"] = true;
            }

            if (request.Scope == null && request.GrantTypes?.Count > 0)
            {
                _missingScope = true;
            }
            // Console.WriteLine(request.SerializeToJson(true));
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

            var redirectUrls = new List<string>();

            foreach (var url in RedirectUrls.Where(r => r.Value).Select(r => r.Key))
            {
                redirectUrls.Add(url);
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

            if (DPoPEnabled)
            {
                request["dpop_enabled"] = true;
            }

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


    private async Task BuildRawCertSoftwareStatement()
    {
        try
        {
            if (!AppState.CertificationCertLoaded)
            {
                return;
            }

            var builder = UdapCertificationsAndEndorsementBuilderUnchecked
                .Create(_selectedCertificationTemplate.CertificationName);

            // Pull scope, grant_types, and response_types from the client software statement
            string? clientScope = null;
            List<string>? clientGrantTypes = null;
            HashSet<string>? clientResponseTypes = null;

            if (AppState.SoftwareStatementBeforeEncoding?.SoftwareStatement != null)
            {
                var clientDoc = JsonSerializer.Deserialize<UdapDynamicClientRegistrationDocument>(
                    AppState.SoftwareStatementBeforeEncoding.SoftwareStatement);

                if (clientDoc != null)
                {
                    clientScope = clientDoc.Scope;
                    clientGrantTypes = clientDoc.GrantTypes?.ToList();
                    clientResponseTypes = clientDoc.ResponseTypes != null
                        ? new HashSet<string>(clientDoc.ResponseTypes)
                        : null;
                }
            }

            builder
            .WithCertificationDescription(_selectedCertificationTemplate.CertificationDescription)
                .WithCertificationUris(_selectedCertificationTemplate.CertificationUris)
                .WithDeveloperName("Joe Shook")
                .WithDeveloperAddress("Portland Oregon")
                .WithClientName(UdapEdConstants.CLIENT_NAME)
                .WithSoftwareId(UdapEdConstants.CLIENT_NAME)
                .WithSoftwareVersion(@Assembly.GetExecutingAssembly().GetName().Version.ToString())
                .WithClientUri("https://udaped.fhirlabs.net")
                .WithLogoUri("https://udaped.fhirlabs.net/_content/UdapEd.Shared/images/UdapEdLogobyDesigner.png")
                .WithTermsOfService("https://udaped.fhirlabs.net/TermsOfService.html")
                .WithPolicyUri("https://udaped.fhirlabs.net/Policy.html")
                .WithContacts(new List<string>() { "mailto:Joseph.Shook@Surescripts.com", "mailto:JoeShook@gmail.com" })
                .WithGrantTypes(clientGrantTypes)
                .WithResponseTypes(clientResponseTypes)
                .WithScope(clientScope ?? "user/*.write")
                .WithTokenEndpointAuthMethod("private_key_jwt");
                                                                    
            builder.Document.Expiration = EpochTime.GetIntDate(DateTime.Now.AddYears(1)); //todo set expiration in UI
            builder.WithAdditionalClaims(_selectedCertificationTemplate.AdditionalClaims);

            var request = builder.Build();

            var statement = await CertificationService
                .BuildSoftwareStatement(request, _signingAlgorithm);
            
            if (statement != null)
            {
                SetRawCertStatement(statement.Header, statement.SoftwareStatement);
                await AppState.SetPropertyAsync(this, nameof(AppState.CertSoftwareStatementBeforeEncoding), statement);
            }
        }
        catch (Exception ex)
        {
            SetRawCertificationMessage(string.Empty);
            await ResetCertificationSoftwareStatement();
            RawSoftwareStatementError = ex.Message;
        }
    }
    
    private void SetRawMessage(string message)
    {
        RawSoftwareStatementError = string.Empty;
        SoftwareStatementBeforeEncodingHeader = message;
        SoftwareStatementBeforeEncodingSoftwareStatement = string.Empty;
    }

    private void SetRawCertificationMessage(string message)
    {
        RawSoftwareStatementError = string.Empty;
        CertSoftwareStatementBeforeEncodingHeader = message;
        CertSoftwareStatementBeforeEncodingSoftwareStatement = string.Empty;
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

    private void SetRawCertStatement(string header, string softwareStatement = "")
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
            _udapCertificationDocument = JsonSerializer.Deserialize<UdapCertificationAndEndorsementDocument>(jsonStatement);
        }

        CertSoftwareStatementBeforeEncodingHeader = jsonHeader ?? string.Empty;
        CertSoftwareStatementBeforeEncodingSoftwareStatement = jsonStatement ?? string.Empty;
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

    private async Task ResetCertificationSoftwareStatement()
    {
        SetRawCertificationMessage(string.Empty);
        await AppState.SetPropertyAsync(this, nameof(AppState.CertSoftwareStatementBeforeEncoding), null);
       
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

        HighlightSoftwareStatement();
    }

    private async Task BuildRequestBodyForClientCredentials()
    {
        string? certifications = null;

        if (AppState.CertificationCertLoaded && AppState.CertSoftwareStatementBeforeEncoding != null)
        {
            certifications = await CertificationService
                .BuildRequestBody(AppState.CertSoftwareStatementBeforeEncoding, _signingAlgorithm);
        }

        var registerRequest = await RegisterService
            .BuildRequestBodyForClientCredentials(AppState.SoftwareStatementBeforeEncoding, _signingAlgorithm);

        if (!string.IsNullOrEmpty(certifications))
        {
            registerRequest.Certifications = new string[] { certifications };
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.UdapRegistrationRequest), registerRequest);
    
        RequestBody = JsonSerializer.Serialize(
            registerRequest,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

    }

    private async Task BuildRequestBodyForAuthorizationCode()
    {
        string? certifications = null;

        if (AppState.CertificationCertLoaded && AppState.CertSoftwareStatementBeforeEncoding != null)
        {
            certifications = await CertificationService
                .BuildRequestBody(AppState.CertSoftwareStatementBeforeEncoding, _signingAlgorithm);
        }

        var registerRequest = await RegisterService
            .BuildRequestBodyForAuthorizationCode(
                AppState.SoftwareStatementBeforeEncoding,
                _signingAlgorithm);

        if (!string.IsNullOrEmpty(certifications))
        {
            registerRequest.Certifications = new string[] { certifications };
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.UdapRegistrationRequest), registerRequest);

        RequestBody = JsonSerializer.Serialize(
            registerRequest,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

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
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

            if (_cancelRegistration)
            { // cancel registration
                AppState.ClientRegistrations?.CancelRegistration(resultModel.Result);
            }
            else if (AppState is { BaseUrl: not null, ClientRegistrations: not null })
            {
                if (resultModel.Result.ClientId != null)
                {
                    var registration = AppState.ClientRegistrations.SetRegistration(resultModel.Result, _udapDcrDocument, Oauth2Flow, AppState.BaseUrl, AppState.UdapClientCertificateInfo?.Thumbprint ?? string.Empty, DPoPEnabled);
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
    
    private DotNetObjectReference<UdapRegistration>? _dotNetRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (AppState.UdapClientCertificateInfo?.SubjectAltNames != null && AppState.UdapClientCertificateInfo.SubjectAltNames.Any())
            {
                SubjectAltName = AppState.UdapClientCertificateInfo?.SubjectAltNames.First();
                StateHasChanged();
            }

            _dotNetRef = DotNetObjectReference.Create(this);
            await JsRuntime.InvokeVoidAsync("dpopArrow.register", _dotNetRef, "softwareStatementTextarea", "\"dpop_enabled\"");
        }

        if (DPoPEnabled && _dpopArrowY < 0 && SoftwareStatementBeforeEncodingSoftwareStatement.Contains("dpop_enabled"))
        {
            await RecalcDPoPArrowPosition();
            StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
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
            var filtered = AppState.ClientRegistrations?.FilterRegistrations(
                AppState,
                r => AppState.MetadataVerificationModel?.UdapServerMetaData?.RegistrationEndpoint == r.RegistrationUrl);

            return filtered?.Values;
        }
    }
    
    private async Task SaveScopesToClient()
    {
        if (AppState.ClientRegistrations.SelectedRegistration != null)
        {

            _udapDcrDocument = JsonSerializer.Deserialize<UdapDynamicClientRegistrationDocument>(_beforeEncodingStatement);
            var beforeEncodingScope = _udapDcrDocument?.Scope;
            
            AppState.ClientRegistrations.SelectedRegistration.Scope = beforeEncodingScope;
            await AppState.SetPropertyAsync(this, nameof(AppState.ClientRegistrations), AppState.ClientRegistrations);
        }
    }

    private Dictionary<string, bool>? _redirectUrls;
    private CertificatePKIViewer _certificateViewerForClient;
    private CertificatePKIViewer _certificateViewerForCertification;
    private CertificationTemplate _selectedCertificationTemplate = CertificationTemplates.Entries[0];

    private static readonly HashSet<string> TefcaHighlightKeys = new()
    {
        "certification_name", "certification_uris", "exchange_purposes", "home_community_id"
    };

    private string HighlightedCertSoftwareStatement
    {
        get
        {
            if (string.IsNullOrEmpty(_beforeEncodingCertStatement))
            {
                return string.Empty;
            }

            var highlighted = _beforeEncodingCertStatement;

            foreach (var key in TefcaHighlightKeys)
            {
                highlighted = HighlightJsonKey(highlighted, key);
            }

            return highlighted;
        }
    }

    private static string HighlightJsonKey(string json, string key)
    {
        var searchKey = $"\"{key}\"";
        var idx = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (idx < 0) return json;

        // Find the start of the line
        var lineStart = json.LastIndexOf('\n', idx);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        // Find the end of the value (handle multi-line arrays/objects)
        var colonIdx = json.IndexOf(':', idx + searchKey.Length);
        if (colonIdx < 0) return json;

        var valueStart = colonIdx + 1;

        // Skip whitespace after colon
        while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]) && json[valueStart] != '\n')
            valueStart++;

        int valueEnd;
        if (valueStart < json.Length && json[valueStart] == '[')
        {
            // Array value - find matching ]
            var depth = 1;
            var pos = valueStart + 1;
            while (pos < json.Length && depth > 0)
            {
                if (json[pos] == '[') depth++;
                else if (json[pos] == ']') depth--;
                pos++;
            }
            valueEnd = pos;
        }
        else
        {
            // Simple value - find end of line
            valueEnd = json.IndexOf('\n', valueStart);
            if (valueEnd < 0) valueEnd = json.Length;
        }

        // Trim trailing comma if present
        var lineEnd = valueEnd;
        while (lineEnd < json.Length && (json[lineEnd] == ',' || json[lineEnd] == '\n'))
            lineEnd++;

        var line = json.Substring(lineStart, lineEnd - lineStart);
        var marked = $"<mark>{line.TrimEnd()}</mark>\n";

        return json.Substring(0, lineStart) + marked + json.Substring(lineEnd);
    }

    public Dictionary<string, bool> RedirectUrls
    {
        get
        {
            if (_redirectUrls == null)
            {
                _redirectUrls = CreateRedirectUrls();
            }
            return _redirectUrls;
        }
        set => _redirectUrls = value;
    }

    private Dictionary<string, bool> CreateRedirectUrls()
    {
        var redirectUrls = new Dictionary<string, bool>
        {
            { $"{NavigationManager.BaseUri}udapBusinessToBusiness", false },
            { $"{NavigationManager.BaseUri}udapConsumer", false },
            { $"{NavigationManager.BaseUri}udapTieredOAuth", false }
        };
        
        return redirectUrls;
    }

    public void SetRedirectUrl()
    {
        if (AppSharedState.ScopeLevelSelected == "patient")
        {
            RedirectUrls[$"{NavigationManager.BaseUri}udapConsumer"] = true;
            RedirectUrls[$"{NavigationManager.BaseUri}udapBusinessToBusiness"] = false;
        }
        else if(AppSharedState.ScopeLevelSelected == "user")
        {
            RedirectUrls[$"{NavigationManager.BaseUri}udapConsumer"] = false;
            RedirectUrls[$"{NavigationManager.BaseUri}udapBusinessToBusiness"] = true;
        }
        else
        {
            RedirectUrls[$"{NavigationManager.BaseUri}udapConsumer"] = false;
            RedirectUrls[$"{NavigationManager.BaseUri}udapBusinessToBusiness"] = false;
        }
    }

    private void HighlightSoftwareStatement()
    {
        RequestBody = RequestBody.Replace("<mark>", "").Replace("</mark>", "");
        RequestBody = RequestBody.Replace($"<div id=\"expandableText\" class=\"expandable\" onclick=\"toggleText()\">", "").Replace("</mark>", "");
        RequestBody = RequestBody.Replace($"<span id=\"indicator\" class=\"indicator\">[+]</span></div>", "").Replace("</mark>", "");

        try { 
            var jsonDocument = JsonDocument.Parse(RequestBody);
            if (jsonDocument.RootElement.TryGetProperty("software_statement", out var softwareStatementElement))
            {
                var softwareStatement = softwareStatementElement.GetString();
                RequestBody = RequestBody.Replace(softwareStatement, $"<mark>{softwareStatement}</mark>");
            }

            if (jsonDocument.RootElement.TryGetProperty("certifications", out var certificationsElement) &&
                certificationsElement.ValueKind == JsonValueKind.Array &&
                certificationsElement.GetArrayLength() > 0)
            {
                var firstCertification = certificationsElement[0];
                if (firstCertification.ValueKind == JsonValueKind.String)
                {
                    var certificationValue = firstCertification.GetString();
                    RequestBody = RequestBody
                        .Replace(certificationValue,
                            $"<div id=\"expandableText\" class=\"expandable\" onclick=\"toggleText()\">" +
                            $"{certificationValue}" +
                            $"<span id=\"indicator\" class=\"indicator\">[+]</span></div>");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

    }

    private void HighlightCertifications()
    {
        RequestBody = RequestBody.Replace("<mark>", "").Replace("</mark>", "");
        RequestBody = RequestBody.Replace($"<div id=\"expandableText\" class=\"expandable\" onclick=\"toggleText()\">", "").Replace("</mark>", "");
        RequestBody = RequestBody.Replace($"<span id=\"indicator\" class=\"indicator\">[+]</span></div>", "").Replace("</mark>", "");

        var jsonDocument = JsonDocument.Parse(RequestBody);
        if (jsonDocument.RootElement.TryGetProperty("certifications", out var certificationsElement) &&
            certificationsElement.ValueKind == JsonValueKind.Array &&
            certificationsElement.GetArrayLength() > 0)
        {
            var firstCertification = certificationsElement[0];
            if (firstCertification.ValueKind == JsonValueKind.String)
            {
                var certificationValue = firstCertification.GetString();
                RequestBody = RequestBody.Replace(certificationValue, $"<mark>{certificationValue}</mark>");
            }
        }

        if (jsonDocument.RootElement.TryGetProperty("software_statement", out var softwareStatementElement))
        {
            var softwareStatement = softwareStatementElement.GetString();
            RequestBody = RequestBody
                .Replace(softwareStatement,
                    $"<div id=\"expandableText\" class=\"expandable\" onclick=\"toggleText()\">" +
                    $"{softwareStatement}" +
                    $"<span id=\"indicator\" class=\"indicator\">[+]</span></div>");
        }

    }

    public async ValueTask DisposeAsync()
    {
        await JsRuntime.InvokeVoidAsync("dpopArrow.unregister");
        _dotNetRef?.Dispose();
    }
}
