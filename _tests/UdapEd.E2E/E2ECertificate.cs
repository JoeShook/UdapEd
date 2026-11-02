#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Formats.Asn1;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;

namespace UdapEd.E2E;

/// <summary>
/// The FhirLabs client certificate the E2E tests register with: one certificate, one SAN per flow,
/// issued by the SureFhirLabs CA through UdapEd's "Build FhirLabs Community Certificates" endpoint.
/// </summary>
public static class E2ECertificate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Returns the certificate file, issuing a new one when it is missing, expires within 30 days,
    /// or does not carry the configured SANs.
    /// </summary>
    public static async Task<string> EnsureAsync(Action<string> log)
    {
        await Gate.WaitAsync();
        try
        {
            var path = E2ESettings.CertificatePath;
            if (IsUsable(path))
            {
                return path;
            }

            log($"Issuing the E2E certificate from {E2ESettings.CertificateIssuerUrl} with SANs: {string.Join(", ", E2ESettings.CertificateSans)}");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var query = string.Join("&", E2ESettings.CertificateSans.Select(san => $"subjAltNames={Uri.EscapeDataString(san)}"));
            // The endpoint returns the zip as base64 text.
            var base64Zip = (await http.GetStringAsync(
                $"{E2ESettings.CertificateIssuerUrl}/Infrastructure/BuildMyTestCertificatePackage?{query}")).Trim().Trim('"');

            using var zip = new ZipArchive(new MemoryStream(Convert.FromBase64String(base64Zip)));
            var entry = zip.GetEntry("fhirlabs_client_rsa.p12")
                        ?? throw new InvalidOperationException("The certificate pack has no fhirlabs_client_rsa.p12");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var file = File.Create(path))
            await using (var stream = entry.Open())
            {
                await stream.CopyToAsync(file);
            }

            if (!IsUsable(path))
            {
                throw new InvalidOperationException($"The issued certificate at {path} is not usable");
            }

            log($"Saved the E2E certificate to {path}");
            return path;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool IsUsable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, E2ESettings.CertificatePassword);
            var sans = UriSubjectAltNames(certificate);

            return certificate.NotAfter > DateTime.Now.AddDays(30)
                   && E2ESettings.CertificateSans.All(san => sans.Contains(san));
        }
        catch
        {
            return false;
        }
    }

    // GeneralNames ::= SEQUENCE OF GeneralName; uniformResourceIdentifier is [6] IA5String (RFC 5280 4.2.1.6).
    private static List<string> UriSubjectAltNames(X509Certificate2 certificate)
    {
        var uris = new List<string>();
        var extension = certificate.Extensions["2.5.29.17"];
        if (extension == null)
        {
            return uris;
        }

        var uriTag = new Asn1Tag(TagClass.ContextSpecific, 6);
        var names = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
        while (names.HasData)
        {
            if (names.PeekTag().HasSameClassAndValue(uriTag))
            {
                uris.Add(names.ReadCharacterString(UniversalTagNumber.IA5String, uriTag));
            }
            else
            {
                names.ReadEncodedValue();
            }
        }

        return uris;
    }
}
