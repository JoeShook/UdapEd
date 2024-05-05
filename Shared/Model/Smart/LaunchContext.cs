using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdapEd.Shared.Model.Smart;

/// <summary>
/// see https://hl7.org/fhir/smart-app-launch/scopes-and-launch-context.html
/// </summary>
public class LaunchContext
{
    public string Patient { get; set; }
}
