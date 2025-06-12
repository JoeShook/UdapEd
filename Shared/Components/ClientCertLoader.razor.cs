using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using Color = MudBlazor.Color;
using FocusEventArgs = Microsoft.AspNetCore.Components.Web.FocusEventArgs;

namespace UdapEd.Shared.Components;

public partial class ClientCertLoader: ComponentBase, IDisposable
{
    private static ClientCertLoader? _instance;
    [Inject] private IRegisterService RegisterService { get; set; } = null!;
    [CascadingParameter] private CascadingAppState AppState { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    private readonly PeriodicTimer _periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
    private bool _checkServerSession;

    protected override async Task OnInitializedAsync()
    {
        _instance = this;
        var userSuppliedCertificate = AppState.UdapClientCertificateInfo?.UserSuppliedCertificate;
        var clientCertificateLoadStatus = await RegisterService.ClientCertificateLoadStatus();

        if (clientCertificateLoadStatus != null)
        {
            clientCertificateLoadStatus.UserSuppliedCertificate = userSuppliedCertificate ?? false;
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.UdapClientCertificateInfo), clientCertificateLoadStatus);

        if (clientCertificateLoadStatus != null &&
            clientCertificateLoadStatus.Issuer.StartsWith("EMR Direct Test Client SubCA"))
        {
            await SetCertLoadedColorForDefaultCommunity(clientCertificateLoadStatus.CertLoaded);
        }
        else if (clientCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            await SetCertLoadedColor(clientCertificateLoadStatus.CertLoaded);
        }
        else if (clientCertificateLoadStatus != null &&
                 clientCertificateLoadStatus.Issuer.StartsWith("SureFhir-Intermediate"))
        {
            await SetCertLoadedColorForFhirLabs(clientCertificateLoadStatus.CertLoaded);
        }


        await JSRuntime.InvokeVoidAsync("pageEventHandlers.registerHandlers");
        RunTimer();
    }

    [JSInvokable("ClientCertLoader_OnPageFocus")]
    public static async Task OnPageFocus()
    {
        if (_instance != null)
        {
            await _instance.OnPageFocusInstance();
        }
    }

    [JSInvokable("ClientCertLoader_OnPageBlur")]
    public static void OnPageBlur()
    {
        _instance?.OnPageBlurInstance();
    }

    private async Task OnPageFocusInstance()
    {
        var userSuppliedCertificate = AppState.UdapClientCertificateInfo?.UserSuppliedCertificate;
        var clientCertificateLoadStatus = await RegisterService.ClientCertificateLoadStatus();

        if (clientCertificateLoadStatus != null)
        {
            clientCertificateLoadStatus.UserSuppliedCertificate = userSuppliedCertificate ?? false;
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.UdapClientCertificateInfo), clientCertificateLoadStatus);

        if (clientCertificateLoadStatus != null &&
            clientCertificateLoadStatus.Issuer.StartsWith("EMR Direct Test Client SubCA"))
        {
            await SetCertLoadedColorForDefaultCommunity(clientCertificateLoadStatus?.CertLoaded);
        }
        else if (clientCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            await SetCertLoadedColor(clientCertificateLoadStatus?.CertLoaded);
        }
        else if (clientCertificateLoadStatus != null &&
                 clientCertificateLoadStatus.Issuer.StartsWith("SureFhir-Intermediate"))
        {
            await SetCertLoadedColorForFhirLabs(clientCertificateLoadStatus?.CertLoaded);
        }

