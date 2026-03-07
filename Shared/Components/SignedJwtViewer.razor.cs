#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.IdentityModel.Tokens;
using MudBlazor;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Components;

public partial class SignedJwtViewer : ComponentBase
{
    /// <summary>
    /// A Signed JWT
    /// </summary>
    [Parameter]
    public string? SignedSoftwareStatement { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public bool HighlightScopes { get; set; }

    [Inject]
    public IDiscoveryService MetadataService { get; set; } = null!;

    private string? _decodedJwt;
    private string? _previousInput;

    public string? DecodedJwt => _decodedJwt;

    /// <summary>
    /// Method invoked when the component has received parameters from its parent in
    /// the render tree, and the incoming values have been assigned to properties.
    /// </summary>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> representing any asynchronous operation.</returns>
    protected override async Task OnParametersSetAsync()
    {
        if (_previousInput != null && SignedSoftwareStatement != _previousInput)
        {
            _decodedJwt = string.Empty;
            StateHasChanged();
            await Task.Delay(200);
        }
        _previousInput = SignedSoftwareStatement;

        await BuildAccessTokenRequestVisualForClientCredentials(default);
        await base.OnParametersSetAsync();
    }

    public async Task BuildAccessTokenRequestVisualForClientCredentials(CancellationToken token)
    {
        await Task.Delay(1, token);
        if (SignedSoftwareStatement == null)
        {
            _decodedJwt = string.Empty;
            return;
        }

        try
        {
            var jwt = new JwtSecurityToken(SignedSoftwareStatement);
            using var jsonDocument = JsonDocument.Parse(jwt.Payload.SerializeToJson());
            var formattedStatement = JsonSerializer.Serialize(
                jsonDocument,
                new JsonSerializerOptions { WriteIndented = true }
            );

            var formattedHeader = UdapEd.Shared.JsonExtensions.FormatJson(Base64UrlEncoder.Decode(jwt.EncodedHeader));

            var sb = new StringBuilder();
            sb.AppendLine("<p class=\"text-line\">HEADER: <span>Algorithm & TOKEN TYPE</span></p>");

            sb.AppendLine(formattedHeader);
            sb.AppendLine("<p class=\"text-line\">PAYLOAD: <span>DATA</span></p>");
            if (HighlightScopes)
            {
                sb.AppendLine(formattedStatement.HighlightScope());
            }
            else
            {
                sb.AppendLine(formattedStatement);
            }

            _decodedJwt = CollapseX5cCerts(AddEpochTooltips(sb.ToString()));
        }
        catch (Exception ex)
        {
            _decodedJwt = SignedSoftwareStatement;
        }
    }

    private static readonly Regex X5cArrayRegex = new(
        @"(""x5c"":\s*\[)(.*?)(\])",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex X5cCertRegex = new(
        @"""([A-Za-z0-9+/=]{60,})""",
        RegexOptions.Compiled);

    private static string CollapseX5cCerts(string html)
    {
        return X5cArrayRegex.Replace(html, arrayMatch =>
        {
            var prefix = arrayMatch.Groups[1].Value;
            var content = arrayMatch.Groups[2].Value;
            var suffix = arrayMatch.Groups[3].Value;

            var replaced = X5cCertRegex.Replace(content, certMatch =>
            {
                var cert = certMatch.Groups[1].Value;
                var truncated = $"{cert[..20]}...{cert[^10..]}";
                return "<details class=\"x5c-collapsible\">" +
                       "<summary>" +
                       $"<span class=\"x5c-short\">\"{truncated}\"</span>" +
                       $"<span class=\"x5c-full\">\"{cert}\"</span>" +
                       "</summary></details>";
            });

            return $"{prefix}{replaced}{suffix}";
        });
    }

    private static readonly Regex EpochClaimRegex = new(
        @"(""(nbf|iat|exp)"":\s*)(\d+)",
        RegexOptions.Compiled);

    private static string ClaimFullName(string claim) => claim switch
    {
        "nbf" => "Not Before",
        "iat" => "Issued At",
        "exp" => "Expiration Time",
        _ => claim
    };

    private static string AddEpochTooltips(string html)
    {
        return EpochClaimRegex.Replace(html, match =>
        {
            var prefix = match.Groups[1].Value;
            var claimName = match.Groups[2].Value;
            var epochStr = match.Groups[3].Value;

            if (!long.TryParse(epochStr, out var epoch))
                return match.Value;

            var utc = DateTimeOffset.FromUnixTimeSeconds(epoch);
            var local = utc.ToLocalTime();
            var expired = claimName == "exp" && utc < DateTimeOffset.UtcNow;
            var cssClass = expired ? "epoch-tooltip epoch-expired" : "epoch-tooltip";

            return $"{prefix}<span class=\"{cssClass}\">{epochStr}<span class=\"epoch-tooltip-text\">{ClaimFullName(claimName)}<br/>{utc:yyyy-MM-dd HH:mm:ss} UTC<br/>{local:yyyy-MM-dd h:mm:ss tt} local</span></span>";
        });
    }
}
