//
// From GitHub discussion:
// https://github.com/dotnet/runtime/issues/39835#issuecomment-663106476
//

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;


namespace UdapEd.Shared.Services.Authentication;


public static class HttpClientHandlerExtension
{
    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>?
        CreateCustomRootValidator(
            X509Certificate2Collection? trustedRoots,
            ILogger logger,
            X509Certificate2Collection? intermediates = null)
    {
        RemoteCertificateValidationCallback callback = CreateCustomRootRemoteValidator(trustedRoots, logger, intermediates);
        return (message, serverCert, chain, errors) => callback(message, serverCert, chain, errors);
    }

    public static RemoteCertificateValidationCallback CreateCustomRootRemoteValidator(
        X509Certificate2Collection? trustedRoots,
        ILogger logger,
        X509Certificate2Collection? intermediates = null)
    {
        if (trustedRoots == null)
            throw new ArgumentNullException(nameof(trustedRoots));
        if (trustedRoots.Count == 0)
            throw new ArgumentException("No trusted roots were provided", nameof(trustedRoots));

        // Let's avoid complex state and/or race conditions by making copies of these collections.
        // Then the delegates should be safe for parallel invocation (provided they are given distinct inputs, which they are).
        X509Certificate2Collection roots = new X509Certificate2Collection(trustedRoots);
        X509Certificate2Collection? intermeds = null;

        if (intermediates != null)
        {
            intermeds = new X509Certificate2Collection(intermediates);
        }

        trustedRoots = null;
        intermediates = null;

        return (_, serverCert, chain, errors) =>
        {
            // Missing cert or the destination hostname wasn't valid for the cert.
            if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                return false;
            }

            using var chainBuilder = new X509Chain();

            if (intermeds != null)
            {
                chainBuilder.ChainPolicy.ExtraStore.AddRange(intermeds);
            }

            chainBuilder.ChainPolicy.CustomTrustStore.Clear();
            chainBuilder.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chainBuilder.ChainPolicy.CustomTrustStore.AddRange(roots);
            var certificate = serverCert as X509Certificate2;

            //
            // Troubleshooting:  On Windows the chain passed in is still respected.  
            // So the CA has to be installed in the Windows Trust Store.  
            // Seems to be a missing feature on Windows, as it works on Linux
            //
            // foreach (var chainChainElement in chain.ChainElements)
            // {
            //     foreach (var status in chainChainElement.ChainElementStatus)
            //     {
            //         logger.LogInformation($"Chain element status: {status.Status}");
            //     }
            // }
         

            return chainBuilder.Build(certificate!); // I don't think this is never null at this point.
        };
    }
}
