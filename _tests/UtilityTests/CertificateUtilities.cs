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
using Xunit.Abstractions;

namespace UtilityTests;

/// <summary>
/// Some of the code below was adapted from the following article: https://u0041.co/posts/articals/certutil-artifacts-analysis/
/// And the associated Python and Rust code.  
/// </summary>
public class CertificateUtilities
{
    private readonly ITestOutputHelper _testOutputHelper;

    public CertificateUtilities(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ViewCrlCache()
    {
        string metaDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\LocalLow\Microsoft\CryptnetUrlCache\MetaData");

        if (Directory.Exists(metaDataPath))
        {
            foreach (string file in Directory.GetFiles(metaDataPath))
            {
                _testOutputHelper.WriteLine($"Reading MetaData file: {file}");
                var parsedData = ParseMetadata(file);
                if (parsedData != null)
                {
                    foreach (var kvp in parsedData)
                    {
                        _testOutputHelper.WriteLine($"{kvp.Key}: {kvp.Value}");
                    }
                }

                _testOutputHelper.WriteLine("");
            }
        }
        else
        {
            _testOutputHelper.WriteLine($"Directory {metaDataPath} does not exist.");
        }


        // string localLowPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\LocalLow\Microsoft\CryptnetUrlCache");
        // string metaDataPath = Path.Combine(localLowPath, "MetaData");
        // string contentPath = Path.Combine(localLowPath, "Content");
        //
        // if (Directory.Exists(metaDataPath))
        // {
        //     foreach (string file in Directory.GetFiles(metaDataPath))
        //     {
        //         _testOutputHelper.WriteLine($"Reading MetaData file: {file}");
        //         ParseMetadata(file);
        //     }


        // _testOutputHelper.WriteLine($"Reading MetaData file: {file}");
        // byte[] fileBytes = File.ReadAllBytes(file);
        //
        // // Example: Read the first 4 bytes as an integer (file size)
        // int fileSize = BitConverter.ToInt32(fileBytes, 0);
        //  _testOutputHelper.WriteLine($"File size: {fileSize} bytes");
        //
        // // Example: Read the next 20 bytes as a SHA1 hash
        // string sha1Hash = BitConverter.ToString(fileBytes, 4, 20).Replace("-", "");
        //  _testOutputHelper.WriteLine($"SHA1 Hash: {sha1Hash}");
        //
        // // Example: Read the next bytes as a URL (assuming it's null-terminated)
        // int urlStartIndex = 24;
        // int urlLength = Array.IndexOf(fileBytes, (byte)0, urlStartIndex) - urlStartIndex;
        // string url = Encoding.ASCII.GetString(fileBytes, urlStartIndex, urlLength);
        //  _testOutputHelper.WriteLine($"URL: {url}");
        //
        //  _testOutputHelper.WriteLine("");


        // if (Directory.Exists(contentPath))
        // {
        //     foreach (string file in Directory.GetFiles(contentPath))
        //     {
        //          _testOutputHelper.WriteLine($"Reading Content file: {file}");
        //         byte[] fileBytes = File.ReadAllBytes(file);
        //
        //         // Process the binary data as needed
        //          _testOutputHelper.WriteLine($"File size: {fileBytes.Length} bytes");
        //     }
        // }
    }

    // void ParseMetadata(string filePath)
    // {
    //     byte[] data = File.ReadAllBytes(filePath);
    //
    //     // Read the timestamp (8 bytes)
    //     long timestamp = BitConverter.ToInt64(data, 0);
    //     DateTime dateTime = DateTime.FromFileTimeUtc(timestamp);
    //      _testOutputHelper.WriteLine($"Timestamp: {dateTime}");
    //
    //     // Read the URL length (4 bytes)
    //     int urlLength = BitConverter.ToInt32(data, 8);
    //
    //     // Read the URL (variable length)
    //     string url = Encoding.UTF8.GetString(data, 12, urlLength).TrimEnd('\0');
    //     _testOutputHelper.WriteLine($"URL: {url}");
    //
    //     // Read the file size (4 bytes)
    //     int fileSize = BitConverter.ToInt32(data, 12 + urlLength);
    //      _testOutputHelper.WriteLine($"File Size: {fileSize} bytes");
    //
    //      _testOutputHelper.WriteLine("");
    // }



    Dictionary<string, string> ParseMetadata(string filePath)
    {
        byte[] data = File.ReadAllBytes(filePath);
        Dictionary<string, string> parsedData = new Dictionary<string, string>();

        try
        {
            // Read the header
            int urlSize = BitConverter.ToInt32(data, 12);
            long lastDownloadTime = BitConverter.ToInt64(data, 16);
            long lastModificationTimeHeader = BitConverter.ToInt64(data, 88);
            int etagSize = BitConverter.ToInt32(data, 100);
            int fileSize = BitConverter.ToInt32(data, 112);

            // Convert FILETIME to DateTime
            DateTime lastDownloadDateTime = DateTime.FromFileTimeUtc(lastDownloadTime);
            DateTime lastModificationDateTime = DateTime.FromFileTimeUtc(lastModificationTimeHeader);

            // Read the URL
            string url = Encoding.Unicode.GetString(data, 116, urlSize - 2); // -2 to ignore null byte
            url = url.TrimEnd('\0');

            // Read the ETag
            string hash;
            try
            {
                hash = Encoding.Unicode.GetString(data, 120 + urlSize, etagSize - 2).Replace("\"", "").TrimEnd('\0');
            }
            catch
            {
                hash = "Not Found";
            }

            // Populate parsed data
            parsedData["LastDownloadTime"] = lastDownloadDateTime.ToString("o");
            parsedData["LastModificationTimeHeader"] = lastModificationDateTime.ToString("o");
            parsedData["URL"] = url;
            parsedData["FileSize"] = fileSize.ToString();
            parsedData["ETag"] = hash;
            parsedData["FullPath"] = filePath;

            // Check if the file exists in the Content folder and calculate MD5 hash
            string contentFilePath = Path.Combine(Path.GetDirectoryName(filePath), @"..\Content", Path.GetFileName(filePath));
            if (File.Exists(contentFilePath))
            {
                string md5 = CalculateMD5(contentFilePath);
                parsedData["MD5"] = md5;
            }
        }
        catch (Exception ex)
        {
            _testOutputHelper.WriteLine($"Error parsing metadata: {ex.Message}");
            return null;
        }

        return parsedData;
    }

    static string CalculateMD5(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }




    [Fact]
    public void ViewCrlCacheFromRust()
    {
        string metaDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\LocalLow\Microsoft\CryptnetUrlCache\MetaData");
        bool useContent = true;

        if (Directory.Exists(metaDataPath))
        {
            foreach (string file in Directory.GetFiles(metaDataPath))
            {

                var parser = CryptnetURLCacheParser.ParseFile(file, useContent);

                _testOutputHelper.WriteLine($"URL Size: {parser.UrlSize}");
                _testOutputHelper.WriteLine($"Last Download Time: {parser.LastDownloadTime}");
                _testOutputHelper.WriteLine($"Last Modification Time Header: {parser.LastModificationTimeHeader}");
                _testOutputHelper.WriteLine($"Hash Size: {parser.HashSize}");
                _testOutputHelper.WriteLine($"File Size: {parser.FileSize}");
                _testOutputHelper.WriteLine($"URL: {parser.Url}");
                _testOutputHelper.WriteLine($"ETag: {parser.ETag}");
                _testOutputHelper.WriteLine($"SHA256: {parser.Sha256}");
                _testOutputHelper.WriteLine($"MD5: {parser.Md5}");
                _testOutputHelper.WriteLine($"Full Path: {parser.FullPath}");
                _testOutputHelper.WriteLine("");
            }
        }
    }

    [Fact]
    public void GetIntermediateCertificates_ShouldReturnCertificates()
    {
        // Act
        List<X509Certificate2> certificates = CertificateStoreHelper.GetIntermediateCertificates();

        // Assert
        Assert.NotNull(certificates);
        Assert.NotEmpty(certificates);

        // Optionally, print out the certificates for debugging
        foreach (var cert in certificates)
        {
             _testOutputHelper.WriteLine($"Subject: {cert.Subject}");
             _testOutputHelper.WriteLine($"Issuer: {cert.Issuer}");
             _testOutputHelper.WriteLine($"Thumbprint: {cert.Thumbprint}");
             _testOutputHelper.WriteLine("");
        }
    }
}

public static class CertificateStoreHelper
{
    public static List<X509Certificate2> GetIntermediateCertificates()
    {
        List<X509Certificate2> certificates = new List<X509Certificate2>();

        using (X509Store store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadOnly);

            foreach (X509Certificate2 cert in store.Certificates)
            {
                certificates.Add(cert);
            }
        }

