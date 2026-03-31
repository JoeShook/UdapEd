#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using Udap.Model.Registration;
using Udap.Model.Statement;

namespace UdapEd.Shared.Model.Registration;

/// <summary>
/// This builder gives access to the underlying <see cref="UdapCertificationAndEndorsementDocument"/>.
/// It is intended for usage in constructing invalid documents.
/// </summary>
public class UdapCertificationsAndEndorsementBuilderUnchecked : UdapCertificationsAndEndorsementBuilder
{
    public new UdapCertificationAndEndorsementDocument Document => base.Document;

    private List<X509Certificate2>? _certificates;

    protected UdapCertificationsAndEndorsementBuilderUnchecked(string certificationName, X509Certificate2 certificate) : base(certificationName)
    {
        base.WithCertificate(certificate);
    }

    protected UdapCertificationsAndEndorsementBuilderUnchecked(string certificationName, List<X509Certificate2> certificates) : base(certificationName)
    {
        _certificates = certificates;
        if (certificates.Count > 0)
        {
            base.WithCertificate(certificates[0]);
        }
    }

    protected UdapCertificationsAndEndorsementBuilderUnchecked(string certificationName) : base(certificationName)
    {

    }

    public new static UdapCertificationsAndEndorsementBuilderUnchecked Create(string certificationName, X509Certificate2 cert)
    {
        return new UdapCertificationsAndEndorsementBuilderUnchecked(certificationName, cert);
    }

    public static UdapCertificationsAndEndorsementBuilderUnchecked Create(string certificationName, List<X509Certificate2> certs)
    {
        return new UdapCertificationsAndEndorsementBuilderUnchecked(certificationName, certs);
    }

    public new static UdapCertificationsAndEndorsementBuilderUnchecked Create(string certificationName)
    {
        return new UdapCertificationsAndEndorsementBuilderUnchecked(certificationName);
    }

    public new string BuildSoftwareStatement(string? signingAlgorithm = "RS256")
    {
        if (_certificates != null && _certificates.Count > 0)
        {
            return SignedSoftwareStatementBuilder<UdapCertificationAndEndorsementDocument>
                .Create(_certificates, Document)
                .Build(signingAlgorithm);
        }

        return base.BuildSoftwareStatement(signingAlgorithm);
    }
}
