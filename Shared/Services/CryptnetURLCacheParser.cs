#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography;
using System.Text;

namespace UdapEd.Shared.Services;

/// <summary>
/// Parse cached CRL data from AppData\LocalLow\Microsoft\CryptnetUrlCache\MetaData
/// 
/// Some of the code below was adapted from the following article: https://u0041.co/posts/articals/certutil-artifacts-analysis/
/// And the associated Python and Rust code.  
/// </summary>
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