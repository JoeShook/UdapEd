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

namespace UdapEd.Shared.Model.Registration;

/// <summary>
/// This builder gives access to the underlying <see cref="UdapCertificationAndEndorsementDocument"/>.
/// It is intended for usage in constructing invalid documents.
/// </summary>
public class UdapCertificationsAndEndorsementBuilderUnchecked : UdapCertificationsAndEndorsementBuilder
{
    public new UdapCertificationAndEndorsementDocument Document => base.Document;

    protected UdapCertificationsAndEndorsementBuilderUnchecked(string certificationName, X509Certificate2 certificate) : base(certificationName)
    {
        base.WithCertificate(certificate);
    }
    protected UdapCertificationsAndEndorsementBuilderUnchecked(string certificationName) : base(certificationName)
    {
       
    }

    public new static UdapCertificationsAndEndorsementBuilderUnchecked Create(string certificationName, X509Certificate2 cert)
    {
        return new UdapCertificationsAndEndorsementBuilderUnchecked(certificationName, cert);
    }


    public new static UdapCertificationsAndEndorsementBuilderUnchecked Create(string certificationName)
    {
        return new UdapCertificationsAndEndorsementBuilderUnchecked(certificationName);
    }
}