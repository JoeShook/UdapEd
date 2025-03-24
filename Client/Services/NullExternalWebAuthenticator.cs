using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class NullExternalWebAuthenticator : IExternalWebAuthenticator
{
    public Task<WebAuthenticatorResult> AuthenticateAsync(string url, string callbackUrl)
    {
        throw new NotImplementedException("External web authentication is not supported on this platform.");
    }
}
