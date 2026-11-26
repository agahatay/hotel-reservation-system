using System.Data;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

/// <summary>Small helpers that keep repository code short while using strongly typed parameters.</summary>
internal static class SqlExtensions
{
    public static SqlCommand Command(this SqlConnection con, string sql, SqlTransaction? tx = null) =>
        new(sql, con, tx) { CommandTimeout = 30 };

    public static SqlCommand Int(this SqlCommand cmd, string name, int? value) =>
        cmd.Add(name, SqlDbType.Int, value);

    public static SqlCommand Bit(this SqlCommand cmd, string name, bool value) =>
        cmd.Add(name, SqlDbType.Bit, value);

    public static SqlCommand Date(this SqlCommand cmd, string name, DateTime value) =>
        cmd.Add(name, SqlDbType.Date, value.Date);

    public static SqlCommand DateTime2(this SqlCommand cmd, string name, DateTime? value) =>
        cmd.Add(name, SqlDbType.DateTime2, value);

    public static SqlCommand NVarChar(this SqlCommand cmd, string name, string? value, int size) =>
        cmd.Add(name, SqlDbType.NVarChar, value, size);

    public static SqlCommand Char(this SqlCommand cmd, string name, string value, int size) =>
        cmd.Add(name, SqlDbType.Char, value, size);

    public static SqlCommand Money(this SqlCommand cmd, string name, decimal value)
    {
        var p = cmd.Parameters.Add(name, SqlDbType.Decimal);
        p.Precision = 12;
        p.Scale = 2;
        p.Value = value;
        return cmd;
    }

    private static SqlCommand Add(this SqlCommand cmd, string name, SqlDbType type, object? value, int size = 0)
    {
        var p = cmd.Parameters.Add(name, type);
        if (size > 0) p.Size = size;
        p.Value = value ?? DBNull.Value;
        return cmd;
    }

    public static List<T> ReadAll<T>(this SqlCommand cmd, Func<SqlDataReader, T> map)
    {
        var list = new List<T>();
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(map(rd));
        return list;
    }

    public static T? ReadSingle<T>(this SqlCommand cmd, Func<SqlDataReader, T> map) where T : class
    {
        using var rd = cmd.ExecuteReader();
        return rd.Read() ? map(rd) : null;
    }

    public static int ScalarInt(this SqlCommand cmd) => Convert.ToInt32(cmd.ExecuteScalar());

    public static decimal ScalarDecimal(this SqlCommand cmd)
    {
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? 0m : Convert.ToDecimal(v);
    }

    public static string? NullableString(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static int? NullableInt(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    public static DateTime? NullableDateTime(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }

    public static TEnum Enum<TEnum>(this SqlDataReader r, string column) where TEnum : struct, System.Enum =>
        System.Enum.Parse<TEnum>(r.GetString(r.GetOrdinal(column)).Trim(), ignoreCase: true);
}

/// <summary>SQL Server error numbers the application handles explicitly.</summary>
internal static class SqlErrors
{
    public const int ForeignKeyViolation = 547;
    public const int UniqueIndexViolation = 2601;
    public const int UniqueConstraintViolation = 2627;
    public const int Deadlock = 1205;

    public static bool IsUniqueViolation(SqlException ex) =>
        ex.Number is UniqueIndexViolation or UniqueConstraintViolation;
}
