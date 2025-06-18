using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NSubstitute;
using UdapEd.Client.Services;

namespace UtilityTests;
public class ScopeTests
{
    [Fact]
    public void TestSmartSelectionTests()
    {
       var registerService = new RegisterService(Substitute.For<HttpClient>(), Substitute.For<IJSRuntime>());

       var scopes = new List<string>()
       {
           "openid",
           "system/*.read",
           "user/*.read",
           "patient/*.read"
       };

       registerService.GetScopesForClientCredentials(scopes);


    }
}
