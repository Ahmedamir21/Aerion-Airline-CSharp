using Microsoft.Data.SqlClient;

namespace Aerion.Data;

// All database access goes through this class (ADO.NET).
public class Db
{
    private readonly string _cs;

    public Db(IConfiguration config)
    {
        _cs = config.GetConnectionString("Aerion")
              ?? throw new InvalidOperationException("Connection string 'Aerion' is missing");
    }

    public SqlConnection Open()
    {
        var con = new SqlConnection(_cs);
        con.Open();
        return con;
    }

    public List<T> Query<T>(string sql, Func<SqlDataReader, T> map, params (string, object?)[] ps)
    {
        using var con = Open();
        return Runner.Query(con, null, sql, map, ps);
    }

    public T? One<T>(string sql, Func<SqlDataReader, T> map, params (string, object?)[] ps) where T : class
    {
        return Query(sql, map, ps).FirstOrDefault();
    }

    public object? Scalar(string sql, params (string, object?)[] ps)
    {
        using var con = Open();
        return Runner.Scalar(con, null, sql, ps);
    }

    public int Int(string sql, params (string, object?)[] ps)
    {
        var v = Scalar(sql, ps);
        return v == null || v is DBNull ? 0 : Convert.ToInt32(v);
    }

    public int Execute(string sql, params (string, object?)[] ps)
    {
        using var con = Open();
        return Runner.Execute(con, null, sql, ps);
    }

    // Runs several statements as one unit: all of them succeed or none is saved.
    public T InTransaction<T>(Func<Tx, T> work)
    {
        using var con = Open();
        using var tran = con.BeginTransaction();
        try
        {
            var result = work(new Tx(con, tran));
            tran.Commit();
            return result;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }
}

public class Tx
{
    private readonly SqlConnection _con;
    private readonly SqlTransaction _tran;

    public Tx(SqlConnection con, SqlTransaction tran)
    {
        _con = con;
        _tran = tran;
    }

    public List<T> Query<T>(string sql, Func<SqlDataReader, T> map, params (string, object?)[] ps)
        => Runner.Query(_con, _tran, sql, map, ps);

    public object? Scalar(string sql, params (string, object?)[] ps)
        => Runner.Scalar(_con, _tran, sql, ps);

    public int Execute(string sql, params (string, object?)[] ps)
        => Runner.Execute(_con, _tran, sql, ps);
}

internal static class Runner
{
    private static SqlCommand Make(SqlConnection con, SqlTransaction? tran, string sql, (string, object?)[] ps)
    {
        var cmd = new SqlCommand(sql, con, tran);
        foreach (var (name, value) in ps)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        return cmd;
    }

    public static List<T> Query<T>(SqlConnection con, SqlTransaction? tran, string sql,
        Func<SqlDataReader, T> map, (string, object?)[] ps)
    {
        using var cmd = Make(con, tran, sql, ps);
        using var reader = cmd.ExecuteReader();
        var list = new List<T>();
        while (reader.Read())
        {
            list.Add(map(reader));
        }
        return list;
    }

    public static object? Scalar(SqlConnection con, SqlTransaction? tran, string sql, (string, object?)[] ps)
    {
        using var cmd = Make(con, tran, sql, ps);
        return cmd.ExecuteScalar();
    }

    public static int Execute(SqlConnection con, SqlTransaction? tran, string sql, (string, object?)[] ps)
    {
        using var cmd = Make(con, tran, sql, ps);
        return cmd.ExecuteNonQuery();
    }
}

public static class ReaderExtensions
{
    public static string Str(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? "" : r.GetString(i);
    }

    public static string? NStr(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static int Int(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
    }

    public static int? NInt(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }

    public static decimal Dec(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
    }

    public static DateTime Date(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.GetDateTime(i);
    }

    public static DateTime? NDate(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }

    public static bool Bool(this SqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return !r.IsDBNull(i) && r.GetBoolean(i);
    }
}
