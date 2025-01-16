#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.X509;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services.x509;

namespace UdapEd.Shared.Services;
public class Infrastructure : IInfrastructure
{
    protected readonly HttpClient HttpClient;
    private readonly CrlCacheService _crlCacheService;
    private readonly ILogger<Infrastructure> _logger;

    public Infrastructure(HttpClient httpClient, CrlCacheService crlCacheService, ILogger<Infrastructure> logger)
    {
        HttpClient = httpClient;
        _crlCacheService = crlCacheService;
        _logger = logger;
        HttpClient.Timeout = TimeSpan.FromSeconds(2);
    }

    public Task<string> GetMyIp()
    {
       return Task.FromResult("0.0.0.0");
    }

    public async Task<byte[]> BuildMyTestCertificatePackage(List<string> subjAltNames)
    {
        if (!subjAltNames.Any())
        {
            return [];
        }

        var (caData, intermediateData) = await GetSigningCertificates();

        using var rootCa = X509CertificateLoader.LoadPkcs12(caData, "udap-test");
        using var subCa = X509CertificateLoader.LoadPkcs12(intermediateData, "udap-test");

        var certTooling = new CertificateTooling();

        var x500Builder = new X500DistinguishedNameBuilder();
        x500Builder.AddCommonName(subjAltNames.First());
        x500Builder.AddOrganizationalUnitName("UDAP");
        x500Builder.AddOrganizationName("Fhir Coding");
        x500Builder.AddLocalityName("Portland");
        x500Builder.AddStateOrProvinceName("Oregon");
        x500Builder.AddCountryOrRegion("US");

        var distinguishedName = x500Builder.Build();

        var rsaCertificate = certTooling.BuildUdapClientCertificate(
            subCa,
            rootCa,
            subCa.GetRSAPrivateKey()!,
            distinguishedName,
            subjAltNames,
            "http://crl.fhircerts.net/crl/surefhirlabsIntermediateCrl.crl",
            "http://crl.fhircerts.net/certs/intermediates/SureFhirLabs_Intermediate.cer"
        );

        var ecdsaCertificate = certTooling.BuildClientCertificateECDSA(
            subCa,
            rootCa,
            subCa.GetRSAPrivateKey()!,
            distinguishedName,
            subjAltNames,
            "http://crl.fhircerts.net/crl/surefhirlabsIntermediateCrl.crl",
            "http://crl.fhircerts.net/certs/intermediates/SureFhirLabs_Intermediate.cer"
        );


        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Create the first entry
            var entry1 = archive.CreateEntry($"fhirlabs_client_rsa.p12");
            await using (var entryStream = entry1.Open())
            await using (var binaryWriter = new BinaryWriter(entryStream))
            {
                binaryWriter.Write(rsaCertificate);
            }

            // Create the second entry
            var entry2 = archive.CreateEntry("fhirlabs_client_ecdsa.p12");
            await using (var entryStream = entry2.Open())
            await using (var binaryWriter = new BinaryWriter(entryStream))
            {
                binaryWriter.Write(ecdsaCertificate);
            }
        }

