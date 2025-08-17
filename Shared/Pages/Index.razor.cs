#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using UdapEd.Shared.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Pages;

public partial class Index
{
    [CascadingParameter] public CascadingAppState AppState { get; set; } = null!;
    [Inject] ISnackbar Snackbar { get; set; } = null!;
    [Inject] IFileSaveService FileSaveService { get; set; } = null!;
    [Inject] IJSRuntime JSRuntime { get; set; } = null!;

    private ErrorBoundary? ErrorBoundary { get; set; }
    private string _myIp = string.Empty;
    private string _fhirLabsCommunityList = string.Empty;
    private bool _rowInEditMode;

    protected override async Task OnInitializedAsync()
    {
        _myIp = await Infrastructure.GetMyIp();

        await GetCommunities();
        Headers = AppState.ClientHeaders?.Headers ?? new List<ClientHeader>();
    }

    protected override void OnParametersSet()
    {
        ErrorBoundary?.Recover();
    }

    private async Task GetCommunities()
    {
        _fhirLabsCommunityList = await DiscoveryService.GetFhirLabsCommunityList();
    }

    private List<ClientHeader> Headers { get; set; } = new();

    private void AddRow()
    {
        Headers.Add(new ClientHeader());
        _rowInEditMode = true;
    }

    private async Task RemoveClientHeader(ClientHeader clientHeader)
    {
        var header = Headers.FirstOrDefault(h => h.Name == clientHeader.Name);
        if (header != null) Headers.Remove(header);
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientHeaders), Headers);
    }

    private async Task CommitItem(object obj)
    {
        await AppState.SetPropertyAsync(this, nameof(AppState.ClientHeaders), new ClientHeaders { Headers = Headers });
        await DiscoveryService.SetClientHeaders(Headers.ToDictionary(h => h.Name, h => h.Value));
        _rowInEditMode = false;
    }

    private void ItemHasBeenCanceled(object obj)
    {
        Headers.Remove((ClientHeader)obj);
        _rowInEditMode = false;
    }

    private string subjectAltName = "";
    private List<string> SubjectAltNames { get; set; } = new List<string>();

    private void AddSubjectAltName()
    {
        if (!SubjectAltNames.Contains(subjectAltName) && SubjectAltNames.Count < 10)
        {
            if (Uri.TryCreate(subjectAltName, UriKind.Absolute, out var uri))
            {
                SubjectAltNames.Add(uri.AbsoluteUri);
                subjectAltName = "";
                StateHasChanged();
            }
        }
    }

    private void RemoveSubjectAltName(string item)
    {
        SubjectAltNames.Remove(item);
        StateHasChanged();
    }
    
    private async Task BuildMyTestCertificatePackage()
    {
        byte[] zipFile = await Infrastructure.BuildMyTestCertificatePackage(SubjectAltNames);

        // Delegate saving to a platform-specific service
        await FileSaveService.SaveAsync("UdapEdCertificatePack.zip", zipFile, "application/zip");
    }

    private void Callback(string obj)
    {
        throw new NotImplementedException();
    }

    // Clears a named entry from the browser's local storage
    private async Task ClearLocalStore()
    {
        await AppState.ResetStateAsync();
        Snackbar.Add("Local store cleared.", Severity.Success, config => { config.VisibleStateDuration = 2000; });
    }
}