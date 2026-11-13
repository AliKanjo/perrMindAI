using System.Data;
using System.Data.Common;
using MySqlConnector;

namespace PeerMindAI.Data;

/// <summary>
/// Small helper around MySqlConnector. Every repository uses it to call the
/// stored procedures in database/02_procedures.sql (or a plain SQL query).
/// All values are sent as parameters, never concatenated into the SQL text.
/// </summary>
public class Database
{
    private const string BusinessRuleSqlState = "45000";
    private readonly string _connectionString;

    public Database(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PeerMind")
            ?? throw new InvalidOperationException("Connection string 'PeerMind' is missing in appsettings.json.");
    }

    /// <summary>Input parameter. A null value is sent as SQL NULL.</summary>
    public static MySqlParameter In(string name, object? value) => new(name, value ?? DBNull.Value);

    /// <summary>OUT parameter of a stored procedure (read it with <see cref="OutInt"/> afterwards).</summary>
    public static MySqlParameter Out(string name) =>
        new(name, MySqlDbType.Int32) { Direction = ParameterDirection.Output };

    public static int OutInt(MySqlParameter parameter) => Convert.ToInt32(parameter.Value);

    /// <summary>Runs a stored procedure that returns one result set.</summary>
    public Task<List<T>> QueryProcedureAsync<T>(string procedure, Func<DbDataReader, T> map, params MySqlParameter[] parameters) =>
        QueryAsync(procedure, CommandType.StoredProcedure, map, parameters);

    /// <summary>Runs a SELECT statement.</summary>
    public Task<List<T>> QuerySqlAsync<T>(string sql, Func<DbDataReader, T> map, params MySqlParameter[] parameters) =>
        QueryAsync(sql, CommandType.Text, map, parameters);

    /// <summary>Runs a stored procedure that returns no rows (OUT parameters are filled in).</summary>
    public Task<int> ExecuteProcedureAsync(string procedure, params MySqlParameter[] parameters) =>
        ExecuteAsync(procedure, CommandType.StoredProcedure, parameters);

    /// <summary>Runs an INSERT / UPDATE / DELETE statement and returns the number of affected rows.</summary>
    public Task<int> ExecuteSqlAsync(string sql, params MySqlParameter[] parameters) =>
        ExecuteAsync(sql, CommandType.Text, parameters);

    /// <summary>
    /// Runs a stored procedure that returns several result sets
    /// (the caller moves between them with reader.NextResultAsync()).
    /// </summary>
    public async Task<T> ReadProcedureAsync<T>(string procedure, Func<DbDataReader, Task<T>> read, params MySqlParameter[] parameters)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = CreateCommand(connection, procedure, CommandType.StoredProcedure, parameters);
            await using var reader = await command.ExecuteReaderAsync();
            return await read(reader);
        }
        catch (MySqlException ex) when (ex.SqlState == BusinessRuleSqlState)
        {
            throw new DbRuleException(ex.Message);
        }
    }

    private async Task<List<T>> QueryAsync<T>(string text, CommandType type, Func<DbDataReader, T> map, MySqlParameter[] parameters)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = CreateCommand(connection, text, type, parameters);
            await using var reader = await command.ExecuteReaderAsync();

            var rows = new List<T>();
            while (await reader.ReadAsync())
            {
                rows.Add(map(reader));
            }
            return rows;
        }
        catch (MySqlException ex) when (ex.SqlState == BusinessRuleSqlState)
        {
            throw new DbRuleException(ex.Message);
        }
    }

    private async Task<int> ExecuteAsync(string text, CommandType type, MySqlParameter[] parameters)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = CreateCommand(connection, text, type, parameters);
            return await command.ExecuteNonQueryAsync();
        }
        catch (MySqlException ex) when (ex.SqlState == BusinessRuleSqlState)
        {
            throw new DbRuleException(ex.Message);
        }
    }

    private static MySqlCommand CreateCommand(MySqlConnection connection, string text, CommandType type, MySqlParameter[] parameters)
    {
        var command = new MySqlCommand(text, connection) { CommandType = type };
        command.Parameters.AddRange(parameters);
        return command;
    }
}
