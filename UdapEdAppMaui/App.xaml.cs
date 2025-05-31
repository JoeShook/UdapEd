using UdapEd.Shared.Services;

namespace UdapEdAppMaui;

public partial class App : Application
{
    private readonly CrlCacheService _crlCacheService;
    
    private readonly MainPage _mainPage;
#if IOS
    private readonly IDeviceOrientationService _deviceOrientationService;
    public App(CrlCacheService crlCacheService, IDeviceOrientationService deviceOrientationService)
    {
        _crlCacheService = crlCacheService;
        _deviceOrientationService = deviceOrientationService;
        InitializeComponent();
    }
#else
    public App(CrlCacheService crlCacheService)
    {
        _crlCacheService = crlCacheService;
        InitializeComponent();
    }
#endif
    
    protected override Window CreateWindow(IActivationState? activationState)
    {

#if IOS
        return new Window(new MainPage(_deviceOrientationService)) { Title = "UdapEd" };
#else
        return new Window(new MainPage()) { Title = "UdapEd" };
#endif
    }

    protected override void OnStart()
    {
        base.OnStart();
        Task.Run(() => _crlCacheService.ProcessExistingFiles());
    }
}
