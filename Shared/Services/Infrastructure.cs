#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using Google.Cloud.Storage.V1;
using UdapEd.Shared.Services.x509;

namespace UdapEd.Shared.Services;
public class Infrastructure : IInfrastructure
{
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

        using var rootCA = new X509Certificate2(caData, "udap-test");
        using var subCA = new X509Certificate2(intermediateData, "udap-test");

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
            subCA,
            rootCA,
            subCA.GetRSAPrivateKey()!,
            distinguishedName,
            subjAltNames,
            "http://crl.fhircerts.net/crl/surefhirlabsIntermediateCrl.crl",
            "http://crl.fhircerts.net/certs/intermediates/SureFhirLabs_Intermediate.cer"
        );

        var ecdsaCertificate = certTooling.BuildClientCertificateECDSA(
            subCA,
            rootCA,
            subCA.GetRSAPrivateKey()!,
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

        using var rootCA = new X509Certificate2(caData, "udap-test");
        using var subCA = new X509Certificate2(intermediateData, "udap-test");

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
            subCA,
            rootCA,
            subCA.GetRSAPrivateKey()!,
            distinguishedName,
            subjAltNames,
            "http://crl.fhircerts.net/crl/surefhirlabsIntermediateCrl.crl",
            "http://crl.fhircerts.net/certs/intermediates/SureFhirLabs_Intermediate.cer"
        );

        return rsaCertificate;

    }

    private async Task<(byte[] caData, byte[] intermediateData)> GetSigningCertificates()
    {
        var caFilePath = "gs://cert_store_private/surefhirlabs_community/SureFhirLabs_CA.pfx";
        var intermediateFilePath = "gs://cert_store_private/surefhirlabs_community/intermediates/SureFhirLabs_Intermediate.pfx";

        byte[] caData = await DownloadFileFromGcsAsync(caFilePath);
        byte[] intermediateData = await DownloadFileFromGcsAsync(intermediateFilePath);
        return (caData, intermediateData);
    }
    

    private async Task<byte[]> DownloadFileFromGcsAsync(string gcsFilePath)
    {
        var storageClient = StorageClient.Create();
        var bucketName = "cert_store_private";
        var objectName = gcsFilePath.Replace("gs://cert_store_private/", "");

        using var memoryStream = new MemoryStream();
        await storageClient.DownloadObjectAsync(bucketName, objectName, memoryStream);
        return memoryStream.ToArray();
    }
}
