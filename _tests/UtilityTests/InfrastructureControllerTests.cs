#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Org.BouncyCastle.X509;
using System.Security.Cryptography.X509Certificates;
using NSubstitute;
using Udap.Util.Extensions;
using UdapEd.Shared.Services;
using Xunit.Abstractions;

namespace UtilityTests
{
    public class InfrastructureControllerTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public InfrastructureControllerTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task BuildAUdapClientCertFromWebService()
        {
            var subjAltNames = new List<string> { "udap://joe.shook/", "udap://joseph.shook/" };

            var queryString = string.Join("&", subjAltNames.Select(name => $"subjAltNames={Uri.EscapeDataString(name)}"));
            queryString += $"&password=udap-test";
            var response = await new HttpClient()
                .GetAsync($"https://udaped.fhirlabs.net/Infrastructure/JitFhirlabsCommunityCertificate?{queryString}");

            Assert.True(response.IsSuccessStatusCode);
            
            var contentBase64 = await response.Content.ReadAsStringAsync();
            var bytes = Convert.FromBase64String(contentBase64);
            var certificate = new X509Certificate2(bytes, "udap-test");

            var subjectAltNames = certificate.GetSubjectAltNames()
                .Where(s => s.Item1 == "URI")
                .Select(s => s.Item2)
                .ToList();

            Assert.Equal(subjAltNames, subjectAltNames);
            Assert.True(certificate.HasPrivateKey);
        }

        [Fact]
        public async Task BuildAUdapClientCert()
        {
            var subjAltNames = new List<string> { "udap://joe.shook/", "udap://joseph.shook/" };

            var infrastructure = new Infrastructure(Substitute.For<HttpClient>());
            var bytes = await infrastructure.JitFhirlabsCommunityCertificate(subjAltNames, "udap-test");

            var certificate = new X509Certificate2(bytes, "udap-test");

            var subjectAltNames = certificate.GetSubjectAltNames()
                .Where(s => s.Item1 == "URI")
                .Select(s => s.Item2)
                .ToList();

            Assert.Equal(subjAltNames, subjectAltNames);
            Assert.True(certificate.HasPrivateKey);
        }

        [Fact]
        public async Task GetCrl()
        {
            var bytes = await new HttpClient()
                .GetByteArrayAsync($"http://host.docker.internal:5033/crl/caLocalhostCert.crl");

            var crl = new X509Crl(bytes);
            _testOutputHelper.WriteLine(crl.ToString());
        }
    }
}