        // Return the zip file as a byte array
        return memoryStream.ToArray();
    }

    public async Task<byte[]> JitFhirlabsCommunityCertificate(List<string> subjAltNames, string password)
    {
        var (caData, intermediateData) = await GetSigningCertificates();

        using var rootCa = X509CertificateLoader.LoadPkcs12(caData, "udap-test");
        using var subCa = X509CertificateLoader.LoadPkcs12(intermediateData, "udap-test");

        var certTooling = new CertificateTooling();

        var x500Builder = new X500DistinguishedNameBuilder();
        x500Builder.AddCommonName(subjAltNames.First());
        x500Builder.AddOrganizationalUnitName("UDAP");
        x500Builder.AddOrganizationName("Fhir Coding");
        x500Builder.AddLocalityName("Portland");
        x500Builder.AddStateOrProvinceName("Oregon");
        x500Builder.AddCountryOrRegion("US");

        var distinguishedName = x500Builder.Build();

        var rsaCertificate = certTooling.BuildUdapClientCertificate(
            subCa,
            rootCa,
            subCa.GetRSAPrivateKey()!,
            distinguishedName,
            subjAltNames,
            "http://crl.fhircerts.net/crl/surefhirlabsIntermediateCrl.crl",
            "http://crl.fhircerts.net/certs/intermediates/SureFhirLabs_Intermediate.cer"
        );

        return rsaCertificate;

    }

    public async Task<CertificateViewModel?> GetX509ViewModel(string url)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);

            var cert = X509CertificateLoader.LoadCertificate(bytes);
            return new CertificateDisplayBuilder(cert).BuildCertificateDisplayData();
        }
        catch (Exception ex)
        {
            var model = new CertificateViewModel();
            var errorEntry = new Dictionary<string, string> { { "Error", ex.Message } };
            model.TableDisplay.Add(errorEntry);
            return model;
        }
    }

    /// <summary>
    /// Resolve certificate
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public virtual async Task<string?> GetIntermediateX509(string url)
    {
        var bytes = await HttpClient.GetByteArrayAsync(url);

        return Convert.ToBase64String(bytes);
    }

    public async Task<string?> GetCrldata(string url)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);
            var crl = new X509Crl(bytes);

            return crl.ToString();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public Task<X509CacheSettings?> GetX509StoreCache(string thumbprint)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Define the stores to search on Windows
            var stores = new[]
            {
                //StoreName.My,
                // StoreName.Root,
                 StoreName.CertificateAuthority,
                // StoreName.TrustedPeople,
                // StoreName.TrustedPublisher
            };

            foreach (var storeName in stores)
            {
                using (var store = new X509Store(storeName, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                    if (cert.Count > 0)
                    {
                        return Task.FromResult<X509CacheSettings?>(new X509CacheSettings
                        {
                            StoreLocation = StoreLocation.LocalMachine,
                            StoreName = storeName,
                            StoreNameDescription = DescribeStoreName(storeName),
                            Cached = true,
                            Thumbprint = thumbprint
                        });
                    }
                }

                using (var store = new X509Store(storeName, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                    if (cert.Count > 0)
                    {
                        return Task.FromResult<X509CacheSettings?>(new X509CacheSettings
                        {
                            StoreLocation = StoreLocation.CurrentUser,
                            StoreName = storeName,
                            StoreNameDescription = DescribeStoreName(storeName),
                            Cached = true,
                            Thumbprint = thumbprint
                        });
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux, search the OpenSSL-based certificate store
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (cert.Count > 0)
            {
                return Task.FromResult<X509CacheSettings?>(new X509CacheSettings
                {
                    StoreLocation = StoreLocation.CurrentUser,
                    StoreName = StoreName.My,
                    StoreNameDescription = "OpenSSL-based store",
                    Cached = true,
                    Thumbprint = thumbprint
                });
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS, search the Keychain
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (cert.Count > 0)
            {
                return Task.FromResult<X509CacheSettings?>(new X509CacheSettings
                {
                    StoreLocation = StoreLocation.CurrentUser,
                    StoreName = StoreName.My,
                    StoreNameDescription = "macOS Keychain",
                    Cached = true,
                    Thumbprint = thumbprint
                });
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")))
        {
            // On Android, search the KeyStore
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (cert.Count > 0)
            {
                return Task.FromResult<X509CacheSettings?>(new X509CacheSettings
                {
                    StoreLocation = StoreLocation.CurrentUser,
                    StoreName = StoreName.My,
                    StoreNameDescription = "Android KeyStore",
                    Cached = true,
                    Thumbprint = thumbprint
                });
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("MACCATALYST")))
        {
            // On Mac Catalyst, search the Keychain
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (cert.Count > 0)
            {
                return Task.FromResult<X509CacheSettings?>(new X509CacheSettings
                {
                    StoreLocation = StoreLocation.CurrentUser,
                    StoreName = StoreName.My,
                    StoreNameDescription = "Mac Catalyst Keychain",
                    Cached = true,
                    Thumbprint = thumbprint
                });
            }
        }

        return Task.FromResult<X509CacheSettings?>(new X509CacheSettings());
    }

    public Task<CrlFileCacheSettings?> GetCryptNetUrlCache(string? location)
    {
        if (location == null)
        {
            return Task.FromResult<CrlFileCacheSettings?>(new CrlFileCacheSettings()
            {
                Cached = false
            });
        }

        return Task.FromResult<CrlFileCacheSettings?>(_crlCacheService.Get(location));
    }

    public Task RemoveFromX509Store(X509CacheSettings? settings)
    {
        if(settings != null && 
           settings.StoreName.HasValue && 
           settings.StoreLocation.HasValue &&
           settings.Thumbprint != null)
        {
            try
            {
                using var store = new X509Store(settings.StoreName.Value, settings.StoreLocation.Value);
                store.Open(OpenFlags.ReadWrite);
                var certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, settings.Thumbprint, false);

                if (certCollection.Count > 0)
                {
                    store.Remove(certCollection[0]);
                    Console.WriteLine($"Certificate with thumbprint {settings.Thumbprint} removed from {settings.StoreName} store at {settings.StoreLocation}.");
                }
                else
                {
                    Console.WriteLine($"Certificate with thumbprint {settings.Thumbprint} not found in {settings.StoreName} store at {settings.StoreLocation}.");
                }

                store.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Invalid settings provided for certificate removal.");
        }


        return Task.CompletedTask;
    }

    public Task RemoveFromFileCache(CrlFileCacheSettings? settings)
    {
        if (settings != null)
        {
            _crlCacheService.Remove(settings);
        }
        

        return Task.CompletedTask;
    }

    private async Task<(byte[] caData, byte[] intermediateData)> GetSigningCertificates()
    {
        var caFilePath = "gs://cert_store_private/surefhirlabs_community/SureFhirLabs_CA.pfx";
        var intermediateFilePath = "gs://cert_store_private/surefhirlabs_community/intermediates/SureFhirLabs_Intermediate.pfx";

        byte[] caData = await DownloadFileFromGcs(caFilePath);
        byte[] intermediateData = await DownloadFileFromGcs(intermediateFilePath);
        return (caData, intermediateData);
    }
    

    private async Task<byte[]> DownloadFileFromGcs(string gcsFilePath)
    {
        var storageClient = StorageClient.Create();
        var bucketName = "cert_store_private";
        var objectName = gcsFilePath.Replace("gs://cert_store_private/", "");

        using var memoryStream = new MemoryStream();
        await storageClient.DownloadObjectAsync(bucketName, objectName, memoryStream);
        return memoryStream.ToArray();
    }


    private string? DescribeStoreName(StoreName storeName)
    {
        switch (storeName)
        {
            case StoreName.My:
                return "Personal";
            case StoreName.Root:
                return "Trusted Root Certification Authorities";
            case StoreName.CertificateAuthority:
                return "Intermediate Certification Authorities";
            case StoreName.TrustedPeople:
                return "Trusted People";
            case StoreName.TrustedPublisher:
                return "Trusted Publisher";
            default:
                return null;
        }
    }

}
