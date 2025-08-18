using System.Collections.Immutable;
using UdapEd.Shared.Components;
using UdapEd.Shared.Model;

namespace UdapEd.Shared.Extensions;

public static class ClientRegistrationFilterExtensions
{
    /// <summary>
    /// Filters registrations to those matching the current certificate (SAN), resource server (BaseUrl),
    /// plus any optional additional predicate.
    /// </summary>
    public static IDictionary<string, ClientRegistration?> FilterRegistrations(
        this ClientRegistrations? source,
        CascadingAppState appState,
        Func<ClientRegistration, bool>? predicate = null)
    {
        if (source?.Registrations == null ||
            appState.UdapClientCertificateInfo == null ||
            appState.UdapClientCertificateInfo?.SubjectAltNames == null)
        {
            return new Dictionary<string, ClientRegistration?>();
        }

        var query = source.Registrations.Where(r =>
            r.Value != null &&
            appState.UdapClientCertificateInfo.SubjectAltNames.Contains(r.Value.SubjAltName) &&
            appState.BaseUrl == r.Value.ResourceServer &&
            appState.MetadataVerificationModel?.UdapServerMetaData?.RegistrationEndpoint == r.Value.RegistrationUrl);

        if (predicate != null)
        {
            query = query.Where(r => r.Value != null && predicate(r.Value));
        }

        // Preserve existing usage that calls ToImmutableDictionary()
        return query.ToImmutableDictionary();
    }
}