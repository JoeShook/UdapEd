#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using UdapEd.Shared.Model;

namespace UdapEd.Shared.Services;

public interface IMutualTlsService
{
    Task UploadClientCertificate(string certBytes);

    Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName);

    Task<CertificateStatusViewModel?> ValidateCertificate(string password);

    Task<CertificateStatusViewModel?> ClientCertificateLoadStatus();

    Task<CertificateStatusViewModel?> UploadAnchorCertificate(string base64String);

    Task<CertificateStatusViewModel?> LoadAnchor();

    Task<CertificateStatusViewModel?> AnchorCertificateLoadStatus();

    Task<List<string>?> VerifyMtlsTrust(string publicCertificate);
}