using System.Collections.Generic;
using System.Text.Json;
using Azure.DataApiBuilder.Auth;
using Azure.DataApiBuilder.Config;
using Azure.DataApiBuilder.Service.Configurations;
using Azure.DataApiBuilder.Service.Services;
using Azure.DataApiBuilder.Service.Tests.GraphQLBuilder.Helpers;
using HotChocolate;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Snapshooter.MSTest;

namespace Azure.DataApiBuilder.Service.Tests.SqlTests
{
    [TestClass]
    public class GraphQLSchemaCreatorTests
    {
        private static DatabaseTable CreateDatabaseTable(string name)
        {
            DatabaseTable databaseTable = new("dbo", name.ToLower())
            {
                TableDefinition = new()
            };
            databaseTable.TableDefinition.Columns.Add("id", new() { IsAutoGenerated = true, SystemType = typeof(int) });
            databaseTable.TableDefinition.Columns.Add("name", new() { SystemType = typeof(string) });
            databaseTable.TableDefinition.PrimaryKey.Add("id");
            return databaseTable;
        }

        private static Entity CreateEntity(string name)
        {
            return new Entity(JsonSerializer.SerializeToElement(name.ToLower()), null, name, new[] { new PermissionSetting(Operation.Read.ToString(), new[] { "*" }), new PermissionSetting(Operation.Create.ToString(), new[] { "*" }) }, null, null);
        }

