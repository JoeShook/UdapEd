#region (c) 2023 Joseph Shook. All rights reserved.
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
/// This builder gives access to the underlying <see cref="UdapDynamicClientRegistrationDocument"/>.
/// It is intended for usage in constructing invalid documents.
/// </summary>
public class UdapDcrBuilderForAuthorizationCodeUnchecked : UdapDcrBuilderForAuthorizationCode
{
    public new UdapDynamicClientRegistrationDocument Document => base.Document;

    protected UdapDcrBuilderForAuthorizationCodeUnchecked(X509Certificate2 certificate, bool cancelRegistration) : base(cancelRegistration)
    {
        base.WithCertificate(certificate);
    }

    protected UdapDcrBuilderForAuthorizationCodeUnchecked(List<X509Certificate2> certificates, bool cancelRegistration) : base(cancelRegistration)
    {
        base.WithCertificates(certificates);
    }

    protected UdapDcrBuilderForAuthorizationCodeUnchecked(bool cancelRegistration) : base(cancelRegistration)
    {
    }

    public new static UdapDcrBuilderForAuthorizationCodeUnchecked Create(List<X509Certificate2> certs)
    {
        return new UdapDcrBuilderForAuthorizationCodeUnchecked(certs, false);
    }

   
    public new static UdapDcrBuilderForAuthorizationCodeUnchecked Create()
    {
        return new UdapDcrBuilderForAuthorizationCodeUnchecked(false);
    }

    public new static UdapDcrBuilderForAuthorizationCodeUnchecked Cancel(X509Certificate2 cert)
    {
        return new UdapDcrBuilderForAuthorizationCodeUnchecked(cert, true);
    }
    
    public new static UdapDcrBuilderForAuthorizationCodeUnchecked Cancel()
    {
        return new UdapDcrBuilderForAuthorizationCodeUnchecked(true);
    }
}