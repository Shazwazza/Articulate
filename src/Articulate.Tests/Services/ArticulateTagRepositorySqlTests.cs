#nullable enable
using Articulate.Services;
using Moq;
using NPoco;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Articulate.Tests.Services
{
    /// <summary>
    ///     Asserts the shape of the hand-rolled tag/category SQL built by
    ///     <see cref="ArticulateTagRepository" />. The builders are pure (no ambient scope); tests pass
    ///     a stub <see cref="ISqlSyntaxProvider" /> and inspect the emitted <see cref="Sql" /> text and
    ///     arguments. Catches the regression-prone parts: multi-blog <c>path LIKE</c> scoping,
    ///     published-only filters, publishedDate property filter, and parameterisation (no injection).
    /// </summary>
    /// <remarks>
    ///     These cover SQL shape only — the <c>Database.Fetch</c>/<c>Page</c> round-trip needs a real
    ///     database and is left to an integration test. NPoco rewrites named parameters to positional
    ///     (<c>@path</c> → <c>@0</c>, <c>@1</c> ...) inside <see cref="Sql.SQL" />, so assertions match
    ///     on the clause text and read the bound values from <see cref="Sql.Arguments" /> in order.
    /// </remarks>
    [TestFixture]
    public class ArticulateTagRepositorySqlTests
    {
        private const string RootPath = "-1,1234";
        private const string QuotedPath = "[path]";

        [Test]
        public void BuildTagQuery_scopes_to_blog_root_path()
        {
            Sql sql = ArticulateTagRepository.BuildTagQuery("id", RootPath, CreateSqlSyntax());

            Assert.Multiple(() =>
            {
                Assert.That(sql.SQL, Does.Contain($"{QuotedPath} LIKE @"));
                Assert.That(ArgumentsOf(sql), Does.Contain(RootPath + ",%"));
            });
        }

        [Test]
        public void BuildTagQuery_filters_to_document_node_object_type()
        {
            Sql sql = ArticulateTagRepository.BuildTagQuery("id", RootPath, CreateSqlSyntax());

            Assert.Multiple(() =>
            {
                Assert.That(sql.SQL, Does.Contain("nodeObjectType = @"));
                Assert.That(ArgumentsOf(sql), Does.Contain(Constants.ObjectTypes.Document));
            });
        }

        [Test]
        public void BuildTagQuery_joins_tag_relationship_and_content_tables()
        {
            Sql sql = ArticulateTagRepository.BuildTagQuery("id", RootPath, CreateSqlSyntax());

            Assert.Multiple(() =>
            {
                Assert.That(sql.SQL, Does.Contain(Constants.DatabaseSchema.Tables.Tag));
                Assert.That(sql.SQL, Does.Contain(Constants.DatabaseSchema.Tables.TagRelationship));
                Assert.That(sql.SQL, Does.Contain(Constants.DatabaseSchema.Tables.Node));
            });
        }

        [Test]
        public void BuildContentByTagQueryForPaging_requires_published_document_and_version()
        {
            Sql sql = ArticulateTagRepository.BuildContentByTagQueryForPaging(
                "id", RootPath, publishedDatePropertyTypeId: 42, CreateSqlSyntax());

            Assert.Multiple(() =>
            {
                Assert.That(sql.SQL, Does.Contain($"{Constants.DatabaseSchema.Tables.Document}.published = 1"));
                Assert.That(sql.SQL, Does.Contain($"{Constants.DatabaseSchema.Tables.DocumentVersion}.published = 1"));
            });
        }

        [Test]
        public void BuildContentByTagQueryForPaging_filters_to_publishedDate_property_type()
        {
            Sql sql = ArticulateTagRepository.BuildContentByTagQueryForPaging(
                "id", RootPath, publishedDatePropertyTypeId: 42, CreateSqlSyntax());

            Assert.Multiple(() =>
            {
                Assert.That(sql.SQL, Does.Contain("propertytypeid = @"));
                Assert.That(ArgumentsOf(sql), Does.Contain(42));
            });
        }

        [Test]
        public void BuildContentByTagQueryForPaging_scopes_to_blog_root_path()
        {
            Sql sql = ArticulateTagRepository.BuildContentByTagQueryForPaging(
                "id", RootPath, publishedDatePropertyTypeId: 42, CreateSqlSyntax());

            Assert.Multiple(() =>
            {
                Assert.That(sql.SQL, Does.Contain($"{QuotedPath} LIKE @"));
                Assert.That(ArgumentsOf(sql), Does.Contain(RootPath + ",%"));
            });
        }

        // NPoco's .Arguments is the bound parameter values; unwrap whatever collection shape it has.
        private static IEnumerable<object?> ArgumentsOf(Sql sql) =>
            sql.Arguments as IEnumerable<object?> ?? Array.Empty<object?>();

        // Minimal stub: the builders only call GetQuotedColumnName. SQL Server-style [col] quoting
        // keeps assertions readable while still exercising the indirection.
        private static ISqlSyntaxProvider CreateSqlSyntax()
        {
            Mock<ISqlSyntaxProvider> syntax = new();
            syntax.Setup(x => x.GetQuotedColumnName(It.IsAny<string>()))
                .Returns<string>(c => $"[{c}]");
            return syntax.Object;
        }
    }
}
