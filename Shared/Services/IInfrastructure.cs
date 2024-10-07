#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Org.BouncyCastle.X509;
using System.Security.Cryptography.X509Certificates;
using UdapEd.Shared.Model;

namespace UdapEd.Shared.Services;
public interface IInfrastructure
{
    Task<string> GetMyIp();

    /// <summary>
    /// Package up a zip file containing an RSA and ECSD type certificates for use in the Fhirlabs community.
    /// </summary>
    /// <param name="subjAltNames"></param>
    /// <returns></returns>
    Task<byte[]> BuildMyTestCertificatePackage(List<string> subjAltNames);

    Task<byte[]> JitFhirlabsCommunityCertificate(List<string> subjAltNames, string password);

    Task<CertificateViewModel?> GetX509data(string url);

    Task<string?> GetCrldata(string url);
    Task<X509CacheSettings?> GetX509StoreCache(string thumbprint);
    Task<CrlFileCacheSettings?> GetCryptNetUrlCache(string? path);
    Task RemoveFromX509Store(X509CacheSettings? settings);
    Task RemoveFromFileCache(CrlFileCacheSettings? settings);
}

public class X509CacheSettings
{
    public bool Cached { get; set; }

    public StoreName? StoreName { get; set; }
    public string? StoreNameDescription { get; set; }
    public StoreLocation? StoreLocation { get; set; }

    public string? Thumbprint { get; set; }
}

public class CrlFileCacheSettings
{
    public bool Cached { get; set; }

    public string? MetadataFile { get; set; }
    public string? ContentFile { get; set; }

    public string? UrlPath { get; set; }
}