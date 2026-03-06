using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class PushbackStream : Stream
{
    private readonly Stream _baseStream;
    private readonly byte _pushedBackByte;
    private bool _returned;
    private readonly bool _leaveOpen;

    public PushbackStream(Stream baseStream, byte pushedBackByte, bool leaveOpen = true)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _pushedBackByte = pushedBackByte;
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => _baseStream.CanWrite; // разрешаем запись, если базовый поток её поддерживает
    public override bool CanTimeout => _baseStream.CanTimeout;
    public override int ReadTimeout { get => _baseStream.ReadTimeout; set => _baseStream.ReadTimeout = value; }
    public override int WriteTimeout { get => _baseStream.WriteTimeout; set => _baseStream.WriteTimeout = value; }

    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    // Чтение
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        if (!_returned && count > 0)
        {
            buffer[offset] = _pushedBackByte;
            _returned = true;
            return 1;
        }

        return _baseStream.Read(buffer, offset, count);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!_returned && count > 0)
        {
            buffer[offset] = _pushedBackByte;
            _returned = true;
            return 1;
        }

        return await _baseStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }

#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_returned && !buffer.IsEmpty)
        {
            buffer.Span[0] = _pushedBackByte;
            _returned = true;
            return 1;
        }

        return await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif

    // Запись
    public override void Write(byte[] buffer, int offset, int count)
    {
        _baseStream.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _baseStream.WriteAsync(buffer, cancellationToken);
    }
#endif

    // Остальное
    public override void Flush() => _baseStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _baseStream.FlushAsync(cancellationToken);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }
}