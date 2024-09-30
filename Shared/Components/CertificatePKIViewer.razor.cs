using Microsoft.AspNetCore.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Components;

public partial class CertificatePKIViewer : ComponentBase
{
    [Parameter]
    public RenderFragment ChildContent { get; set; }

    [Parameter] public string? Title { get; set; } = "Certificates in x5c header";
    [Parameter] public string Class { get; set; }

    [Parameter]
    public CertificateViewModel? EncodeCertificate { get; set; }

    [Parameter] public string? JwtHeaderWithx5c { get; set; }
    [Inject] private IDiscoveryService MetadataService { get; set; } = null!;
    [Inject] private IInfrastructure Infrastructure { get; set; } = null!;


    private CertificateViewModel? IntermediateCertificate { get; set; }

    private string? CRL { get; set; }

    
}