using Hl7.Fhir.Model;
using UdapEd.Shared.Model.Smart;

namespace UdapEd.Shared.Extensions;
public static class CapabilityStatementExtensions
{
    const string OAUTH_EXT = "http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris";

    public static OAuthUris GetOAuthUris(this CapabilityStatement statement)
    {
        var oAuthUris = new OAuthUris();
        var smartExtension = statement.Rest.FirstOrDefault()?.Security?.Extension?.FirstOrDefault(a => string.Equals(a.Url, OAUTH_EXT));

        smartExtension?.Extension?.ForEach(ext =>
        {
            if (ext.Url == "authorize")
            {
                oAuthUris.Authorization = ext.Value.ToString();
            }
            else if (ext.Url == "token")
            {
                oAuthUris.Token = ext.Value.ToString();
            }
        });

        return oAuthUris;
    }
}
