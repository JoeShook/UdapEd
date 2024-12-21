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

namespace UdapEd.Shared.Extensions
{
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

        public static string ToPemFormat(this byte[]? rawData, bool lineBreaks = false)
        {
            if (rawData == null)
            {
                return string.Empty;
            }

            const string pemHeader = "-----BEGIN CERTIFICATE-----";
            var rawDataString = Encoding.UTF8.GetString(rawData);
            
            if (rawDataString.StartsWith(pemHeader))
            {
                return rawDataString;
            }

            var pem = new StringBuilder();
            pem.AppendLine("-----BEGIN CERTIFICATE-----");
            pem.AppendLine(Convert.ToBase64String(rawData, lineBreaks ? Base64FormattingOptions.None : Base64FormattingOptions.InsertLineBreaks));
            pem.AppendLine("-----END CERTIFICATE-----");

            return pem.ToString();
        }

        public static string ExportToPem(this X509Certificate2 cert, bool lineBreaks = false)
        {
            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert),
                lineBreaks ? Base64FormattingOptions.None : Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");
            return builder.ToString();
        }

        public static string ExportPrivateKeyToPem(this X509Certificate2 cert, bool lineBreaks = false)
        {
            var builder = new StringBuilder();
            var privateKey = cert.GetRSAPrivateKey();
            if (privateKey != null)
            {
                var keyBytes = privateKey.ExportPkcs8PrivateKey();
                builder.AppendLine("-----BEGIN PRIVATE KEY-----");
                builder.AppendLine(Convert.ToBase64String(keyBytes,
                    lineBreaks ? Base64FormattingOptions.None : Base64FormattingOptions.InsertLineBreaks));
                builder.AppendLine("-----END PRIVATE KEY-----");
            }
            return builder.ToString();
        }

    }
}
