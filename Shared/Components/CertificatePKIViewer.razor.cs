using Microsoft.AspNetCore.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Components;

public partial class CertificatePKIViewer : ComponentBase
{
    /// <summary>
    /// A X509Certificate as a Base64 encoding of the DER representation
    /// </summary>
    [Parameter]
    public string? Certificate { get; set; }

    [Parameter] public string? JwtHeaderWithx5c { get; set; }
    [Inject] private IDiscoveryService MetadataService { get; set; } = null!;
    [Inject] private IInfrastructure Infrastructure { get; set; } = null!;


    private CertificateViewModel? IntermediateCertificate { get; set; }

    private string? CRL { get; set; }
}