        _checkServerSession = true;
    }

    private void OnPageBlurInstance()
    {
        _checkServerSession = false;
    }

    private async Task SetCertLoadedColor(CertLoadedEnum? isCertLoaded)
    {
        CertLoadedColorForFhirLabs = Color.Default;
        CertLoadedColorForDefaultCommunity = Color.Default;

        switch (isCertLoaded)
        {
            case CertLoadedEnum.Negative:
                CertLoadedColor = Color.Default;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Positive:
                CertLoadedColor = Color.Success;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), true);
                break;
            case CertLoadedEnum.InvalidPassword:
                CertLoadedColor = Color.Warning;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Expired:
                CertLoadedColor = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            default:
                CertLoadedColor = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
        }

        this.StateHasChanged();
    }

    private async Task SetCertLoadedColorForFhirLabs(CertLoadedEnum? isCertLoaded)
    {
        CertLoadedColor = Color.Default;
        CertLoadedColorForDefaultCommunity = Color.Default;

        switch (isCertLoaded)
        {
            case CertLoadedEnum.Negative:
                CertLoadedColorForFhirLabs = Color.Default;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Positive:
                CertLoadedColorForFhirLabs = Color.Success;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), true);
                break;
            case CertLoadedEnum.InvalidPassword:
                CertLoadedColorForFhirLabs = Color.Warning;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Expired:
                CertLoadedColorForFhirLabs = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            default:
                CertLoadedColorForFhirLabs = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
        }

        this.StateHasChanged();
    }

    private async Task SetCertLoadedColorForDefaultCommunity(CertLoadedEnum? isCertLoaded)
    {
        CertLoadedColor = Color.Default;
        CertLoadedColorForFhirLabs = Color.Default;

        switch (isCertLoaded)
        {
            case CertLoadedEnum.Negative:
                CertLoadedColorForDefaultCommunity = Color.Default;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Positive:
                CertLoadedColorForDefaultCommunity = Color.Success;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), true);
                break;
            case CertLoadedEnum.InvalidPassword:
                CertLoadedColorForDefaultCommunity = Color.Warning;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Expired:
                CertLoadedColorForDefaultCommunity = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
            default:
                CertLoadedColorForDefaultCommunity = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificateLoaded), false);
                break;
        }

        this.StateHasChanged();
    }

    public Color CertLoadedColor { get; set; } = Color.Default;
    public Color CertLoadedColorForFhirLabs { get; set; } = Color.Default;
    public Color CertLoadedColorForDefaultCommunity { get; set; } = Color.Default;

    private async Task UploadFilesAsync(InputFileChangeEventArgs e)
    {
        long maxFileSize = 1024 * 10;

        var uploadStream = await new StreamContent(e.File.OpenReadStream(maxFileSize)).ReadAsStreamAsync();
        var ms = new MemoryStream();
        await uploadStream.CopyToAsync(ms);
        var certBytes = ms.ToArray();

        await RegisterService.UploadClientCertificate(Convert.ToBase64String(certBytes));

        //dialog
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<Password_Dialog>("Certificate Password", options);
        var result = await dialog.Result;
        var certViewModel = await RegisterService.ValidateCertificate(result.Data?.ToString() ?? "");
        await SetCertLoadedColor(certViewModel?.CertLoaded);
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapClientCertificateInfo), certViewModel);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);
        await OnCertificateLoaded.InvokeAsync();
    }

    private async Task LoadFhirLabsTestCertificate()
    {
        var certViewModel = await RegisterService.LoadTestCertificate("./CertificateStore/fhirlabs.net.client.pfx");
        await SetCertLoadedColorForFhirLabs(certViewModel?.CertLoaded);
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapClientCertificateInfo), certViewModel);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);
        await OnCertificateLoaded.InvokeAsync();
    }

    private async Task LoadEmrTestCertificate()
    {
        var certViewModel = await RegisterService.LoadTestCertificate("./CertificateStore/udap.emrdirect.client.certificate.p12");
        await SetCertLoadedColorForDefaultCommunity(certViewModel?.CertLoaded);
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapClientCertificateInfo), certViewModel);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);
        await OnCertificateLoaded.InvokeAsync();
    }

    private async void RunTimer()
    {
        while (await _periodicTimer.WaitForNextTickAsync())
        {
            if (_checkServerSession)
            {
                var userSuppliedCertificate = AppState.UdapClientCertificateInfo?.UserSuppliedCertificate;
                var clientCertificateLoadStatus = await RegisterService.ClientCertificateLoadStatus();

                if (clientCertificateLoadStatus != null)
                {
                    clientCertificateLoadStatus.UserSuppliedCertificate = userSuppliedCertificate ?? false;
                }

                await AppState.SetPropertyAsync(this, nameof(AppState.UdapClientCertificateInfo),
                    clientCertificateLoadStatus);
                await SetCertLoadedColor(clientCertificateLoadStatus?.CertLoaded);
            }
        }
    }

    [Parameter] public EventCallback OnCertificateLoaded { get; set; }

    public void Dispose()
    {
        _periodicTimer.Dispose();
        JSRuntime.InvokeVoidAsync("pageEventHandlers.unregisterHandlers");
    }
}