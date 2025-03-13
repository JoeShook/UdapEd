using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using Udap.Util.Extensions;
using X509Extensions = Org.BouncyCastle.Asn1.X509.X509Extensions;
using System;
using System.Numerics;

namespace UdapEd.Shared.Model;
public class CertificateDisplayBuilder
{
    private readonly X509Certificate2 _cert;

    public CertificateDisplayBuilder(X509Certificate2 cert)
    {
        _cert = cert;
    }

    public CertificateViewModel? BuildCertificateDisplayData()
    {
        var data = new List<KeyValuePair<string, string>>();

        data.Add(new KeyValuePair<string, string>("Serial Number", GetSerialNumber(_cert)));
        data.Add(new KeyValuePair<string, string>("Subject", _cert.Subject));
        data.Add(new KeyValuePair<string, string>("Subject Alternative Names", GetSANs(_cert)));
        data.Add(new KeyValuePair<string, string>("Public Key Algorithm", GetPublicKeyAlgorithm(_cert)));
        data.Add(new KeyValuePair<string, string>("Certificate Policy", BuildPolicyInfo(_cert)));
        data.Add(new KeyValuePair<string, string>("Start Date", _cert.GetEffectiveDateString()));
        data.Add(new KeyValuePair<string, string>("End Date", _cert.GetExpirationDateString()));
        data.Add(new KeyValuePair<string, string>("Key Usage", GetKeyUsage(_cert)));
        // data.Add(new KeyValuePair<string, string>("Extended Key Usage", GetExtendedKeyUsage(cert)));
        data.Add(new KeyValuePair<string, string>("Issuer", _cert.Issuer));
        data.Add(new KeyValuePair<string, string>("Subject Key Identifier", GetSubjectKeyIdentifier(_cert)));
        data.Add(new KeyValuePair<string, string>("Authority Key Identifier", GetAuthorityKeyIdentifier(_cert)));

        foreach (var url in GetAIAUrls(_cert) ?? new List<string>())
        {
            data.Add(new KeyValuePair<string, string>("Authority Information Access", url));
        }

        foreach (var url in GetCrlDistributionPoints(_cert) ?? new List<string>())
        {
            data.Add(new KeyValuePair<string, string>("CRL Distribution", url));
        }

        data.Add(new KeyValuePair<string, string>("Thumbprint SHA1", _cert.Thumbprint));

        var result = new CertificateViewModel();

        result.TableDisplay.Add(data);
        return result;
    }

    private static List<string>? GetAIAUrls(X509Certificate2 cert)
    {
        var aiaExtensions =
            cert.Extensions["1.3.6.1.5.5.7.1.1"] as X509AuthorityInformationAccessExtension;

        if (aiaExtensions == null)
        {
            return null;
        }

        var urls = new List<string>();
        foreach (var url in aiaExtensions!.EnumerateCAIssuersUris())
        {
            urls.Add(url);
        }

        return urls;
    }

    private string GetPublicKeyAlgorithm(X509Certificate2 cert)
    {
        string keyAlgOid = cert.GetKeyAlgorithm();
        var oid = new Oid(keyAlgOid);

        var key = cert.GetRSAPublicKey() as AsymmetricAlgorithm ?? cert.GetECDsaPublicKey();
        return $"{oid.FriendlyName} ({key?.KeySize})";
    }

    private string GetSerialNumber(X509Certificate2 cert)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hex : {cert.SerialNumber}");
        sb.AppendLine($"Decimal : {new BigInteger(cert.GetSerialNumber())}");

        return sb.ToString();
    }

    private string GetSANs(X509Certificate2 cert)
    {
        var sans = cert.GetSubjectAltNames();

        if (!sans.Any())
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var tuple in sans)
        {
            sb.AppendLine($"{tuple.Item1} : {tuple.Item2}");
        }

        return sb.ToString();
    }

    private string BuildPolicyInfo(X509Certificate2 cert)
    {
        var extension = cert.GetExtensionValue("2.5.29.32") as Asn1OctetString;
        if (extension == null)
        {
            return string.Empty;
        }
        var policies = extension.GetOctets();
        var policyInfoList = CertificatePolicies.GetInstance(policies).GetPolicyInformation();
        return string.Join("\r\n", policyInfoList.Select(p => p.PolicyIdentifier.ToString()));
    }

    private string GetKeyUsage(X509Certificate2 cert)
    {
        var extensions = cert.Extensions.OfType<X509KeyUsageExtension>().ToList();

        if (!extensions.Any())
        {
            return String.Empty;
        }

        var keyUsage = extensions.First().KeyUsages;

        return string.Join("; ", keyUsage.ToKeyUsageToString());
    }

    private string GetExtendedKeyUsage(X509Certificate2 cert)
    {
        var ext = cert.GetExtensionValue(X509Extensions.ExtendedKeyUsage.Id) as Asn1OctetString;

        if (ext == null)
        {
            return string.Empty;
        }

        var instance = ExtendedKeyUsage.GetInstance(Asn1Object.FromByteArray(ext.GetOctets()));

        var joe = instance.GetAllUsages();
        return joe.ToString();
    }

    private string GetSubjectKeyIdentifier(X509Certificate2 cert)
    {
        var extensions = cert.Extensions.OfType<X509SubjectKeyIdentifierExtension>().ToList();

        if (!extensions.Any())
        {
            return string.Empty;
        }

        return extensions.First().SubjectKeyIdentifier ?? string.Empty;
    }

    private string GetAuthorityKeyIdentifier(X509Certificate2 cert)
    {
        var extensions = cert.Extensions.OfType<X509AuthorityKeyIdentifierExtension>().ToList();

        if (!extensions.Any())
        {
            return string.Empty;
        }

        var bytes = extensions.First().KeyIdentifier?.ToArray();

        if (bytes == null)
        {
            return string.Empty;
        }

        return CreateByteStringRep(bytes);
    }

    private static List<string>? GetCrlDistributionPoints(X509Certificate2 cert)
    {
        var ext = cert.GetExtensionValue(X509Extensions.CrlDistributionPoints.Id);

        if (ext == null)
        {
            return null;
        }

        var distPoints = CrlDistPoint.GetInstance(ext);
        var urls = new List<string>();

        foreach (var distPoint in distPoints.GetDistributionPoints())
        {
            if (distPoint.DistributionPointName != null
                && distPoint.DistributionPointName.PointType == DistributionPointName.FullName)
            {
                var names = GeneralNames.GetInstance(distPoint.DistributionPointName.Name);

                foreach (var generalName in names.GetNames())
                {
                    var name = generalName.Name.ToString();
                    if (name != null)
                    {
                        urls.Add(name);
                    }
                }
            }
        }

        return urls;
    }

    private static string CreateByteStringRep(byte[] bytes)
    {
        var c = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i] >> 4;
            c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
            b = bytes[i] & 0xF;
            c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
        }
        return new string(c);

    }
}
