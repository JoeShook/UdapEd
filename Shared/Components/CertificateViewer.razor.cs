#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Components;

public partial class CertificateViewer : ComponentBase
{
    private CancellationTokenSource _cts = new();
    
    [Parameter] public string Class { get; set; }

    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// The public X509 Certificate to decompose,  It is in a JWT Header part as a json string
    /// </summary>
    [Parameter] public string? JwtHeader { get; set; }

    /// <summary>
    /// The public X509 Certificate to decompose.  It is in Base64 format
    /// </summary>
    [Parameter]
    public CertificateViewModel? EncodeCertificate
    {
        get => _certificateView;
        set => _certificateView = value;
    }

    [Inject] private IDiscoveryService MetadataService { get; set; } = null!;
    [Inject] private IInfrastructure Infrastructure { get; set; } = null!;
    private Dictionary<string, string> _certificateMetadata = new Dictionary<string, string>();
    private CertificateViewModel? _certificateView;
    private List<string>? _thumbPrints;
    private List<string>? _aiaPaths;

    [Parameter]
    public EventCallback<CertificateViewModel?> IntermediateResolvedEvent { get; set; }

    [Parameter]
    public EventCallback<string?> CrlResolvedEvent { get; set; }

    [Parameter]
    public EventCallback<CrlFileCacheSettings?> CrlCachedEvent { get; set; }

    public List<X509CacheSettings>? StoreCache { get; set; }
    public List<CrlFileCacheSettings>? FileCache { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await FindX509StoreCache();
    }

    private async Task FindX509StoreCache()
    {
        _thumbPrints = _certificateView?.TableDisplay
            .SelectMany(list => list)
            .Where(x => x.Key == "Thumbprint SHA1")
            .Select(x => x.Value)
            .ToList();

        _aiaPaths = _certificateView?.TableDisplay
            .SelectMany(list => list)
            .Where(x => x.Key == "Authority Information Access")
            .Select(x => x.Value)
            .ToList();

        if (_thumbPrints != null)
        {
            StoreCache = new List<X509CacheSettings>();
            foreach (var thumbPrint in _thumbPrints)
            {
                var cache = await Infrastructure.GetX509StoreCache(thumbPrint);
                if (cache != null)
                {
                    StoreCache.Add(cache);
                }
            }
        }

        if (_aiaPaths != null)
        {
            FileCache = new List<CrlFileCacheSettings>();
            foreach (var aiaPath in _aiaPaths)
            {
                var cache = await Infrastructure.GetCryptNetUrlCache(aiaPath);
                if (cache != null)
                {
                    FileCache.Add(cache);
                }
            }
        }
    }

    /// <summary>
    /// Method invoked when the component has received parameters from its parent in
    /// the render tree, and the incoming values have been assigned to properties.
    /// </summary>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing any asynchronous operation.</returns>
    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrEmpty(JwtHeader) && EncodeCertificate == null)
        {
            _certificateView = default;
            return;
        }

        if (!string.IsNullOrEmpty(JwtHeader) && !JwtHeader.Equals("Loading ..."))
        {
            var document = JsonDocument.Parse(JwtHeader);
            var root = document.RootElement;
            var certificates = root.TryGetStringArray("x5c");
            _certificateView = await MetadataService.GetCertificateData(certificates, _cts.Token);
        }
    }

    /// <summary>Performs application-defined tasks associated with freeing, 
    /// releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
    }

    private async Task ResolveIntermediate(string url)
    {
        var viewModel = await Infrastructure.GetX509ViewModel(url);
        await IntermediateResolvedEvent.InvokeAsync(viewModel);
    }

    private async Task AddIntermediateToX5c(string url)
    {
        await Infrastructure.GetIntermediateX509(url);
    }

    private async Task ResolveCrl(string url)
    {
        var crl = await Infrastructure.GetCrldata(url);
        var crlFileCache = await Infrastructure.GetCryptNetUrlCache(url);
        await CrlResolvedEvent.InvokeAsync(crl);
        await CrlCachedEvent.InvokeAsync(crlFileCache);
    }

    private async Task RemoveFromStore()
    {
        if (_thumbPrints != null && StoreCache != null)
        {
            foreach (var thumbPrint in _thumbPrints)
            {
                var cache = StoreCache.FirstOrDefault(c => c.Thumbprint == thumbPrint);
                if (cache != null)
                {
                    await Infrastructure.RemoveFromX509Store(cache);
                }
            }
            await FindX509StoreCache();
        }
    }

    private async Task RemoveFromFileCache()
    {
        if (_aiaPaths != null && FileCache != null)
        {
            foreach (var aiaPath in _aiaPaths)
            {
                var cache = FileCache.FirstOrDefault(c => c.UrlPath == aiaPath);
                if (cache != null)
                {
                    await Infrastructure.RemoveFromFileCache(cache);
                }
            }
            FileCache = null;
        }
    }
}