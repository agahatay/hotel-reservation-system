using System.Data;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Core.Data;

public static class Transactions
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction and commits it. A deadlock victim
    /// is retried (the whole unit of work runs again) up to 3 times.
    /// </summary>
    public static T Run<T>(SqlConnectionFactory db, IsolationLevel isolation,
                           Func<SqlConnection, SqlTransaction, T> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var con = db.Open();
            using var tx = con.BeginTransaction(isolation);
            try
            {
                var result = work(con, tx);
                tx.Commit();
                return result;
            }
            catch (SqlException ex) when (ex.Number == SqlErrors.Deadlock && attempt < MaxAttempts)
            {
                // The server already rolled back the victim's transaction; try again.
            }
            catch
            {
                TryRollback(tx);
                throw;
            }
        }
    }

    public static void Run(SqlConnectionFactory db, IsolationLevel isolation, Action<SqlConnection, SqlTransaction> work) =>
        Run(db, isolation, (c, t) => { work(c, t); return 0; });

    private static void TryRollback(SqlTransaction tx)
    {
        try { tx.Rollback(); }
        catch (InvalidOperationException) { /* already completed by the server */ }
        catch (SqlException) { /* connection broken; server rolls back */ }
    }
}
