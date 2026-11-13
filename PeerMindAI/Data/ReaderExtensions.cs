using System.Data.Common;

namespace PeerMindAI.Data;

/// <summary>Read a column by name and convert it, treating SQL NULL safely.</summary>
public static class ReaderExtensions
{
    private static bool IsNull(DbDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column));

    public static string Text(this DbDataReader reader, string column) =>
        IsNull(reader, column) ? "" : Convert.ToString(reader[column]) ?? "";

    public static string? TextOrNull(this DbDataReader reader, string column) =>
        IsNull(reader, column) ? null : Convert.ToString(reader[column]);

    public static int Int(this DbDataReader reader, string column) =>
        IsNull(reader, column) ? 0 : Convert.ToInt32(reader[column]);

    public static int? IntOrNull(this DbDataReader reader, string column) =>
        IsNull(reader, column) ? null : Convert.ToInt32(reader[column]);

    public static decimal Decimal(this DbDataReader reader, string column) =>
        IsNull(reader, column) ? 0 : Convert.ToDecimal(reader[column]);

    public static decimal? DecimalOrNull(this DbDataReader reader, string column) =>
        IsNull(reader, column) ? null : Convert.ToDecimal(reader[column]);

    public static bool Bool(this DbDataReader reader, string column) =>
        !IsNull(reader, column) && Convert.ToBoolean(reader[column]);

    public static DateTime Date(this DbDataReader reader, string column) =>
        Convert.ToDateTime(reader[column]);

    public static DateTime? DateOrNull(this DbDataReader reader, string column) =>
        IsNull(reader, column) ? null : Convert.ToDateTime(reader[column]);
}
