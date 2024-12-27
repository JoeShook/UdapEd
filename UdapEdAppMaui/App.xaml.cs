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
        var window = base.CreateWindow(activationState);
        window.Page = new MainPage();
        return window;
    }

    protected override void OnStart()
    {
        base.OnStart();
        Task.Run(() => _crlCacheService.ProcessExistingFiles());
    }
}
