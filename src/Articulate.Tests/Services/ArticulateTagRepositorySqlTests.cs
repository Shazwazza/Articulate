#nullable enable
using Articulate.Services;
using Microsoft.Data.Sqlite;
using Moq;
using NPoco;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Articulate.Tests.Services
{
    /// <summary>
    ///     Executes the hand-rolled tag/category SQL builders against a focused in-memory database.
    ///     This covers the observable filtering and join behaviour without requiring an Umbraco host.
    /// </summary>
    [TestFixture]
    public class ArticulateTagRepositorySqlTests
    {
        private const string RootPath = "-1,1234";

        [Test]
        public void BuildTagQuery_returns_only_document_under_requested_root()
        {
            string nodeTable = Constants.DatabaseSchema.Tables.Node;
            string tagTable = Constants.DatabaseSchema.Tables.Tag;
            string relationshipTable = Constants.DatabaseSchema.Tables.TagRelationship;
            string contentTable = Constants.DatabaseSchema.Tables.Content;

            using SqliteConnection database = CreateDatabase($"""
                CREATE TABLE [{tagTable}] (id INTEGER PRIMARY KEY);
                CREATE TABLE [{relationshipTable}] (tagId INTEGER, nodeId INTEGER);
                CREATE TABLE [{contentTable}] (nodeId INTEGER);
                CREATE TABLE [{nodeTable}] (id INTEGER PRIMARY KEY, nodeObjectType TEXT, [path] TEXT);
                INSERT INTO [{tagTable}] (id) VALUES (1);
                INSERT INTO [{relationshipTable}] (tagId, nodeId) VALUES (1, 100), (1, 101), (1, 102);
                INSERT INTO [{contentTable}] (nodeId) VALUES (100), (101), (102), (103);
                INSERT INTO [{nodeTable}] (id, nodeObjectType, [path]) VALUES
                    (100, '{Constants.ObjectTypes.Document}', '-1,1234,100'),
                    (101, '{Constants.ObjectTypes.Document}', '-1,9999,101'),
                    (102, 'media', '-1,1234,102'),
                    (103, '{Constants.ObjectTypes.Document}', '-1,1234,103');
                """);

            Sql sql = ArticulateTagRepository.BuildTagQuery(
                $"{nodeTable}.id", RootPath, CreateSqlSyntax());

            Assert.That(ExecuteIds(database, sql), Is.EqualTo(new[] { 100L }));
        }

        [Test]
        public void BuildContentByTagQueryForPaging_returns_only_published_matching_date_property()
        {
            string nodeTable = Constants.DatabaseSchema.Tables.Node;
            string documentTable = Constants.DatabaseSchema.Tables.Document;
            string contentVersionTable = Constants.DatabaseSchema.Tables.ContentVersion;
            string documentVersionTable = Constants.DatabaseSchema.Tables.DocumentVersion;
            string propertyDataTable = Constants.DatabaseSchema.Tables.PropertyData;

            using SqliteConnection database = CreateDatabase($"""
                CREATE TABLE [{nodeTable}] (id INTEGER PRIMARY KEY, nodeObjectType TEXT, [path] TEXT);
                CREATE TABLE [{documentTable}] (nodeId INTEGER, published INTEGER);
                CREATE TABLE [{contentVersionTable}] (nodeId INTEGER, id INTEGER PRIMARY KEY);
                CREATE TABLE [{documentVersionTable}] (id INTEGER PRIMARY KEY, published INTEGER);
                CREATE TABLE [{propertyDataTable}] (versionId INTEGER, propertytypeid INTEGER, dateValue TEXT);
                INSERT INTO [{nodeTable}] (id, nodeObjectType, [path]) VALUES
                    (100, '{Constants.ObjectTypes.Document}', '-1,1234,100'),
                    (101, '{Constants.ObjectTypes.Document}', '-1,1234,101'),
                    (102, '{Constants.ObjectTypes.Document}', '-1,1234,102'),
                    (103, '{Constants.ObjectTypes.Document}', '-1,1234,103'),
                    (104, '{Constants.ObjectTypes.Document}', '-1,9999,104'),
                    (105, 'media', '-1,1234,105'),
                    (106, '{Constants.ObjectTypes.Document}', '-1,1234,106');
                INSERT INTO [{documentTable}] (nodeId, published) VALUES
                    (100, 1), (101, 0), (102, 1), (103, 1), (104, 1), (105, 1), (106, 1);
                INSERT INTO [{contentVersionTable}] (nodeId, id) VALUES
                    (100, 1000), (101, 1001), (102, 1002), (103, 1003),
                    (104, 1004), (105, 1005), (106, 1006);
                INSERT INTO [{documentVersionTable}] (id, published) VALUES
                    (1000, 1), (1001, 1), (1002, 0), (1003, 1),
                    (1004, 1), (1005, 1), (1006, 1);
                INSERT INTO [{propertyDataTable}] (versionId, propertytypeid, dateValue) VALUES
                    (1000, 42, '2026-01-01'), (1001, 42, '2026-01-02'),
                    (1002, 42, '2026-01-03'), (1003, 99, '2026-01-04'),
                    (1004, 42, '2026-01-05'), (1005, 42, '2026-01-06');
                """);

            Sql sql = ArticulateTagRepository.BuildContentByTagQueryForPaging(
                $"{nodeTable}.id", RootPath, publishedDatePropertyTypeId: 42, CreateSqlSyntax());

            Assert.That(ExecuteIds(database, sql), Is.EqualTo(new[] { 100L }));
        }

        private static SqliteConnection CreateDatabase(string schema)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            Execute(connection, schema);
            return connection;
        }

        private static long[] ExecuteIds(SqliteConnection database, Sql sql)
        {
            using SqliteCommand command = database.CreateCommand();
            command.CommandText = sql.SQL;

            object?[] arguments = sql.Arguments.Cast<object?>().ToArray();
            for (var index = 0; index < arguments.Length; index++)
            {
                object? argument = arguments[index] is Guid guid ? guid.ToString() : arguments[index];
                command.Parameters.AddWithValue($"@{index}", argument ?? DBNull.Value);
            }

            using SqliteDataReader reader = command.ExecuteReader();
            var ids = new List<long>();
            while (reader.Read())
            {
                ids.Add(reader.GetInt64(0));
            }

            return ids.ToArray();
        }

        private static void Execute(SqliteConnection database, string sql)
        {
            using SqliteCommand command = database.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static ISqlSyntaxProvider CreateSqlSyntax()
        {
            Mock<ISqlSyntaxProvider> syntax = new();
            syntax.Setup(x => x.GetQuotedColumnName(It.IsAny<string>()))
                .Returns<string>(c => $"[{c}]");
            return syntax.Object;
        }
    }
}
