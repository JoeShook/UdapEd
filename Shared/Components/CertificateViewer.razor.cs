﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using IdentityModel.Client;
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

    [Parameter]
    public EventCallback<CertificateViewModel?> IntermediateResolved { get; set; }

    [Parameter]
    public EventCallback<string?> CrlResolved { get; set; }

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
        var viewModel = await Infrastructure.GetX509data(url);
        await IntermediateResolved.InvokeAsync(viewModel);
    }

    private async Task ResolveCrl(string url)
    {
        var crl = await Infrastructure.GetCrldata(url);
        await CrlResolved.InvokeAsync(crl);
    }
}