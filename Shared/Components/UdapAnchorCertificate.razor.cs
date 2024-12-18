using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using Color = MudBlazor.Color;

namespace UdapEd.Shared.Components;

public partial class UdapAnchorCertificate
{
    private static UdapAnchorCertificate? _instance;
    [CascadingParameter] private CascadingAppState AppState { get; set; } = null!;
    private readonly PeriodicTimer _periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject] private IDiscoveryService DiscoveryService { get; set; } = null!;
    private bool _checkServerSession;
    public Color AnchorLoadedColor;
    public Color EmrDirectAnchorLoadedColor;
    public Color FhirLabsAnchorLoadedColor;

    protected override async Task OnInitializedAsync()
    {
        _instance = this;
        var userSuppliedCertificate = AppState.UdapAnchorCertificateInfo?.UserSuppliedCertificate;

        var anchorCertificateLoadStatus = await DiscoveryService.AnchorCertificateLoadStatus();

        if (anchorCertificateLoadStatus != null)
        {
            anchorCertificateLoadStatus.UserSuppliedCertificate = userSuppliedCertificate ?? false;
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.UdapAnchorCertificateInfo), anchorCertificateLoadStatus);

        if (anchorCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            SetCertLoadedColor(anchorCertificateLoadStatus.CertLoaded, ref AnchorLoadedColor);
        }
        else if (anchorCertificateLoadStatus != null &&
                 anchorCertificateLoadStatus.Issuer.StartsWith("EMR Direct Test CA"))
        {
            SetCertLoadedColor(anchorCertificateLoadStatus.CertLoaded, ref EmrDirectAnchorLoadedColor);
        }
        else if (anchorCertificateLoadStatus != null && anchorCertificateLoadStatus.Issuer.StartsWith("SureFhir-CA"))
        {
            SetCertLoadedColor(anchorCertificateLoadStatus.CertLoaded, ref FhirLabsAnchorLoadedColor);
        }


        await JSRuntime.InvokeVoidAsync("pageEventHandlers.registerHandlers");
        RunTimer();
    }

    [JSInvokable("OnPageFocus")]
    public static async Task OnPageFocus()
    {
        if (_instance != null)
        {
            await _instance.OnPageFocusInstance();
        }
    }

    [JSInvokable("OnPageBlur")]
    public static void OnPageBlur()
    {
        _instance?.OnPageBlurInstance();
    }

    private async Task OnPageFocusInstance()
    {
        var anchorCertificateLoadStatus = await DiscoveryService.AnchorCertificateLoadStatus();
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapAnchorCertificateInfo), anchorCertificateLoadStatus);


        if (anchorCertificateLoadStatus != null && anchorCertificateLoadStatus.Issuer.StartsWith("EMR Direct Test CA"))
        {
            SetCertLoadedColor(anchorCertificateLoadStatus.CertLoaded, ref EmrDirectAnchorLoadedColor);
        }
        else if (anchorCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            SetCertLoadedColor(anchorCertificateLoadStatus.CertLoaded, ref AnchorLoadedColor);
        }
        else if (anchorCertificateLoadStatus != null && anchorCertificateLoadStatus.Issuer.StartsWith("SureFhir-CA"))
        {
            SetCertLoadedColor(anchorCertificateLoadStatus.CertLoaded, ref FhirLabsAnchorLoadedColor);
        }

        _checkServerSession = true;
    }

    private void OnPageBlurInstance()
    {
        _checkServerSession = false;
    }

    private async Task LoadFhirLabsAnchor()
    {
        var certViewModel = await DiscoveryService.LoadFhirLabsAnchor();
        SetCertLoadedColor(certViewModel?.CertLoaded, ref FhirLabsAnchorLoadedColor);
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapAnchorCertificateInfo), certViewModel);
    }

    private async Task LoadUdapOrgAnchor()
    {
        var certViewModel = await DiscoveryService.LoadUdapOrgAnchor();
        SetCertLoadedColor(certViewModel?.CertLoaded, ref EmrDirectAnchorLoadedColor);
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapAnchorCertificateInfo), certViewModel);
    }

    private void SetCertLoadedColor(CertLoadedEnum? isCertLoaded, ref Color anchorColor)
    {
        AnchorLoadedColor = Color.Default;
        EmrDirectAnchorLoadedColor = Color.Default;
        FhirLabsAnchorLoadedColor = Color.Default;

        switch (isCertLoaded)
        {
            case CertLoadedEnum.Negative:
                anchorColor = Color.Error;
                AppState.SetProperty(this, nameof(AppState.AnchorLoaded), false);
                break;
            case CertLoadedEnum.Positive:
                anchorColor = Color.Success;
                AppState.SetProperty(this, nameof(AppState.AnchorLoaded), true);
                break;
            case CertLoadedEnum.InvalidPassword:
                anchorColor = Color.Warning;
                AppState.SetProperty(this, nameof(AppState.AnchorLoaded), false);
                break;
            default:
                anchorColor = Color.Error;
                AppState.SetProperty(this, nameof(AppState.AnchorLoaded), false);
                break;
        }

        this.StateHasChanged();
    }

    private async void RunTimer()
    {
        while (await _periodicTimer.WaitForNextTickAsync())
        {
            if (_checkServerSession)
            {
                var certViewModel = await DiscoveryService.AnchorCertificateLoadStatus();
                await AppState.SetPropertyAsync(this, nameof(AppState.UdapAnchorCertificateInfo), certViewModel);
                SetCertLoadedColor(certViewModel?.CertLoaded, ref AnchorLoadedColor);
            }
        }
    }

    private async Task UploadFilesAsync(InputFileChangeEventArgs e)
    {
        long maxFileSize = 1024 * 10;

        var uploadStream = await new StreamContent(e.File.OpenReadStream(maxFileSize)).ReadAsStreamAsync();
        var ms = new MemoryStream();
        await uploadStream.CopyToAsync(ms);
        var certBytes = ms.ToArray();

        var certViewModel = await DiscoveryService.UploadAnchorCertificate(Convert.ToBase64String(certBytes));
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapAnchorCertificateInfo), certViewModel);
        SetCertLoadedColor(certViewModel?.CertLoaded, ref AnchorLoadedColor);
    }

    public void Dispose()
    {
        _periodicTimer.Dispose();
        JSRuntime.InvokeVoidAsync("pageEventHandlers.unregisterHandlers");
    }
}