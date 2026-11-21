#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;

namespace UdapEd.Shared.Extensions;

/// <summary>
/// Builds the certificate list for the <c>x5c</c> header of a UDAP JWT: the signing certificate
/// first, followed by the intermediate that issued it (and that intermediate's issuers, up to but
/// not including a self-signed root). TEFCA requires the issuing intermediate to be present.
/// </summary>
public static class X5cChain
{
    private const string AuthorityKeyIdentifierOid = "2.5.29.35";

    /// <summary>
    /// Returns the intermediates carried in a PKCS#12 file that chain <paramref name="signingCertificate"/>
    /// toward its root, ordered issuer first. Self-signed roots are left out.
    /// </summary>
    public static X509Certificate2Collection IntermediatesFromPkcs12(
        byte[] pkcs12,
        string? password,
        X509Certificate2 signingCertificate)
    {
        var collection = X509CertificateLoader.LoadPkcs12Collection(pkcs12, password);

        return new X509Certificate2Collection(IssuerPath(signingCertificate, collection).ToArray());
    }

    /// <summary>
    /// Orders certificates for an <c>x5c</c> header: the signing certificate, then the issuer path
    /// found among <paramref name="intermediates"/> and <paramref name="anchor"/>, then any remaining
    /// intermediates in their original order. A self-signed anchor is never added.
    /// </summary>
    public static List<X509Certificate2> Order(
        X509Certificate2 signingCertificate,
        IEnumerable<X509Certificate2>? intermediates,
        X509Certificate2? anchor = null)
    {
        var supplied = (intermediates ?? Enumerable.Empty<X509Certificate2>())
            .Where(c => c.Thumbprint != signingCertificate.Thumbprint)
            .ToList();

        var candidates = new List<X509Certificate2>(supplied);

        if (anchor != null && !IsSelfSigned(anchor) && candidates.All(c => c.Thumbprint != anchor.Thumbprint))
        {
            candidates.Add(anchor);
        }

        var x5c = new List<X509Certificate2> { signingCertificate };
        x5c.AddRange(IssuerPath(signingCertificate, candidates));

        foreach (var cert in supplied)
        {
            if (x5c.All(c => c.Thumbprint != cert.Thumbprint))
            {
                x5c.Add(cert);
            }
        }

        return x5c;
    }

    /// <summary>
    /// Returns a warning when the first certificate in <paramref name="x5c"/> is not self-signed and
    /// none of the following certificates issued it; otherwise <c>null</c>.
    /// </summary>
    public static string? MissingIssuerWarning(IReadOnlyList<X509Certificate2> x5c)
    {
        if (x5c.Count == 0)
        {
            return null;
        }

        var signingCertificate = x5c[0];

        if (IsSelfSigned(signingCertificate) || x5c.Skip(1).Any(c => IsIssuedBy(signingCertificate, c)))
        {
            return null;
        }

        return $"The x5c header does not contain the certificate that issued the signing certificate " +
               $"(issuer: {signingCertificate.Issuer}). TEFCA requires the signing certificate to be followed " +
               "by its issuing intermediate. Load a PKCS#12 that includes the intermediate, load the intermediate " +
               "as the anchor, or resolve it from the certificate's AIA extension and add it to x5c.";
    }

    /// <summary>
    /// <see cref="MissingIssuerWarning(IReadOnlyList{X509Certificate2})"/> for the base64 values of an x5c header.
    /// Returns <c>null</c> when the values cannot be parsed.
    /// </summary>
    public static string? MissingIssuerWarning(IEnumerable<string>? x5cBase64)
    {
        if (x5cBase64 == null)
        {
            return null;
        }

        try
        {
            var certificates = x5cBase64
                .Select(b64 => X509Certificate2.CreateFromPem(Convert.FromBase64String(b64).ToPemFormat()))
                .ToList();

            return MissingIssuerWarning(certificates);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Walks from <paramref name="certificate"/> through <paramref name="candidates"/> to the first
    /// self-signed certificate (which is not returned) or until no issuer is found.
    /// </summary>
    public static List<X509Certificate2> IssuerPath(
        X509Certificate2 certificate,
        IEnumerable<X509Certificate2> candidates)
    {
        var pool = candidates.ToList();
        var path = new List<X509Certificate2>();
        var current = certificate;

        while (!IsSelfSigned(current))
        {
            var issuer = pool.FirstOrDefault(c =>
                c.Thumbprint != current.Thumbprint &&
                path.All(p => p.Thumbprint != c.Thumbprint) &&
                IsIssuedBy(current, c));

            if (issuer == null || IsSelfSigned(issuer))
            {
                break;
            }

            path.Add(issuer);
            current = issuer;
        }

        return path;
    }

    public static bool IsSelfSigned(X509Certificate2 certificate)
    {
        return certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);
    }

    /// <summary>
    /// True when <paramref name="issuer"/>'s subject matches <paramref name="certificate"/>'s issuer and,
    /// when both key identifiers are present, the authority key identifier matches the subject key identifier.
    /// </summary>
    public static bool IsIssuedBy(X509Certificate2 certificate, X509Certificate2 issuer)
    {
        if (!certificate.IssuerName.RawData.AsSpan().SequenceEqual(issuer.SubjectName.RawData))
        {
            return false;
        }

        var akiExtension = certificate.Extensions[AuthorityKeyIdentifierOid];
        var aki = akiExtension as X509AuthorityKeyIdentifierExtension
                  ?? (akiExtension != null
                      ? new X509AuthorityKeyIdentifierExtension(akiExtension.RawData, akiExtension.Critical)
                      : null);
        var ski = issuer.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault();

        if (aki?.KeyIdentifier is { } keyId && ski?.SubjectKeyIdentifierBytes is { Length: > 0 } skiBytes)
        {
            return keyId.Span.SequenceEqual(skiBytes.Span);
        }

        return true;
    }
}
