using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Common;

/// <summary>
/// Central error handling: rule violations are shown as friendly warnings, unexpected
/// errors are logged to %LOCALAPPDATA%\HotelReservationSystem\logs and reported without
/// crashing the application.
/// </summary>
internal static class ErrorHandler
{
    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "HotelReservationSystem", "logs");

    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Show(null, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Log(ex);
        };
    }

    /// <summary>Runs an action with a wait cursor; any exception is reported. Returns true on success.</summary>
    public static bool Try(IWin32Window? owner, Action action)
    {
        var previous = Cursor.Current;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            Cursor.Current = previous;
            Show(owner, ex);
            return false;
        }
        finally
        {
            Cursor.Current = previous;
        }
    }

    public static void Show(IWin32Window? owner, Exception ex)
    {
        switch (ex)
        {
            case BusinessRuleException rule:
                Dialogs.Warning(owner, rule.Message);
                break;

            case SqlException sql when IsConnectionError(sql):
                Log(ex);
                Dialogs.Error(owner,
                    "The database server could not be reached. Check that SQL Server is running and try again." +
                    $"{Environment.NewLine}{Environment.NewLine}{sql.Message}");
                break;

            case SqlException sql:
                Log(ex);
                Dialogs.Error(owner,
                    $"The database rejected the operation (error {sql.Number}). Nothing was saved." +
                    $"{Environment.NewLine}Details were written to the log folder:{Environment.NewLine}{LogDirectory}");
                break;

            default:
                Log(ex);
                Dialogs.Error(owner,
                    $"An unexpected error occurred: {ex.Message}{Environment.NewLine}{Environment.NewLine}" +
                    $"Details were written to the log folder:{Environment.NewLine}{LogDirectory}");
                break;
        }
    }

    private static bool IsConnectionError(SqlException ex) =>
        ex.Number is -2 or -1 or 2 or 53 or 4060 or 18456 or 233 or 10054 or 10060 or 10061 or 11001;

    public static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var file = Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException) { /* logging must never crash the app */ }
        catch (UnauthorizedAccessException) { }
    }
}

internal static class Dialogs
{
    private const string Caption = "Hotel Reservation System";

    public static void Info(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warning(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(IWin32Window? owner, string message, string title = "Please confirm") =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}
