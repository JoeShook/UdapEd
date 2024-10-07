#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion


using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace UdapEd.Shared.Services
{
    /// <summary>
    /// Built to watch the Windows AppData\LocalLow\Microsoft\CryptnetUrlCache\MetaData folder for changes.
    /// This is where CRL get cached that we may be interested in.
    /// </summary>
    public class CrlCacheService
    {
        private readonly ILogger<CrlCacheService> _logger;
        private readonly string _folderPath;
        private readonly Dictionary<string, string> _urlCache = new Dictionary<string, string>();


        public CrlCacheService(ILogger<CrlCacheService> logger)
        {
            _logger = logger;
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Microsoft", "CryptnetUrlCache", "MetaData");
        }

        public void ProcessExistingFiles()
        {
            var files = Directory.GetFiles(_folderPath);

            
            foreach (var file in files)
            {
                if (_urlCache.ContainsValue(file))
                {
                    continue;
                }
                    
                var parser = CryptnetURLCacheParser.ParseFile(file, false);

                _logger.LogDebug($"Processing existing file: {file} for url: {parser.Url}");
                _urlCache[parser.Url] = file;
            }
        }

        public CrlFileCacheSettings Get(string location)
        {
            ProcessExistingFiles();
            var cacheSettings = new CrlFileCacheSettings();
            if (_urlCache.TryGetValue(location, out var value))
            {
                cacheSettings.MetadataFile = value;
                string parent = Directory.GetParent(cacheSettings.MetadataFile).Parent.FullName;
                string contentPath = Path.Combine(parent, "Content", Path.GetFileName(cacheSettings.MetadataFile));
                cacheSettings.ContentFile = contentPath;
                cacheSettings.UrlPath = location;
                cacheSettings.Cached = true;
            }

            return cacheSettings;
        }

        public void Remove(CrlFileCacheSettings settings)
        {
            //
            // Deleting the files does not seem to be enough.  I think the cache is still in memory.
            // Because repeated x509Chain builds does not repopulate the files as expected.
            // But after a restart of the Cryptographic Services the files are repopulated after the x509chain build. 
            //
            if (File.Exists(settings.ContentFile))
            {
                File.Delete(settings.ContentFile);
            }
            
            if (File.Exists(settings.MetadataFile))
            {
                File.Delete(settings.MetadataFile);
            }



            //
            // Switching to shell out to certUtil
            //

            //
            // Crowdstrike doesn't like this sometimes.
            //
            // if (OperatingSystem.IsWindows())
            // {
            //     _logger.LogDebug($"calling  certutil -urlcache {settings.UrlPath} delete");
            //     ProcessStartInfo startInfo = new ProcessStartInfo
            //     {
            //         FileName = "certutil",
            //         Arguments = $"-urlcache {settings.UrlPath} delete",
            //         RedirectStandardOutput = true,
            //         UseShellExecute = false,
            //         CreateNoWindow = true
            //     };
            //
            //     using (var process = Process.Start(startInfo))
            //     {
            //         process?.WaitForExit();
            //         var output = process?.StandardOutput.ReadToEnd();
            //         _logger.LogDebug(output);
            //     }
            // }

            //ClearUrlCache();
                // ResetCrlCache();
                if (!string.IsNullOrEmpty(settings.UrlPath))
                {
                    _urlCache.Remove(settings.UrlPath);
                }
               
            
        }


        #region Intersting experiments that didn't work out.  
        //
        // I was trying to clear the CRL that gets cached in memory.  Even
        // While using the certutil technique  certutil -urlcache "http://crl.net/mycrl" delete
        // does delete from the file cache, an application that is running is subject 
        // to an in memory cache that I cannot control unless I recycle the host process
        // Even restarting the Cryptographic Services does not clear the in memory cache.
        //
        // I could host that parts that build the x509chain our of process so I could shell to it.
        // But the spirit of this tooling is to give insight on what is happening.  So I think
        // I will just try to tell the story better in the UI so the user know they will have to 
        // restart their process even if they clear the CRL file cache.
        //

        //
        //
        // // P/Invoke signature for CertOpenStore
        // [DllImport("crypt32.dll", SetLastError = true)]
        // private static extern IntPtr CertOpenStore(
        //     IntPtr lpszStoreProvider,
        //     uint dwMsgAndCertEncodingType,
        //     IntPtr hCryptProv,
        //     uint dwFlags,
        //     string pvPara);
        //
        // // P/Invoke signature for CertControlStore
        // [DllImport("crypt32.dll", SetLastError = true)]
        // private static extern bool CertControlStore(
        //     IntPtr hCertStore,
        //     uint dwFlags,
        //     uint dwCtrlType,
        //     IntPtr pvCtrlPara);
        //
        // // P/Invoke signature for CertCloseStore
        // [DllImport("crypt32.dll", SetLastError = true)]
        // private static extern bool CertCloseStore(
        //     IntPtr hCertStore,
        //     uint dwFlags);
        //
        // // Constants for CertOpenStore
        // private const uint CERT_STORE_PROV_SYSTEM = 10;
        // private const uint CERT_SYSTEM_STORE_CURRENT_USER = 0x00010000;
        // private const uint CERT_SYSTEM_STORE_LOCAL_MACHINE = 0x00020000;
        // private const uint CERT_STORE_OPEN_EXISTING_FLAG = 0x00004000;
        // private const uint CERT_STORE_READONLY_FLAG = 0x00008000;
        //
        // // Constants for CertControlStore
        // private const uint CERT_STORE_CTRL_RESYNC = 1;
        //
        // /// <summary>
        // /// Directly reseting the in-memory cache of CRLs held by the underlying Windows CryptoAPI.
        // /// Use the CertControlStore function with the CERT_STORE_CTRL_AUTO_RESYNC control type.
        // /// This approach will resynchronize the certificate store, which can help in clearing the in-memory cache of CRLs.
        // /// </summary>
        // /// <exception cref="InvalidOperationException"></exception>
        // public void ResetCrlCache()
        // {
        //     try
        //     {
        //         string[] storeNames = { "Root", "My", "CA" };
        //         bool storeOpened = false;
        //
        //         foreach (var storeName in storeNames)
        //         {
        //             try
        //             {
        //                 _logger.LogDebug($"Attempting to open certificate store '{storeName}' in current user context.");
        //                 using (X509Store store = new X509Store(storeName, StoreLocation.CurrentUser))
        //                 {
        //                     store.Open(OpenFlags.ReadOnly);
        //                     _logger.LogDebug($"Successfully opened certificate store '{storeName}' in current user context.");
        //                     storeOpened = true;
        //
        //                     // Get the handle to the store
        //                     IntPtr hCertStore = store.StoreHandle;
        //
        //                     // Attempt to resynchronize the store
        //                     _logger.LogDebug($"Attempting to resynchronize certificate store '{storeName}'.");
        //                     if (!CertControlStore(hCertStore, 0, CERT_STORE_CTRL_RESYNC, IntPtr.Zero))
        //                     {
        //                         int errorCode = Marshal.GetLastWin32Error();
        //                         throw new InvalidOperationException($"Failed to resynchronize certificate store '{storeName}'. Error code: {errorCode}");
        //                     }
        //
        //                     _logger.LogDebug($"Successfully resynchronized certificate store '{storeName}'.");
        //                 }
        //             }
        //             catch (Exception ex)
        //             {
        //                 _logger.LogDebug($"Failed to open or resynchronize certificate store '{storeName}' in current user context. Exception: {ex.Message}");
        //             }
        //
        //             if (!storeOpened)
        //             {
        //                 try
        //                 {
        //                     _logger.LogDebug($"Attempting to open certificate store '{storeName}' in local machine context.");
        //                     using (X509Store store = new X509Store(storeName, StoreLocation.LocalMachine))
        //                     {
        //                         store.Open(OpenFlags.ReadOnly);
        //                         _logger.LogDebug($"Successfully opened certificate store '{storeName}' in local machine context.");
        //                         storeOpened = true;
        //
        //                         // Get the handle to the store
        //                         IntPtr hCertStore = store.StoreHandle;
        //
        //                         // Attempt to resynchronize the store
        //                         _logger.LogDebug($"Attempting to resynchronize certificate store '{storeName}'.");
        //                         if (!CertControlStore(hCertStore, 0, CERT_STORE_CTRL_RESYNC, IntPtr.Zero))
        //                         {
        //                             int errorCode = Marshal.GetLastWin32Error();
        //                             throw new InvalidOperationException($"Failed to resynchronize certificate store '{storeName}'. Error code: {errorCode}");
        //                         }
        //
        //                         _logger.LogDebug($"Successfully resynchronized certificate store '{storeName}'.");
        //                     }
        //                 }
        //                 catch (Exception ex)
        //                 {
        //                     _logger.LogDebug($"Failed to open or resynchronize certificate store '{storeName}' in local machine context. Exception: {ex.Message}");
        //                 }
        //             }
        //
        //             if (storeOpened)
        //             {
        //                 // break;
        //             }
        //         }
        //
        //         if (!storeOpened)
        //         {
        //             throw new InvalidOperationException("Failed to open any certificate store.");
        //         }
        //     }
        //     catch (InvalidOperationException ex)
        //     {
        //         _logger.LogDebug(ex.Message);
        //         throw;
        //     }
        // }
        //
        //
        // // P/Invoke signature for CertFindCRLInStore
        // [DllImport("crypt32.dll", SetLastError = true)]
        // private static extern IntPtr CertFindCRLInStore(
        //     IntPtr hCertStore,
        //     uint dwCertEncodingType,
        //     uint dwFindFlags,
        //     uint dwFindType,
        //     IntPtr pvFindPara,
        //     IntPtr pPrevCrlContext);
        //
        // // P/Invoke signature for CertFreeCRLContext
        // [DllImport("crypt32.dll", SetLastError = true)]
        // private static extern bool CertFreeCRLContext(IntPtr pCrlContext);
        //
        // // Constants for CertFindCRLInStore
        // private const uint X509_ASN_ENCODING = 0x00000001;
        // private const uint PKCS_7_ASN_ENCODING = 0x00010000;
        // private const uint CERT_FIND_ANY = 0x00000000;
        //
        //
        // public void ResetCrlCache2()
        // {
        //     try
        //     {
        //         string[] storeNames = { "My"};
        //         bool storeOpened = false;
        //
        //         foreach (var storeName in storeNames)
        //         {
        //             try
        //             {
        //                 _logger.LogDebug($"Attempting to open certificate store '{storeName}' in current user context.");
        //                 using (X509Store store = new X509Store(storeName, StoreLocation.CurrentUser))
        //                 {
        //                     store.Open(OpenFlags.ReadOnly);
        //                     _logger.LogDebug($"Successfully opened certificate store '{storeName}' in current user context.");
        //                     storeOpened = true;
        //
        //                     // Get the handle to the store
        //                     IntPtr hCertStore = store.StoreHandle;
        //
        //                     // Free CRL contexts
        //                     IntPtr pCrlContext = IntPtr.Zero;
        //                     while ((pCrlContext = CertFindCRLInStore(hCertStore, X509_ASN_ENCODING | PKCS_7_ASN_ENCODING, 0, CERT_FIND_ANY, IntPtr.Zero, pCrlContext)) != IntPtr.Zero)
        //                     {
        //                         _logger.LogDebug($"Freeing CRL context.");
        //                         if (!CertFreeCRLContext(pCrlContext))
        //                         {
        //                             int errorCode = Marshal.GetLastWin32Error();
        //                             throw new InvalidOperationException($"Failed to free CRL context. Error code: {errorCode}");
        //                         }
        //                     }
        //
        //                     // Attempt to resynchronize the store
        //                     _logger.LogDebug($"Attempting to resynchronize certificate store '{storeName}'.");
        //                     if (!CertControlStore(hCertStore, 0, CERT_STORE_CTRL_RESYNC, IntPtr.Zero))
        //                     {
        //                         int errorCode = Marshal.GetLastWin32Error();
        //                         throw new InvalidOperationException($"Failed to resynchronize certificate store '{storeName}'. Error code: {errorCode}");
        //                     }
        //
        //                     _logger.LogDebug($"Successfully resynchronized certificate store '{storeName}'.");
        //                 }
        //             }
        //             catch (Exception ex)
        //             {
        //                 _logger.LogDebug($"Failed to open or resynchronize certificate store '{storeName}' in current user context. Exception: {ex.Message}");
        //             }
        //
        //             if (!storeOpened)
        //             {
        //                 try
        //                 {
        //                     _logger.LogDebug($"Attempting to open certificate store '{storeName}' in local machine context.");
        //                     using (X509Store store = new X509Store(storeName, StoreLocation.LocalMachine))
        //                     {
        //                         store.Open(OpenFlags.ReadOnly);
        //                         _logger.LogDebug($"Successfully opened certificate store '{storeName}' in local machine context.");
        //                         storeOpened = true;
        //
        //                         // Get the handle to the store
        //                         IntPtr hCertStore = store.StoreHandle;
        //
        //                         // Free CRL contexts
        //                         IntPtr pCrlContext = IntPtr.Zero;
        //                         while ((pCrlContext = CertFindCRLInStore(hCertStore, X509_ASN_ENCODING | PKCS_7_ASN_ENCODING, 0, CERT_FIND_ANY, IntPtr.Zero, pCrlContext)) != IntPtr.Zero)
        //                         {
        //                             _logger.LogDebug($"Freeing CRL context.");
        //                             if (!CertFreeCRLContext(pCrlContext))
        //                             {
        //                                 int errorCode = Marshal.GetLastWin32Error();
        //                                 throw new InvalidOperationException($"Failed to free CRL context. Error code: {errorCode}");
        //                             }
        //                         }
        //
        //                         // Attempt to resynchronize the store
        //                         _logger.LogDebug($"Attempting to resynchronize certificate store '{storeName}'.");
        //                         if (!CertControlStore(hCertStore, 0, CERT_STORE_CTRL_RESYNC, IntPtr.Zero))
        //                         {
        //                             int errorCode = Marshal.GetLastWin32Error();
        //                             throw new InvalidOperationException($"Failed to resynchronize certificate store '{storeName}'. Error code: {errorCode}");
        //                         }
        //
        //                         _logger.LogDebug($"Successfully resynchronized certificate store '{storeName}'.");
        //                     }
        //                 }
        //                 catch (Exception ex)
        //                 {
        //                     _logger.LogDebug($"Failed to open or resynchronize certificate store '{storeName}' in local machine context. Exception: {ex.Message}");
        //                 }
        //             }
        //
        //             if (storeOpened)
        //             {
        //                 // break;
        //             }
        //         }
        //
        //         if (!storeOpened)
        //         {
        //             throw new InvalidOperationException("Failed to open any certificate store.");
        //         }
        //     }
        //     catch (InvalidOperationException ex)
        //     {
        //         _logger.LogDebug(ex.Message);
        //         throw;
        //     }
        // }
        //
        // // P/Invoke signature for CryptnetUrlCacheFlush
        // [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        // private static extern bool CryptnetUrlCacheFlush();
        //
        // // P/Invoke signature for CryptnetUrlCacheFree
        // [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        // private static extern bool CryptnetUrlCacheFree(IntPtr pUrlCacheEntry);
        //
        // // P/Invoke signature for CryptnetUrlCacheFindFirst
        // [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        // private static extern IntPtr CryptnetUrlCacheFindFirst(
        //     [MarshalAs(UnmanagedType.LPWStr)] string pwszUrl,
        //     uint dwFlags,
        //     IntPtr pReserved,
        //     ref CRYPTNET_URL_CACHE_ENTRY pUrlCacheEntry);
        //
        // // P/Invoke signature for CryptnetUrlCacheFindNext
        // [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        // private static extern bool CryptnetUrlCacheFindNext(
        //     IntPtr hFind,
        //     ref CRYPTNET_URL_CACHE_ENTRY pUrlCacheEntry);
        //
        // // P/Invoke signature for CryptnetUrlCacheClose
        // [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        // private static extern bool CryptnetUrlCacheClose(IntPtr hFind);
        //
        // // Structure for CRYPTNET_URL_CACHE_ENTRY
        // [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        // private struct CRYPTNET_URL_CACHE_ENTRY
        // {
        //     public uint cbSize;
        //     public uint dwMagic;
        //     public uint dwFlags;
        //     public long ftCreate;
        //     public long ftLastAccess;
        //     public long ftLastSync;
        //     public uint cbBlob;
        //     public IntPtr pbBlob;
        //     [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        //     public string wszUrl;
        // }
        //
        // public void ClearCrlCache()
        // {
        //     // Clear the URL cache
        //     ClearUrlCache();
        // }
        //
        // private void ClearUrlCache()
        // {
        //     // Flush the URL cache
        //     if (!CryptnetUrlCacheFlush())
        //     {
        //         int errorCode = Marshal.GetLastWin32Error();
        //         throw new InvalidOperationException($"Failed to flush URL cache. Error code: {errorCode}");
        //     }
        //
        //     // Find and free URL cache entries
        //     CRYPTNET_URL_CACHE_ENTRY urlCacheEntry = new CRYPTNET_URL_CACHE_ENTRY
        //     {
        //         cbSize = (uint)Marshal.SizeOf(typeof(CRYPTNET_URL_CACHE_ENTRY))
        //     };
        //
        //     IntPtr hFind = CryptnetUrlCacheFindFirst(null, 0, IntPtr.Zero, ref urlCacheEntry);
        //     if (hFind == IntPtr.Zero)
        //     {
        //         int errorCode = Marshal.GetLastWin32Error();
        //         throw new InvalidOperationException($"Failed to find first URL cache entry. Error code: {errorCode}");
        //     }
        //
        //     do
        //     {
        //         if (!CryptnetUrlCacheFree(urlCacheEntry.pbBlob))
        //         {
        //             int errorCode = Marshal.GetLastWin32Error();
        //             throw new InvalidOperationException($"Failed to free URL cache entry. Error code: {errorCode}");
        //         }
        //     } while (CryptnetUrlCacheFindNext(hFind, ref urlCacheEntry));
        //
        //     if (!CryptnetUrlCacheClose(hFind))
        //     {
        //         int errorCode = Marshal.GetLastWin32Error();
        //         throw new InvalidOperationException($"Failed to close URL cache find handle. Error code: {errorCode}");
        //     }
        // }
    #endregion

    }
}
