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
using System.Text.Json;

namespace UdapEd.Shared.Extensions;

public static class CertificateExtensions
{
    public static string GetPublicKeyAlgorithm(this X509Certificate2 certificate)
    {
        string keyAlgOid = certificate.GetKeyAlgorithm();
        var oid = new Oid(keyAlgOid);

        if (oid.Value == "1.2.840.113549.1.1.1")
        {
            return "RS";
        }

        if (oid.Value == "1.2.840.10045.2.1")
        {
            return "ES";
        }

        return "";
    }

    public static string SerializeCertificates(this X509Certificate2Collection certificates)
    {
        var serializedCertificates = certificates
            .Select(cert => Convert.ToBase64String(cert.Export(X509ContentType.Cert)))
            .ToList();

        return JsonSerializer.Serialize(serializedCertificates);
    }

    public static X509Certificate2Collection DeserializeCertificates(this string? serializedCertificates)
    {
        if (string.IsNullOrEmpty(serializedCertificates))
        {
            return new X509Certificate2Collection();
        }

        var base64Strings = JsonSerializer.Deserialize<List<string>>(serializedCertificates);
        var certificates = new X509Certificate2Collection();

        foreach (var base64String in base64Strings)
        {
            var certBytes = Convert.FromBase64String(base64String);
            var certificate = new X509Certificate2(certBytes);
            certificates.Add(certificate);
        }

        return certificates;
    }
}

