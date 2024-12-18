#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace UdapEd.Shared.CdsComponents;
public class CdsJwtGenerator
{
    private static string ReadPrivateKey(string path)
    {
        return File.ReadAllText(path);
    }

    public static string GenerateJWT(string audience)
    {
        var privateKey = ReadPrivateKey("../../keys/ecprivkey.pem");
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKey.ToCharArray());

        var securityKey = new ECDsaSecurityKey(ecdsa)
        {
            KeyId = "44823f3d-0b01-4a6c-a80e-b9d3e8a7226f"
        };

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha384);

        var header = new JwtHeader(credentials)
        {
            { "typ", "JWT" },
            { "kid", "44823f3d-0b01-4a6c-a80e-b9d3e8a7226f" },
            { "jku", "https://sandbox.cds-hooks.org/.well-known/jwks.json" }
        };

        var payload = new JwtPayload
        {
            { "iss", "https://sandbox.cds-hooks.org" },
            { "aud", audience },
            { "exp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 300 },
            { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { "jti", Guid.NewGuid().ToString() }
        };

        var token = new JwtSecurityToken(header, payload);
        var handler = new JwtSecurityTokenHandler();

        return handler.WriteToken(token);
    }
}
