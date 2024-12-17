#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

// using BQuery;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using Color = MudBlazor.Color;
using FocusEventArgs = Microsoft.AspNetCore.Components.Web.FocusEventArgs;

namespace UdapEd.Shared.Components;

public partial class CertificationAndEndorsementLoader
{
    private static CertificationAndEndorsementLoader? _instance;
    [Inject] private ICertificationService CertificationService { get; set; } = null!;
    [CascadingParameter] private CascadingAppState AppState { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    private readonly PeriodicTimer _periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
    private bool _checkServerSession;

    protected override async Task OnInitializedAsync()
    {
        _instance = this;

        var userSuppliedCertificate = AppState.CertificationAndEndorsementInfo?.UserSuppliedCertificate;
        var clientCertificateLoadStatus = await CertificationService.ClientCertificateLoadStatus();

        if (clientCertificateLoadStatus != null)
        {
            clientCertificateLoadStatus.UserSuppliedCertificate = userSuppliedCertificate ?? false;
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.CertificationAndEndorsementInfo),
            clientCertificateLoadStatus);

        if (clientCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            await SetCertLoadedColor(clientCertificateLoadStatus.CertLoaded);
        }
        else
        {
            await SetCertLoadedColorFoExample(clientCertificateLoadStatus.CertLoaded);
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


    public async Task OnPageFocusInstance()
    {
        var userSuppliedCertificate = AppState.CertificationAndEndorsementInfo?.UserSuppliedCertificate;
        var clientCertificateLoadStatus = await CertificationService.ClientCertificateLoadStatus();

        if (clientCertificateLoadStatus != null)
        {
            clientCertificateLoadStatus.UserSuppliedCertificate = userSuppliedCertificate ?? false;
        }

        await AppState.SetPropertyAsync(this, nameof(AppState.CertificationAndEndorsementInfo),
            clientCertificateLoadStatus);

        if (clientCertificateLoadStatus is { UserSuppliedCertificate: true })
        {
            await SetCertLoadedColor(clientCertificateLoadStatus?.CertLoaded);
        }
        else
        {
            await SetCertLoadedColorFoExample(clientCertificateLoadStatus?.CertLoaded);
        }

        _checkServerSession = true;
    }

    public void OnPageBlurInstance()
    {
        _checkServerSession = false;
    }

    private async Task SetCertLoadedColor(CertLoadedEnum? isCertLoaded)
    {
        CertLoadedColorForDefaultCommunity = Color.Default;

        switch (isCertLoaded)
        {
            case CertLoadedEnum.Negative:
                CertLoadedColor = Color.Default;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
            case CertLoadedEnum.Positive:
                CertLoadedColor = Color.Success;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), true);
                break;
            case CertLoadedEnum.InvalidPassword:
                CertLoadedColor = Color.Warning;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
            case CertLoadedEnum.Expired:
                CertLoadedColor = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
            default:
                CertLoadedColor = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
        }

        this.StateHasChanged();
    }

    private async Task SetCertLoadedColorFoExample(CertLoadedEnum? isCertLoaded)
    {
        CertLoadedColor = Color.Default;

        switch (isCertLoaded)
        {
            case CertLoadedEnum.Negative:
                CertLoadedColorForDefaultCommunity = Color.Default;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
            case CertLoadedEnum.Positive:
                CertLoadedColorForDefaultCommunity = Color.Success;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), true);
                break;
            case CertLoadedEnum.InvalidPassword:
                CertLoadedColorForDefaultCommunity = Color.Warning;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
            case CertLoadedEnum.Expired:
                CertLoadedColorForDefaultCommunity = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
            default:
                CertLoadedColorForDefaultCommunity = Color.Error;
                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationCertLoaded), false);
                break;
        }

        this.StateHasChanged();
    }

    public Color CertLoadedColor { get; set; } = Color.Default;
    public Color CertLoadedColorForDefaultCommunity { get; set; } = Color.Default;

    private async Task UploadFilesAsync(InputFileChangeEventArgs e)
    {
        long maxFileSize = 1024 * 10;

        var uploadStream = await new StreamContent(e.File.OpenReadStream(maxFileSize)).ReadAsStreamAsync();
        var ms = new MemoryStream();
        await uploadStream.CopyToAsync(ms);
        var certBytes = ms.ToArray();

        await CertificationService.UploadCertificate(Convert.ToBase64String(certBytes));

        //dialog
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<Password_Dialog>("Certificate Password", options);
        var result = await dialog.Result;
        var certViewModel = await CertificationService.ValidateCertificate(result.Data?.ToString() ?? "");
        await SetCertLoadedColor(certViewModel?.CertLoaded);
        await AppState.SetPropertyAsync(this, nameof(AppState.CertificationAndEndorsementInfo), certViewModel);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);
        await OnCertificationCertLoaded.InvokeAsync();
    }

    private async Task LoadFhirLabsExampleCertificatationCertificate()
    {
        var certViewModel = await CertificationService.LoadTestCertificate("FhirLabsAdminCertification.pfx");
        await SetCertLoadedColorFoExample(certViewModel?.CertLoaded);
        await AppState.SetPropertyAsync(this, nameof(AppState.CertificationAndEndorsementInfo), certViewModel);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientMode), ClientSecureMode.UDAP);
        await OnCertificationCertLoaded.InvokeAsync();
    }

    private async void RunTimer()
    {
        while (await _periodicTimer.WaitForNextTickAsync())
        {
            if (_checkServerSession)
            {
                var userSuppliedCertificate = AppState.CertificationAndEndorsementInfo?.UserSuppliedCertificate;
                var clientCertificateLoadStatus = await CertificationService.ClientCertificateLoadStatus();

                if (clientCertificateLoadStatus != null)
                {
                    clientCertificateLoadStatus.UserSuppliedCertificate = userSuppliedCertificate ?? false;
                }

                await AppState.SetPropertyAsync(this, nameof(AppState.CertificationAndEndorsementInfo),
                    clientCertificateLoadStatus);
                await SetCertLoadedColor(clientCertificateLoadStatus?.CertLoaded);
            }
        }
    }

    [Parameter] public EventCallback OnCertificationCertLoaded { get; set; }

    public void Dispose()
    {
        _periodicTimer.Dispose();
        JSRuntime.InvokeVoidAsync("pageEventHandlers.unregisterHandlers");
    }

    private async Task ClearCertificationAndEndorsementInfo()
    {
        await CertificationService.RemoveCertificate();
        await SetCertLoadedColor(CertLoadedEnum.Negative);
        await SetCertLoadedColorFoExample(CertLoadedEnum.Negative);
    }
}