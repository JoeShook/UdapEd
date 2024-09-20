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
    }
}