        return certificates;
    }
}

public class CryptnetURLCacheParser
{
    public uint UrlSize { get; set; }
    public DateTime LastDownloadTime { get; set; }
    public DateTime LastModificationTimeHeader { get; set; }
    public uint HashSize { get; set; }
    public uint FileSize { get; set; }
    public string Url { get; set; }
    public string ETag { get; set; }
    public string? Sha256 { get; set; }
    public string? Md5 { get; set; }
    public string FullPath { get; set; }

    public CryptnetURLCacheParser()
    {
        UrlSize = 0;
        LastDownloadTime = DateTime.MinValue;
        LastModificationTimeHeader = DateTime.MinValue;
        HashSize = 0;
        FileSize = 0;
        Url = string.Empty;
        ETag = string.Empty;
        Sha256 = null;
        Md5 = null;
        FullPath = string.Empty;
    }

    public static CryptnetURLCacheParser ParseFile(string filepath, bool? useContent = false)
    {
        bool useContentValue = useContent ?? false;
        string fullpath = Path.GetFullPath(filepath);
        string parent = Directory.GetParent(fullpath).Parent.FullName;
        string contentpath = Path.Combine(parent, "Content", Path.GetFileName(fullpath));

        if (!File.Exists(fullpath))
        {
            throw new FileNotFoundException("File not Found!");
        }

        using (FileStream fs = new FileStream(fullpath, FileMode.Open, FileAccess.Read))
        {
            fs.Seek(12, SeekOrigin.Begin);
            CryptnetURLCacheParser parser = new CryptnetURLCacheParser();
            parser.FullPath = fullpath.Replace("\\\\?\\", "");

            using (BinaryReader reader = new BinaryReader(fs))
            {
                parser.UrlSize = reader.ReadUInt32();
                parser.LastDownloadTime = DateTime.FromFileTime(reader.ReadInt64());
                fs.Seek(64, SeekOrigin.Current);
                parser.LastModificationTimeHeader = DateTime.FromFileTime(reader.ReadInt64());
                fs.Seek(4, SeekOrigin.Current);
                parser.HashSize = reader.ReadUInt32();
                fs.Seek(8, SeekOrigin.Current);
                parser.FileSize = reader.ReadUInt32();
                parser.Url = ReadUtf16String(reader, (int)(parser.UrlSize / 2)) ?? "No URL Found!";
                parser.ETag = ReadUtf16String(reader, (int)(parser.HashSize / 2))?.Replace("\"", "") ?? "No Hash Found!";
                parser.Md5 = CalculateMD5(parser.Url);
            }

            if (useContentValue)
            {
                parser.Sha256 = ComputeSha256(contentpath);
            }

            return parser;
        }
    }

    static string CalculateMD5(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.Unicode.GetBytes(input); // UTF16-LE encoding
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }

    private static string ReadUtf16String(BinaryReader reader, int length)
    {
        byte[] bytes = reader.ReadBytes(length * 2);
        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private static string ComputeSha256(string filepath)
    {
        using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(fs);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}




