using System.Text.Json.Serialization;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Shared.Components;


[JsonSerializable(typeof(UdapClientState))]
[JsonSerializable(typeof(IAppState))]
public partial class UdapJsonContext : JsonSerializerContext { }
