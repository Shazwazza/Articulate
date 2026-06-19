#nullable enable

namespace Articulate.Controllers
{
    /// <summary>
    ///     A read-only stream wrapper that throws <see cref="InvalidOperationException" /> once
    ///     more than <c>maxBytes</c> bytes have been read from the inner stream.
    /// </summary>
    internal sealed class SizeLimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxBytes;
        private long _bytesRead;

        public SizeLimitedStream(Stream inner, long maxBytes)
        {
            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxBytes),
                    maxBytes,
                    @"The byte limit must be greater than zero.");
            }

            _inner = inner;
            _maxBytes = maxBytes;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _inner.Read(buffer, offset, GetLimitedReadCount(count));
            ThrowIfLimitExceeded(bytesRead);
            return bytesRead;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var bytesRead = await _inner.ReadAsync(buffer, offset, GetLimitedReadCount(count), cancellationToken);
            ThrowIfLimitExceeded(bytesRead);
            return bytesRead;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var bytesRead = await _inner.ReadAsync(buffer[..GetLimitedReadCount(buffer.Length)], cancellationToken);
            ThrowIfLimitExceeded(bytesRead);
            return bytesRead;
        }

        private int GetLimitedReadCount(int requestedCount)
        {
            if (requestedCount <= 0)
            {
                return requestedCount;
            }

            var remainingBytes = _maxBytes - _bytesRead;
            return remainingBytes >= requestedCount
                ? requestedCount
                : (int)Math.Min(requestedCount, remainingBytes + 1);
        }

        private void ThrowIfLimitExceeded(int bytesJustRead)
        {
            _bytesRead += bytesJustRead;
            if (_bytesRead > _maxBytes)
            {
                throw new InvalidOperationException(
                    $"Request body exceeded the configured limit of {_maxBytes} bytes");
            }
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
