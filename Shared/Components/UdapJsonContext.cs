using System.Text.Json;
using System.Text.Json.Serialization;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Fhir;

namespace UdapEd.Shared.Components;

/// <summary>
/// •	In AOT (WASM Release), reflection metadata is trimmed.
/// •	System.Text.Json needs parameter names for deserialization of types with parameterized constructors(like KeyValuePair).
/// </summary>
[JsonSerializable(typeof(UdapClientState))]
[JsonSerializable(typeof(IAppState))]
[JsonSerializable(typeof(UdapClientCredentialsTokenRequestModel))]
[JsonSerializable(typeof(UdapAuthorizationCodeTokenRequestModel))]
[JsonSerializable(typeof(UdapClientState))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(KeyValuePair<string, string>))]
public partial class UdapJsonContext : JsonSerializerContext
{
    public static readonly UdapJsonContext UdapDefault = new UdapJsonContext(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new FhirContextJsonConverter() }
        });
}
