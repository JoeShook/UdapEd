using UdapEd.Shared.Services;

namespace UdapEdAppMaui;

public partial class App : Application
{
    private readonly CrlCacheService _crlCacheService;

    public App(CrlCacheService crlCacheService)
    {
        _crlCacheService = crlCacheService;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "UdapEd" };
    }

    protected override void OnStart()
    {
        base.OnStart();
        Task.Run(() => _crlCacheService.ProcessExistingFiles());
    }
}
