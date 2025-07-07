#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Collections;
using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;
using MudBlazor;
using Udap.Model;
using UdapEd.Shared.Components;
using UdapEd.Shared.Services;
using UdapEd.Shared.Model.Discovery;

namespace UdapEd.Shared.Pages;

public partial class UdapDiscovery
{
    [CascadingParameter]
    public CascadingAppState AppState { get; set; } = null!;
    
    ErrorBoundary? ErrorBoundary { get; set; }
    
    [Inject] IDiscoveryService MetadataService { get; set; } = null!;

    [Inject] NavigationManager NavigationManager { get; set; } = null!;
    
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    [Inject] IDialogService DialogService { get; set; } = null!;

    private string? _result;

    private string Result
    {
        get
        {
            if (_result != null)
            {
                return _result;
            }   

            if (AppState.MetadataVerificationModel == null ||
                AppState.MetadataVerificationModel.UdapServerMetaData == null)
            {
                return _result ?? string.Empty;
            }

            // Calculate and update model size
            UpdateModelSize(AppState.MetadataVerificationModel.UdapServerMetaData);

            return JsonSerializer.Serialize(AppState.MetadataVerificationModel.UdapServerMetaData, 
                new JsonSerializerOptions
                {
                    WriteIndented = true, 
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
        }
        set => _result = value;
    }

    private string? _baseUrl;

    private string? BaseUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(_baseUrl))
            {
                return _baseUrl;
            }

            _baseUrl = AppState.BaseUrl;

            return _baseUrl;
        }
        set
        {
            if (_baseUrl != value)
            {
                Reset();
            }

            _baseUrl = value;
        }
    }

    private string? _community;
    private CertificatePKIViewer? _certificateViewer;

    private string? Community
    {
        get
        {
            if (!string.IsNullOrEmpty(_community))
            {
                return _community;
            }

            _community = AppState.Community;

            return _community;
        }
        set
        {
            _community = value;
            AppState.SetProperty(this, nameof(AppState.Community), _community);
        }
    }

    private long? _modelSizeInBytes;
    public long? ModelSizeInBytes
    {
        get => _modelSizeInBytes;
        private set => _modelSizeInBytes = value;
    }

    public string ModelSizeInBytesFormatted => ModelSizeInBytes.HasValue ? ModelSizeInBytes.Value.ToString("N0") : string.Empty;
    
    public Adornment KnownCommunityAdornment
    {
        get => BaseUrl.Equals("https://fhirlabs.net/fhir/r4", StringComparison.OrdinalIgnoreCase) ? Adornment.End : Adornment.None;
    }

    private bool _isLoading;
    

    protected override async Task OnInitializedAsync()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var queryParams = !string.IsNullOrEmpty(uri.Query) ? QueryHelpers.ParseQuery(uri.Query) : null;
        _baseUrl = queryParams?.GetValueOrDefault("BaseUrl") ?? AppState.BaseUrl;
    }

    private void Reset()
    {
        if (AppState.MetadataVerificationModel != null)
        {
            AppState.MetadataVerificationModel.Problems = new List<string>();
            AppState.MetadataVerificationModel.Untrusted = new List<string>();
            AppState.MetadataVerificationModel.TokenErrors = new List<string>();
        }

        if (AppState.MetadataVerificationModel != null)
        {
            AppState.MetadataVerificationModel.UdapServerMetaData = null;
        }

        Result = string.Empty;
        StateHasChanged();
    }

    //
    // Button 
    //
    private async Task GetMetadata()
    {
        _certificateViewer?.Reset();
        Result = "Loading ...";
        _isLoading = true;
        await Task.Delay(1000);

        try
        {
            if (BaseUrl != null)
            {
                var model = await MetadataService.GetUdapMetadataVerificationModel(BaseUrl, Community, default);
                await AppState.SetPropertyAsync(this, nameof(AppState.MetadataVerificationModel), model);
                UpdateModelSize(model.UdapServerMetaData);
            }

            Result = AppState.MetadataVerificationModel?.UdapServerMetaData != null
                ? JsonSerializer.Serialize(AppState.MetadataVerificationModel.UdapServerMetaData, 
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    })
                : string.Empty;
            await AppState.SetPropertyAsync(this, nameof(AppState.BaseUrl), BaseUrl);

            if (_result != null && _result.Contains("udap_versions_supported"))
            {
                await AppendOrMoveBaseUrl(BaseUrl);
            }
            else if(Community == null)
            {
                await RemoveBaseUrl();
            }
        }
        catch (Exception ex)
        {
            Result = ex.Message;
            await AppState.SetPropertyAsync(this, nameof(AppState.MetadataVerificationModel), null);
            UpdateModelSize(null);
        }
        finally
        {
            _isLoading = false;
        }
    }

    //
    // Just validates that a url in the list view are valid
    //
    private async Task<IEnumerable<string>> GetMetadata(string value, CancellationToken token)
    {
        await Task.Delay(5, token);

        if (AppState.BaseUrls == null)
        {
            await AppState.SetPropertyAsync(this, nameof(AppState.BaseUrls), new OrderedDictionary(), true, false);
        }

        if ((value == null && AppState.BaseUrls != null) 
            || (value != null && AppState.BaseUrls != null && AppState.BaseUrls!.Contains(value)))
        {
            return AppState.BaseUrls.Cast<DictionaryEntry>().Select(e => (string)e.Key);
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var _))
        {
            if (BaseUrl != null)
            {
                var result = await MetadataService.GetUdapMetadataVerificationModel(BaseUrl, Community, token);

                if (result != null)
                {
                    await AppState.SetPropertyAsync(this, nameof(AppState.MetadataVerificationModel), result);
                    UpdateModelSize(result.UdapServerMetaData);
                }

                if (result != null && result.UdapServerMetaData != null)
                {
                    await AppendOrMoveBaseUrl(BaseUrl);
                }
            }
        }

        return AppState.BaseUrls.Cast<DictionaryEntry>().Select(e => (string)e.Key);
    }

    private async Task AppendOrMoveBaseUrl(string? appStateBaseUrl)
    {
        var baseUrls = AppState.BaseUrls;
        if (baseUrls != null && appStateBaseUrl != null)
        {
            if (!baseUrls.Contains(appStateBaseUrl) && !baseUrls.Contains(appStateBaseUrl.TrimEnd('/')))
            {
                baseUrls.Insert(0, appStateBaseUrl, null);
            }
            else
            {   //Move
                baseUrls.Remove(appStateBaseUrl);
                baseUrls.Insert(0, appStateBaseUrl, null);
            }
            await AppState.SetPropertyAsync(this, nameof(AppState.BaseUrls), baseUrls);
        }
    }
    
    private async Task RemoveBaseUrl()
    {
        bool confirmed = await JsRuntime.InvokeAsync<bool>("confirm", "Metadata doesn't exist.  Would you like to delete this url?");
        if (!confirmed)
        {
            return;
        }

        Result = "Saving ...";
        await Task.Delay(50);
        var baseUrls = AppState.BaseUrls;
        if (BaseUrl != null)
        {
            baseUrls?.Remove(BaseUrl);
        }
        
        BaseUrl = string.Empty;
        await AppState.SetPropertyAsync(this, nameof(AppState.BaseUrls), baseUrls);
        await AppState.SetPropertyAsync(this, nameof(AppState.BaseUrl), string.Empty);

        Result = "";
        NavigationManager.NavigateTo("udapDiscovery", true);
    }

    protected override void OnParametersSet()
    {
        ErrorBoundary?.Recover();
    }

    private string GetJwtHeader()
    {
        var jwtEncodedString = AppState.MetadataVerificationModel?.UdapServerMetaData?.SignedMetadata;
        
        if (string.IsNullOrEmpty(jwtEncodedString))
        {
            return string.Empty;
        }

        var jwt = new JwtSecurityToken(jwtEncodedString);
        return JsonExtensions.FormatJson(Base64UrlEncoder.Decode(jwt.EncodedHeader));
    }

    private void UpdateModelSize(UdapMetadata? model)
    {
        if (model == null)
        {
            ModelSizeInBytes = null;
            return;
        }
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // WriteIndented = true
        });
        ModelSizeInBytes = System.Text.Encoding.UTF8.GetByteCount(json);
    }

    private async Task CopyResultToClipboard()
    {
        if (!string.IsNullOrEmpty(Result))
        {
            await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", Result);
        }
    }

    private async Task SelectKnownCommunities()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<SelectKnownCommunity_Dialog>("Select a known community", options);
        var result = await dialog.Result;
        Community = result.Data?.ToString() ?? "";
    }
}
