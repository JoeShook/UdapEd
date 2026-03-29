using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using Color = MudBlazor.Color;

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

    public Color CertLoadedColor = Color.Default;
    public Color PrePackagedCertLoadedColor = Color.Default;

    private readonly IReadOnlyList<ClientCertEntry> _prePackagedCerts = PrePackagedClientCerts.Entries;

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

        if (clientCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            SetCertLoadedColor(clientCertificateLoadStatus.CertLoaded, ref CertLoadedColor);
        }
        else if (clientCertificateLoadStatus is { CertLoaded: CertLoadedEnum.Positive })
        {
            SetCertLoadedColor(clientCertificateLoadStatus.CertLoaded, ref PrePackagedCertLoadedColor);
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

        if (clientCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            SetCertLoadedColor(clientCertificateLoadStatus.CertLoaded, ref CertLoadedColor);
        }
        else if (clientCertificateLoadStatus is { CertLoaded: CertLoadedEnum.Positive })
        {
            SetCertLoadedColor(clientCertificateLoadStatus.CertLoaded, ref PrePackagedCertLoadedColor);
        }

        _checkServerSession = true;
    }

    private void OnPageBlurInstance()
    {
        _checkServerSession = false;
    }

    private async Task LoadPrePackagedCert(ClientCertEntry cert)
    {
        var certViewModel = await RegisterService.LoadTestCertificate(cert.CertificateName);
        SetCertLoadedColor(certViewModel?.CertLoaded, ref PrePackagedCertLoadedColor);
        await AppState.SetPropertyAsync(this, nameof(AppState.UdapClientCertificateInfo), certViewModel);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);
        await OnCertificateLoaded.InvokeAsync();
    }

    private void SetCertLoadedColor(CertLoadedEnum? isCertLoaded, ref Color certColor)
    {
        CertLoadedColor = Color.Default;
        PrePackagedCertLoadedColor = Color.Default;

        switch (isCertLoaded)
        {
            case CertLoadedEnum.Negative:
                certColor = Color.Default;
                AppState.SetProperty(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Positive:
                certColor = Color.Success;
                AppState.SetProperty(this, nameof(AppState.CertificateLoaded), true);
                break;
            case CertLoadedEnum.InvalidPassword:
                certColor = Color.Warning;
                AppState.SetProperty(this, nameof(AppState.CertificateLoaded), false);
                break;
            case CertLoadedEnum.Expired:
                certColor = Color.Error;
                AppState.SetProperty(this, nameof(AppState.CertificateLoaded), false);
                break;
            default:
                certColor = Color.Error;
                AppState.SetProperty(this, nameof(AppState.CertificateLoaded), false);
                break;
        }

        this.StateHasChanged();
    }

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
        SetCertLoadedColor(certViewModel?.CertLoaded, ref CertLoadedColor);
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
                SetCertLoadedColor(clientCertificateLoadStatus?.CertLoaded, ref CertLoadedColor);
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
