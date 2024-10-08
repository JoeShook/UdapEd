﻿#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion


using System.Collections.Specialized;
using Microsoft.AspNetCore.Components;
using Udap.Model.Registration;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.AuthExtObjects;
using UdapEd.Shared.Model.Discovery;
using UdapEd.Shared.Model.Smart;

namespace UdapEd.Shared.Services;

/// <summary>
/// Persistence data
/// </summary>
public class UdapClientState : IAppState
{
    public string BaseUrl { get; set; }

    public string Community { get; set; }

    public OrderedDictionary BaseUrls { get; set; }

    public MetadataVerificationModel? MetadataVerificationModel { get; set; }

    public RawSoftwareStatementAndHeader? SoftwareStatementBeforeEncoding { get; set; }

    public RawSoftwareStatementAndHeader? CertSoftwareStatementBeforeEncoding { get; set; }

    public UdapRegisterRequest? UdapRegistrationRequest { get; set; }

    public Oauth2FlowEnum Oauth2Flow { get; set; } = Oauth2FlowEnum.client_credentials;

    public RegistrationDocument? RegistrationDocument { get; set; }
    
    public UdapClientCredentialsTokenRequestModel? ClientCredentialsTokenRequest { get; set; }

    public UdapAuthorizationCodeTokenRequestModel? AuthorizationCodeTokenRequest { get; set; }

    public CertificateStatusViewModel? UdapClientCertificateInfo { get; set; }

    public CertificateStatusViewModel? CertificationAndEndorsementInfo { get; set; }

    public CertificateStatusViewModel? UdapAnchorCertificateInfo { get; set; }

    public CertificateStatusViewModel? MtlsClientCertificateInfo { get; set; }

    public CertificateStatusViewModel? MtlsAnchorCertificateInfo { get; set; }

    public AccessCodeRequestResult? AccessCodeRequestResult { get; set; }
   
    public LoginCallBackResult? LoginCallBackResult { get; set; }

    public SmartSession? SmartSession { get; set; }

    public void UpdateAccessTokens(ComponentBase source, TokenResponseModel? tokenResponseModel)
    {
        AccessTokens = tokenResponseModel;

        NotifyStateChanged();
    }

    public TokenResponseModel? AccessTokens { get; set; }
    
    public ClientSecureMode ClientMode { get; set; }

    public LaunchContext? LaunchContext { get; set; }

    public ClientStatus Status
    {
        get
        {
            if (AccessTokens == null)
            {
                return new ClientStatus(false, "Missing");
            }

            if (AccessTokens.IsError)
            {
                return new ClientStatus(false, "Error");
            }

            if (DateTime.UtcNow >= AccessTokens.ExpiresAt)
            {
                return new ClientStatus (false, "Expired");
            }

            var tokensList = new List<string>();

            if (!string.IsNullOrEmpty(AccessTokens.AccessToken))
            {
                tokensList.Add("Access");
            }
            if (!string.IsNullOrEmpty(AccessTokens.IdentityToken))
            {
                tokensList.Add("Identity");
            }
            if (!string.IsNullOrEmpty(AccessTokens.RefreshToken))
            {
                tokensList.Add("Refresh");
            }

            var statusMessage = string.Join(" | ", tokensList);

            return new ClientStatus(true, statusMessage);
        }

        set
        {

        }
    }

    /// <summary>
    /// String representation of UDAP 3.1 Authorization Code Flow
    /// </summary>
    public AuthorizationCodeRequest AuthorizationCodeRequest { get; set; }

    public Pkce Pkce { get; set; } = new();

    public ClientRegistrations? ClientRegistrations { get; set; }
    public ClientHeaders? ClientHeaders { get; set; }

    public PatientSearchPref? PatientSearchPref { get; set; }
    public Dictionary<string, AuthExtModel> AuthorizationExtObjects { get; set; }

    public FhirContext FhirContext { get; set; }

    public Task SetPropertyAsync(ComponentBase caller, string propertyName, object? propertyValue, bool saveChanges = true,
        bool fhirStateHasChanged = true)
    {
        throw new NotImplementedException();
    }

    public event Action? StateChanged;

    private void NotifyStateChanged() => StateChanged?.Invoke();


}