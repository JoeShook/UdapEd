#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace UdapEd.Shared.Services.x509;
public class CertificateTooling
{
    public byte[]? BuildUdapClientCertificate(
            X509Certificate2 intermediateCert,
            X509Certificate2 caCert,
            RSA intermediateKey,
            X500DistinguishedName distinguishedName,
            List<string> subjectAltNames,
            string? crl,
            string? buildAIAExtensionsPath = null,
            DateTimeOffset notBefore = default,
            DateTimeOffset notAfter = default,
            string password = "udap-test")
    {

        if (notBefore == default)
        {
            notBefore = DateTimeOffset.UtcNow;
        }

        if (notAfter == default)
        {
            notAfter = DateTimeOffset.UtcNow.AddYears(2);
        }


        var intermediateCertWithKey = intermediateCert.HasPrivateKey ?
            intermediateCert :
            intermediateCert.CopyWithPrivateKey(intermediateKey);

        using RSA rsaKey = RSA.Create(2048);

        var clientCertRequest = new CertificateRequest(
            distinguishedName,
            rsaKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        clientCertRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        clientCertRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                true));

        clientCertRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(clientCertRequest.PublicKey, false));

        AddAuthorityKeyIdentifier(intermediateCert, clientCertRequest);

        if (crl != null)
        {
            clientCertRequest.CertificateExtensions.Add(MakeCdp(crl));
        }

        var subAltNameBuilder = new SubjectAlternativeNameBuilder();
        foreach (var subjectAltName in subjectAltNames)
        {
            subAltNameBuilder.AddUri(new Uri(subjectAltName)); //Same as iss claim
        }

        var x509Extension = subAltNameBuilder.Build();
        clientCertRequest.CertificateExtensions.Add(x509Extension);

        if (buildAIAExtensionsPath != null)
        {
            var authorityInfoAccessBuilder = new AuthorityInformationAccessBuilder();
            authorityInfoAccessBuilder.AddCertificateAuthorityIssuerUri(new Uri(buildAIAExtensionsPath));
            var aiaExtension = authorityInfoAccessBuilder.Build();
            clientCertRequest.CertificateExtensions.Add(aiaExtension);
        }

        var clientCert = clientCertRequest.Create(
            intermediateCertWithKey,
            notBefore,
            notAfter,
            new ReadOnlySpan<byte>(RandomNumberGenerator.GetBytes(16)));
        // Do something with these certs, like export them to PFX,
        // or add them to an X509Store, or whatever.
        var clientCertWithKey = clientCert.CopyWithPrivateKey(rsaKey);


        var certPackage = new X509Certificate2Collection
        {
            clientCertWithKey,
            new X509Certificate2(intermediateCert.Export(X509ContentType.Cert)),
            new X509Certificate2(caCert.Export(X509ContentType.Cert))
        };

        return certPackage.Export(X509ContentType.Pkcs12, password);
    }

    public byte[]? BuildClientCertificateECDSA(
        X509Certificate2 intermediateCert,
        X509Certificate2 caCert,
        RSA intermediateKey,
        X500DistinguishedName distinguishedName,
        List<string> subjectAltNames,
        string? crl,
        string? buildAIAExtensionsPath,
        DateTimeOffset notBefore = default,
        DateTimeOffset notAfter = default,
        string password = "udap-test")
    {

        if (notBefore == default)
        {
            notBefore = DateTimeOffset.UtcNow;
        }

        if (notAfter == default)
        {
            notAfter = DateTimeOffset.UtcNow.AddYears(2);
        }


        var intermediateCertWithKey = intermediateCert.HasPrivateKey ?
            intermediateCert :
            intermediateCert.CopyWithPrivateKey(intermediateKey);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        var clientCertRequest = new CertificateRequest(
            distinguishedName,
            ecdsa,
            HashAlgorithmName.SHA256);

        clientCertRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        clientCertRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                true));

        clientCertRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(clientCertRequest.PublicKey, false));

        AddAuthorityKeyIdentifier(intermediateCert, clientCertRequest);

        if (crl != null)
        {
            clientCertRequest.CertificateExtensions.Add(MakeCdp(crl));
        }

        var subAltNameBuilder = new SubjectAlternativeNameBuilder();
        foreach (var subjectAltName in subjectAltNames)
        {
            subAltNameBuilder.AddUri(new Uri(subjectAltName)); //Same as iss claim
        }

        var x509Extension = subAltNameBuilder.Build();
        clientCertRequest.CertificateExtensions.Add(x509Extension);

        if (buildAIAExtensionsPath != null)
        {
            var authorityInfoAccessBuilder = new AuthorityInformationAccessBuilder();
            authorityInfoAccessBuilder.AddCertificateAuthorityIssuerUri(new Uri(buildAIAExtensionsPath));
            var aiaExtension = authorityInfoAccessBuilder.Build();
            clientCertRequest.CertificateExtensions.Add(aiaExtension);
        }

        var clientCert = clientCertRequest.Create(
            intermediateCertWithKey.SubjectName,
            X509SignatureGenerator.CreateForRSA(intermediateKey, RSASignaturePadding.Pkcs1),
            notBefore,
            notAfter,
            new ReadOnlySpan<byte>(RandomNumberGenerator.GetBytes(16)));
        // Do something with these certs, like export them to PFX,
        // or add them to an X509Store, or whatever.
        var clientCertWithKey = clientCert.CopyWithPrivateKey(ecdsa);


        var certPackage = new X509Certificate2Collection
        {
            clientCertWithKey,
            new X509Certificate2(intermediateCert.Export(X509ContentType.Cert)),
            new X509Certificate2(caCert.Export(X509ContentType.Cert))
        };


        return certPackage.Export(X509ContentType.Pkcs12, password);
    }

    protected static void AddAuthorityKeyIdentifier(X509Certificate2 caCert, CertificateRequest intermediateReq)
    {
        //
        // Found way to generate intermediate below
        //
        // https://github.com/rwatjen/AzureIoTDPSCertificates/blob/711429e1b6dee7857452233a73f15c22c2519a12/src/DPSCertificateTool/CertificateUtil.cs#L69
        // https://blog.rassie.dk/2018/04/creating-an-x-509-certificate-chain-in-c/
        //


        var issuerSubjectKey = caCert.Extensions?["2.5.29.14"].RawData;
        var segment = new ArraySegment<byte>(issuerSubjectKey, 2, issuerSubjectKey.Length - 2);
        var authorityKeyIdentifier = new byte[segment.Count + 4];
        // these bytes define the "KeyID" part of the AuthorityKeyIdentifier
        authorityKeyIdentifier[0] = 0x30;
        authorityKeyIdentifier[1] = 0x16;
        authorityKeyIdentifier[2] = 0x80;
        authorityKeyIdentifier[3] = 0x14;
        segment.CopyTo(authorityKeyIdentifier, 4);
        intermediateReq.CertificateExtensions.Add(new X509Extension("2.5.29.35", authorityKeyIdentifier, false));
    }

    protected static X509Extension MakeCdp(string url)
    {
        //
        // urls less than 119 char solution.
        // From Bartonjs of course.
        //
        // https://stackoverflow.com/questions/60742814/add-crl-distribution-points-cdp-extension-to-x509certificate2-certificate
        //
        // From Crypt32:  .NET doesn't support CDP extension. You have to use 3rd party libraries for that. BC is ok if it works for you.
        // Otherwise write you own. :)
        //

        byte[] encodedUrl = Encoding.ASCII.GetBytes(url);

        if (encodedUrl.Length > 119)
        {
            throw new NotSupportedException();
        }

        byte[] payload = new byte[encodedUrl.Length + 10];
        int offset = 0;
        payload[offset++] = 0x30;
        payload[offset++] = (byte)(encodedUrl.Length + 8);
        payload[offset++] = 0x30;
        payload[offset++] = (byte)(encodedUrl.Length + 6);
        payload[offset++] = 0xA0;
        payload[offset++] = (byte)(encodedUrl.Length + 4);
        payload[offset++] = 0xA0;
        payload[offset++] = (byte)(encodedUrl.Length + 2);
        payload[offset++] = 0x86;
        payload[offset++] = (byte)(encodedUrl.Length);
        Buffer.BlockCopy(encodedUrl, 0, payload, offset, encodedUrl.Length);

        return new X509Extension("2.5.29.31", payload, critical: false);
    }
}