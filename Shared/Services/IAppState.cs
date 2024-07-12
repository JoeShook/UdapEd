using System.Collections.Specialized;
using Microsoft.AspNetCore.Components;
using Udap.Model.Registration;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Discovery;
using UdapEd.Shared.Model.Smart;

namespace UdapEd.Shared.Services;

public interface IAppState
{
    string BaseUrl { get; }
    
    string Community { get; }

    OrderedDictionary BaseUrls { get; set; }

    public MetadataVerificationModel? MetadataVerificationModel { get; }

    RawSoftwareStatementAndHeader SoftwareStatementBeforeEncoding { get; }

    UdapRegisterRequest? UdapRegistrationRequest { get; }
    Oauth2FlowEnum Oauth2Flow { get; }

    RegistrationDocument? RegistrationDocument { get; }


    UdapClientCredentialsTokenRequestModel? ClientCredentialsTokenRequest { get; }

    CertificateStatusViewModel? UdapClientCertificateInfo { get; }

    CertificateStatusViewModel? UdapAnchorCertificateInfo { get; }

    CertificateStatusViewModel? MtlsClientCertificateInfo { get; }

    CertificateStatusViewModel? MtlsAnchorCertificateInfo { get; }

    UdapAuthorizationCodeTokenRequestModel? AuthorizationCodeTokenRequest { get; }

    AccessCodeRequestResult? AccessCodeRequestResult { get;  }

    LoginCallBackResult? LoginCallBackResult { get;  }

    SmartSession? SmartSession { get; }

    TokenResponseModel? AccessTokens { get;  }

    ClientSecureMode ClientMode { get; }

    LaunchContext? LaunchContext { get; }

    ClientStatus Status { get; }


    AuthorizationCodeRequest?AuthorizationCodeRequest { get; }

    ClientRegistrations? ClientRegistrations { get; }

    ClientHeaders? ClientHeaders { get; }

    PatientSearchPref? PatientSearchPref { get; }


    Task SetPropertyAsync(
        ComponentBase caller,
        string propertyName,
        object? propertyValue,
        bool saveChanges = true,
        bool fhirStateHasChanged = true);
}
