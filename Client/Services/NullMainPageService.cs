using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public sealed class NullMainPageService : IMainPageService
{
    public void BringToFront() { /* no-op for WebAssembly */ }
}