#region (c) 2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.IO.Compression;
using System.Text;
using UdapEd.Shared;
using UdapEd.Shared.Services.Fhir;

namespace UdapEd.Server.Services.Fhir;

public class CustomDecompressionHandler : DelegatingHandler
{
    private readonly IFhirClientOptionsProvider _provider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomDecompressionHandler(
        IFhirClientOptionsProvider provider,
        IHttpContextAccessor httpContextAccessor)
    {
        _provider = provider;
        _httpContextAccessor = httpContextAccessor;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (await _provider.GetDecompression())
        {
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
        }
        else
        {
            request.Headers.AcceptEncoding.Clear();
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("identity"));
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response?.Content != null)
        {
            var encoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            var compressedStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var countingStream = new CountingStream(compressedStream);

            Stream decompressedStream = encoding switch
            {
                "gzip" => new GZipStream(countingStream, CompressionMode.Decompress),
                "deflate" => new DeflateStream(countingStream, CompressionMode.Decompress),
                _ => countingStream
            };
        
            // Read decompressed content into a string
            using var reader = new StreamReader(decompressedStream);
            var decompressedContent = await reader.ReadToEndAsync(cancellationToken);

            _httpContextAccessor.HttpContext!.Items[UdapEdConstants.FhirClient.FhirCompressedSize] = countingStream.BytesRead.ToString();
            _httpContextAccessor.HttpContext.Items[UdapEdConstants.FhirClient.FhirDecompressedSize] = Encoding.UTF8.GetByteCount(decompressedContent).ToString();

            // Replace the response content with the decompressed content
            response.Content = new StringContent(decompressedContent);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/fhir+json");
            response.Content.Headers.ContentEncoding.Clear();
        }
        
        return response;
    }
}

public class CountingStream : Stream
{
    private readonly Stream _inner;
    public long BytesRead { get; private set; }

    public CountingStream(Stream inner)
    {
        _inner = inner;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(buffer, cancellationToken);
        BytesRead += read;
        return read;
    }

    // The rest just delegate to _inner
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);
    public override void Close() => _inner.Close();
    protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
}