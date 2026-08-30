using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Data;

public static class SqlDataReaderExtensions
{
    public static string? NullableString(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : (string)reader[name];
    public static int? NullableInt32(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : (int)reader[name];
    public static decimal? NullableDecimal(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : (decimal)reader[name];
    public static bool? NullableBoolean(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : (bool)reader[name];
    public static DateTime? NullableDateTime(this SqlDataReader reader, string name) => reader[name] is DBNull ? null : (DateTime)reader[name];
}