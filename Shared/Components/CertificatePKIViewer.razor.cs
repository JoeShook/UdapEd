using Microsoft.AspNetCore.Components;
using MudBlazor;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Components;

public partial class CertificatePKIViewer : ComponentBase
{
    [Parameter] public bool EndCertificate { get; set; }
    [Parameter] public bool EnableAddToClaim { get; set; }
    [Parameter] public string? CertContext { get; set; }
    [Parameter] public EventCallback<string?> IntermediateAddedToX5cEvent { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter] public string? Title { get; set; } = "Certificates in x5c header";
    [Parameter] public string Class { get; set; }

    [Parameter]
    public CertificateViewModel? EncodeCertificate { get; set; }

    [Parameter] public string? JwtHeaderWithx5c { get; set; }
    
    [Inject] private IDiscoveryService MetadataService { get; set; } = null!;
    [Inject] private IInfrastructure Infrastructure { get; set; } = null!;
    [Inject] ISnackbar SnackBar { get; set; } = null!;

    private CertificateViewModel? IntermediateCertificate { get; set; }

    private string? Crl { get; set; }
    public CrlFileCacheSettings? CrlUrlCache { get; set; }


    public void Reset()
    {
        ChildContent = null;
        IntermediateCertificate = null;
        Crl = null;
        StateHasChanged();
    }

    private async Task RemoveCrlFromCache(CrlFileCacheSettings crlUrlCache)
    {
        await Infrastructure.RemoveFromFileCache(crlUrlCache);

        if (!string.IsNullOrEmpty(crlUrlCache.UrlPath))
        {
            await Infrastructure.ClearCrlCache(crlUrlCache.UrlPath);
        }

        CrlUrlCache = null;
        Crl = null;

        SnackBar.Add("CRL removed from file cache and in-memory cache. " +
                     "Click Resolve to fetch the latest CRL.", Severity.Success);
    }
}