        [TestMethod]
        public void SingleObjectSchema()
        {
            DataSource dataSource = new(DatabaseType.mssql);
            MsSqlOptions dbOptions = new();

            Dictionary<string, Entity> entities = new()
            {
                {"Author", CreateEntity("Author") }
            };
            RuntimeConfig config = new("", dataSource, null, dbOptions, null, null, null, entities);
            Mock<ILogger<RuntimeConfigProvider>> configProviderLogger = new();
            RuntimeConfigProvider runtimeConfigProvider = new(config, configProviderLogger.Object);
            DatabaseTable databaseTable = CreateDatabaseTable("Author");

            MsSqlMetadataProvider metadataProvider = new(runtimeConfigProvider, null, null, new Mock<ILogger<ISqlMetadataProvider>>().Object)
            {
                EntityToDatabaseObject = new() { { "Author", databaseTable } }
            };

            Dictionary<string, EntityMetadata> entityMap = GraphQLTestHelpers.CreateStubEntityPermissionsMap(
                    entityNames: new[] { "Author" },
                    operations: new Operation[] { Operation.Create, Operation.Read, Operation.Update, Operation.Delete },
                    roles: new string[] { "anonymous", "authenticated" }
                );

            Mock<IAuthorizationResolver> authResolver = new();
            authResolver.Setup(x => x.GetRolesForEntity(It.IsAny<string>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.GetRolesForField(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Operation>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.EntityPermissionsMap).Returns(entityMap);

            GraphQLSchemaCreator schemaCreator = new(
                runtimeConfigProvider,
                null,
                null,
                metadataProvider,
                authResolver.Object);

            ISchemaBuilder schemaBuilder = schemaCreator.InitializeSchemaAndResolvers(new SchemaBuilder());
            ISchema schema = schemaBuilder.Create();

            Snapshot.Match(schema.ToString());
        }

        [TestMethod]
        public void MultipleObjectSchema()
        {
            DataSource dataSource = new(DatabaseType.mssql);
            MsSqlOptions dbOptions = new();

            Dictionary<string, Entity> entities = new()
            {
                {"Author", CreateEntity("Author") },
                {"Book", CreateEntity("Book") }
            };
            RuntimeConfig config = new("", dataSource, null, dbOptions, null, null, null, entities);
            Mock<ILogger<RuntimeConfigProvider>> configProviderLogger = new();
            RuntimeConfigProvider runtimeConfigProvider = new(config, configProviderLogger.Object);

            MsSqlMetadataProvider metadataProvider = new(runtimeConfigProvider, null, null, new Mock<ILogger<ISqlMetadataProvider>>().Object)
            {
                EntityToDatabaseObject = new() {
                    { "Author", CreateDatabaseTable("Author") },
                    { "Book", CreateDatabaseTable("Book") }
                }
            };

            Dictionary<string, EntityMetadata> entityMap = GraphQLTestHelpers.CreateStubEntityPermissionsMap(
                    entityNames: new[] { "Author", "Book" },
                    operations: new Operation[] { Operation.Create, Operation.Read, Operation.Update, Operation.Delete },
                    roles: new string[] { "anonymous", "authenticated" }
                );

            Mock<IAuthorizationResolver> authResolver = new();
            authResolver.Setup(x => x.GetRolesForEntity(It.IsAny<string>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.GetRolesForField(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Operation>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.EntityPermissionsMap).Returns(entityMap);

            GraphQLSchemaCreator schemaCreator = new(
                runtimeConfigProvider,
                null,
                null,
                metadataProvider,
                authResolver.Object);

            ISchemaBuilder schemaBuilder = schemaCreator.InitializeSchemaAndResolvers(new SchemaBuilder());
            ISchema schema = schemaBuilder.Create();

            Snapshot.Match(schema.ToString());
        }

        [TestMethod]
        public void OneWayRelationship()
        {
            DataSource dataSource = new(DatabaseType.mssql);
            MsSqlOptions dbOptions = new();

            Entity authorEntity = CreateEntity("Author") with
            {
                Relationships = new() {
                    { "Book", new(Cardinality.Many, "dbo.Book", new[] { "book_id" }, new[] { "id" }, null, null, null) }
                }
            };
            Dictionary<string, Entity> entities = new()
            {
                {"Author", authorEntity },
                {"Book", CreateEntity("Book") }
            };
            RuntimeConfig config = new("", dataSource, null, dbOptions, null, null, null, entities);
            Mock<ILogger<RuntimeConfigProvider>> configProviderLogger = new();
            RuntimeConfigProvider runtimeConfigProvider = new(config, configProviderLogger.Object);

            DatabaseTable authorTable = CreateDatabaseTable("Author");
            authorTable.TableDefinition.Columns.Add("book_id", new ColumnDefinition { SystemType = typeof(int) });
            MsSqlMetadataProvider metadataProvider = new(runtimeConfigProvider, null, null, new Mock<ILogger<ISqlMetadataProvider>>().Object)
            {
                EntityToDatabaseObject = new() {
                    { "Author", authorTable },
                    { "Book", CreateDatabaseTable("Book") }
                }
            };

            Dictionary<string, EntityMetadata> entityMap = GraphQLTestHelpers.CreateStubEntityPermissionsMap(
                    entityNames: new[] { "Author", "Book" },
                    operations: new Operation[] { Operation.Create, Operation.Read, Operation.Update, Operation.Delete },
                    roles: new string[] { "anonymous", "authenticated" }
                );

            Mock<IAuthorizationResolver> authResolver = new();
            authResolver.Setup(x => x.GetRolesForEntity(It.IsAny<string>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.GetRolesForField(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Operation>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.EntityPermissionsMap).Returns(entityMap);

            GraphQLSchemaCreator schemaCreator = new(
                runtimeConfigProvider,
                null,
                null,
                metadataProvider,
                authResolver.Object);

            ISchemaBuilder schemaBuilder = schemaCreator.InitializeSchemaAndResolvers(new SchemaBuilder());
            ISchema schema = schemaBuilder.Create();

            Snapshot.Match(schema.ToString());
        }

        [TestMethod]
        public void RecursiveRelationship()
        {
            DataSource dataSource = new(DatabaseType.mssql);
            MsSqlOptions dbOptions = new();

            Entity authorEntity = CreateEntity("Author") with
            {
                Relationships = new() {
                    { "Book", new(Cardinality.Many, "dbo.Book", null, null, "dbo.Book_Author", new[] { "id"}, new [] { "author_id" }) }
                }
            };
            Entity bookEntity = CreateEntity("Book") with
            {
                Relationships = new() {
                    { "Author", new(Cardinality.Many, "dbo.Author", null, null, "dbo.Book_Author", new[] { "id"}, new [] { "book_id" }) }
                }
            };
            Dictionary<string, Entity> entities = new()
            {
                {"Author", authorEntity },
                {"Book", bookEntity }
            };
            RuntimeConfig config = new("", dataSource, null, dbOptions, null, null, null, entities);
            Mock<ILogger<RuntimeConfigProvider>> configProviderLogger = new();
            RuntimeConfigProvider runtimeConfigProvider = new(config, configProviderLogger.Object);

            MsSqlMetadataProvider metadataProvider = new(runtimeConfigProvider, null, null, new Mock<ILogger<ISqlMetadataProvider>>().Object)
            {
                EntityToDatabaseObject = new() {
                    { "Author", CreateDatabaseTable("Author") },
                    { "Book", CreateDatabaseTable("Book") }
                }
            };

            Dictionary<string, EntityMetadata> entityMap = GraphQLTestHelpers.CreateStubEntityPermissionsMap(
                    entityNames: new[] { "Author", "Book" },
                    operations: new Operation[] { Operation.Create, Operation.Read, Operation.Update, Operation.Delete },
                    roles: new string[] { "anonymous", "authenticated" }
                );

            Mock<IAuthorizationResolver> authResolver = new();
            authResolver.Setup(x => x.GetRolesForEntity(It.IsAny<string>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.GetRolesForField(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Operation>())).Returns(new[] { "*" });
            authResolver.Setup(x => x.EntityPermissionsMap).Returns(entityMap);

            GraphQLSchemaCreator schemaCreator = new(
                runtimeConfigProvider,
                null,
                null,
                metadataProvider,
                authResolver.Object);

            ISchemaBuilder schemaBuilder = schemaCreator.InitializeSchemaAndResolvers(new SchemaBuilder());
            ISchema schema = schemaBuilder.Create();

            Snapshot.Match(schema.ToString());
        }
    }
}
