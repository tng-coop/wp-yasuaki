using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorWP;

public class ProgressStream : Stream
{
    private readonly Stream _inner;
    private readonly Action<long, long> _progress;
    private long _bytesRead;

    public ProgressStream(Stream inner, Action<long, long> progress)
    {
        _inner = inner;
        _progress = progress;
    }

    private void Report(int read)
    {
        if (read <= 0)
        {
            return;
        }
        _bytesRead += read;
        _progress?.Invoke(_bytesRead, Length);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        Report(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(buffer, cancellationToken);
        Report(read);
        return read;
    }
}
