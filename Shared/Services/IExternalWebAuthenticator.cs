#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion


namespace UdapEd.Shared.Services;

public interface IExternalWebAuthenticator
{
    Task<ExternalWebAuthenticatorResult> AuthenticateAsync(string url, string callbackUrl);
}

public sealed class ExternalWebAuthenticatorResult
{
    public ExternalWebAuthenticatorResult(IDictionary<string, string?> properties)
    {
        Properties = new Dictionary<string, string?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string?> Properties { get; }

    public string? Get(string key) =>
        Properties.TryGetValue(key, out var value) ? value : null;
}