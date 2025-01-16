using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdapEdAppMaui.Extensions;
public static class SessionExtensions
{
    private const int ChunkSize = 4000; // Define a suitable chunk size per each Platform 

    public static async Task StoreInChunks(string baseKey, byte[] data)
    {
        int totalChunks = (int)Math.Ceiling((double)data.Length / ChunkSize);

        for (int i = 0; i < totalChunks; i++)
        {
            int chunkSize = Math.Min(ChunkSize, data.Length - (i * ChunkSize));
            var chunk = new byte[chunkSize];
            Array.Copy(data, i * ChunkSize, chunk, 0, chunkSize);

            string chunkKey = $"{baseKey}_chunk_{i}";
            await SecureStorage.Default.SetAsync(chunkKey, Convert.ToBase64String(chunk));
        }

        // Store the total number of chunks
        await SecureStorage.Default.SetAsync($"{baseKey}_totalChunks", totalChunks.ToString());
    }
    public static async Task<string?> RetrieveFromChunks(string baseKey)
    {
        string? totalChunksStr = await SecureStorage.Default.GetAsync($"{baseKey}_totalChunks");
        if (totalChunksStr == null)
        {
            return null;
        }

        int totalChunks = int.Parse(totalChunksStr);
        var data = new List<byte>();

        for (int i = 0; i < totalChunks; i++)
        {
            string chunkKey = $"{baseKey}_chunk_{i}";
            string? chunkBase64 = await SecureStorage.Default.GetAsync(chunkKey);
            if (chunkBase64 != null)
            {
                byte[] chunk = Convert.FromBase64String(chunkBase64);
                data.AddRange(chunk);
            }
        }

        return Convert.ToBase64String(data.ToArray());
    }
}
