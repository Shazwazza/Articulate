#nullable enable
using Articulate.Controllers;
using NUnit.Framework;

namespace Articulate.Tests.Controllers
{
    [TestFixture]
    public class SizeLimitedStreamTests
    {
        [Test]
        public async Task ReadAsync_returns_content_when_stream_matches_limit()
        {
            byte[] bytes = [1, 2, 3];
            await using var inner = new MemoryStream(bytes);
            await using var stream = new SizeLimitedStream(inner, bytes.Length);
            var buffer = new byte[bytes.Length];

            await stream.ReadExactlyAsync(buffer);

            Assert.That(buffer, Is.EqualTo(bytes));
        }

        [Test]
        public async Task ReadAsync_throws_when_stream_exceeds_limit()
        {
            await using var inner = new MemoryStream([1, 2, 3]);
            await using var stream = new SizeLimitedStream(inner, 2);
            var buffer = new byte[3];

            InvalidOperationException? exception =
                Assert.ThrowsAsync<InvalidOperationException>(async () => await stream.ReadExactlyAsync(buffer));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Is.EqualTo("Request body exceeded the configured limit of 2 bytes"));
        }

        [Test]
        public void Constructor_rejects_invalid_limit()
        {
            using var inner = new MemoryStream();

            ArgumentOutOfRangeException? exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                using var stream = new SizeLimitedStream(inner, 0);
            });

            Assert.That(exception, Is.Not.Null);
        }
    }
}
