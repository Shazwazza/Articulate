#nullable enable
using Articulate.ImportExport;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;

namespace Articulate.Tests.ImportExport
{
    [TestFixture]
    internal class BlogMlExporterTests
    {
        [Test]
        public void GetPostDate_returns_publishedDate_when_set()
        {
            var publishedDate = new DateTime(2014, 3, 21, 0, 56, 0);
            Mock<IContent> post = new();
            post.Setup(x => x.GetValue<DateTime>("publishedDate", null, null, false)).Returns(publishedDate);
            post.SetupGet(x => x.CreateDate).Returns(new DateTime(2023, 11, 2, 15, 50, 9));

            Assert.That(BlogMlExporter.GetPostDate(post.Object), Is.EqualTo(publishedDate));
        }

        [Test]
        public void GetPostDate_falls_back_to_CreateDate_when_publishedDate_is_not_set()
        {
            var createDate = new DateTime(2023, 11, 2, 15, 50, 9);
            Mock<IContent> post = new();
            post.Setup(x => x.GetValue<DateTime>("publishedDate", null, null, false)).Returns(default(DateTime));
            post.SetupGet(x => x.CreateDate).Returns(createDate);

            Assert.That(BlogMlExporter.GetPostDate(post.Object), Is.EqualTo(createDate));
        }
    }
}
