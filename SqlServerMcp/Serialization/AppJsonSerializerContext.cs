using System.Text.Json.Serialization;
using SqlServerMcp.Contracts;

namespace SqlServerMcp.Serialization;

/// <summary>
/// Source-generated JSON serialization context used by the MCP server.
/// </summary>
[JsonSerializable(typeof(JsonToolResponse))]
[JsonSerializable(typeof(DatabaseInfoRequest))]
[JsonSerializable(typeof(ListSchemasRequest))]
[JsonSerializable(typeof(ListTablesRequest))]
[JsonSerializable(typeof(TableDetailsRequest))]
[JsonSerializable(typeof(ObjectDefinitionRequest))]
[JsonSerializable(typeof(QuerySqlRequest))]
[JsonSerializable(typeof(PreviewDataChangeRequest))]
[JsonSerializable(typeof(ExecuteDataChangeRequest))]
[JsonSerializable(typeof(CreateTableRequest))]
[JsonSerializable(typeof(AlterTableRequest))]
[JsonSerializable(typeof(CreateLoginRequest))]
[JsonSerializable(typeof(CreateUserRequest))]
[JsonSerializable(typeof(GrantRoleMembershipRequest))]
[JsonSerializable(typeof(ExecuteAdminSqlRequest))]
[JsonSerializable(typeof(DatabaseInfo))]
[JsonSerializable(typeof(SchemaInfo[]))]
[JsonSerializable(typeof(TableInfo[]))]
[JsonSerializable(typeof(TableDetails))]
[JsonSerializable(typeof(ObjectDefinitionInfo))]
[JsonSerializable(typeof(QueryResult))]
[JsonSerializable(typeof(MutationPreviewResult))]
[JsonSerializable(typeof(MutationExecutionResult))]
[JsonSerializable(typeof(AdminExecutionResult))]
public partial class AppJsonSerializerContext : JsonSerializerContext;
