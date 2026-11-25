#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shouldly;
using UdapEd.Shared.Extensions;

namespace UtilityTests;

public class X5cChainTests
{
    private readonly X509Certificate2 _root;
    private readonly X509Certificate2 _intermediate;
    private readonly X509Certificate2 _leaf;

    public X5cChainTests()
    {
        _root = CreateCa("CN=Test Root", null);
        _intermediate = CreateCa("CN=Test Intermediate", _root);
        _leaf = CreateLeaf("CN=Test Client", _intermediate);
    }

    [Fact]
    public void IntermediatesFromPkcs12_ReturnsIssuingIntermediate_WithoutRoot()
    {
        var pkcs12 = new X509Certificate2Collection { _leaf, _intermediate, _root }
            .Export(X509ContentType.Pkcs12, "pw")!;

        var intermediates = X5cChain.IntermediatesFromPkcs12(pkcs12, "pw", _leaf);

        intermediates.Count.ShouldBe(1);
        intermediates[0].Thumbprint.ShouldBe(_intermediate.Thumbprint);
    }

    [Fact]
    public void IntermediatesFromPkcs12_LeafOnly_ReturnsEmpty()
    {
        var pkcs12 = _leaf.Export(X509ContentType.Pkcs12, "pw");

        X5cChain.IntermediatesFromPkcs12(pkcs12, "pw", _leaf).Count.ShouldBe(0);
    }

    [Fact]
    public void Order_PutsIssuerAfterSigningCertificate()
    {
        var extra = CreateCa("CN=Unrelated", null);

        var x5c = X5cChain.Order(_leaf, new[] { extra, _intermediate });

        x5c.Select(c => c.Thumbprint).ShouldBe(new[]
        {
            _leaf.Thumbprint, _intermediate.Thumbprint, extra.Thumbprint
        });
    }

    [Fact]
    public void Order_UsesIntermediateAnchor_WhenNoIntermediateStored()
    {
        var x5c = X5cChain.Order(_leaf, null, _intermediate);

        x5c.Select(c => c.Thumbprint).ShouldBe(new[] { _leaf.Thumbprint, _intermediate.Thumbprint });
    }

    [Fact]
    public void Order_DoesNotAddSelfSignedAnchor()
    {
        var x5c = X5cChain.Order(_leaf, null, _root);

        x5c.Select(c => c.Thumbprint).ShouldBe(new[] { _leaf.Thumbprint });
    }

    [Fact]
    public void Order_DoesNotDuplicateAnchorAlreadyStored()
    {
        var x5c = X5cChain.Order(_leaf, new[] { _intermediate }, _intermediate);

        x5c.Count.ShouldBe(2);
    }

    [Fact]
    public void MissingIssuerWarning_LeafOnly_Warns()
    {
        var warning = X5cChain.MissingIssuerWarning(new[] { _leaf });

        warning.ShouldNotBeNull();
        warning.ShouldContain("CN=Test Intermediate");
    }

    [Fact]
    public void MissingIssuerWarning_WithIssuer_IsNull()
    {
        X5cChain.MissingIssuerWarning(new[] { _leaf, _intermediate }).ShouldBeNull();
    }

    [Fact]
    public void MissingIssuerWarning_WrongIntermediate_Warns()
    {
        var other = CreateCa("CN=Other Intermediate", _root);

        X5cChain.MissingIssuerWarning(new[] { _leaf, other }).ShouldNotBeNull();
    }

    [Fact]
    public void MissingIssuerWarning_SameNameDifferentKey_Warns()
    {
        var impostor = CreateCa("CN=Test Intermediate", _root);

        X5cChain.MissingIssuerWarning(new[] { _leaf, impostor }).ShouldNotBeNull();
    }

    [Fact]
    public void MissingIssuerWarning_FromBase64Header()
    {
        var x5c = new[]
        {
            Convert.ToBase64String(_leaf.RawData),
            Convert.ToBase64String(_intermediate.RawData)
        };

        X5cChain.MissingIssuerWarning(x5c).ShouldBeNull();
        X5cChain.MissingIssuerWarning(x5c.Take(1)).ShouldNotBeNull();
    }

    private static X509Certificate2 CreateCa(string subject, X509Certificate2? issuer)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        if (issuer == null)
        {
            using var selfSigned = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return Exportable(selfSigned);
        }

        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));

        using var issued = request.Create(
            issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMonths(6), Guid.NewGuid().ToByteArray());
        using var withKey = issued.CopyWithPrivateKey(key);
        return Exportable(withKey);
    }

    private static X509Certificate2 Exportable(X509Certificate2 certificate)
    {
        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateLeaf(string subject, X509Certificate2 issuer)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));

        using var issued = request.Create(
            issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMonths(3), Guid.NewGuid().ToByteArray());
        using var withKey = issued.CopyWithPrivateKey(key);
        return Exportable(withKey);
    }
}
