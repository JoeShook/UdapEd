using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Udap.Util.Extensions;
using UdapEd.Shared.Services;

namespace UtilityTests
{
    public class InfrastructureControllerTests
    {
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

            var infrastructure = new Infrastructure();
            var bytes = await infrastructure.JitFhirlabsCommunityCertificate(subjAltNames, "udap-test");

            var certificate = new X509Certificate2(bytes, "udap-test");

            var subjectAltNames = certificate.GetSubjectAltNames()
                .Where(s => s.Item1 == "URI")
                .Select(s => s.Item2)
                .ToList();

            Assert.Equal(subjAltNames, subjectAltNames);
            Assert.True(certificate.HasPrivateKey);
        }
    }
}
