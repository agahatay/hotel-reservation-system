using HotelReservationSystem.Core.Configuration;
using HotelReservationSystem.Core.Data;
using Microsoft.Data.SqlClient;

namespace HotelReservationSystem.Forms;

/// <summary>
/// Lets the user point the application at a SQL Server instance. Only Windows
/// authentication is offered, so no password is ever written to disk.
/// </summary>
internal sealed class FrmDatabaseSettings : Form
{
    private readonly TextBox _server = Ui.TextBox(200);
    private readonly TextBox _database = Ui.TextBox(128);
    private readonly CheckBox _trustCertificate = new() { Text = "Trust server certificate (local SQL Server Express / LocalDB)", AutoSize = true, Checked = true };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 8, 0, 0) };

    public FrmDatabaseSettings(AppSettings current, string? error)
    {
        Ui.InitDialog(this, "Database settings", 580, error is null ? 520 : 620);
        if (Application.OpenForms.Count == 0) { StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = true; }
        Padding = new Padding(28);

        try
        {
            var b = new SqlConnectionStringBuilder(current.ConnectionString);
            _server.Text = b.DataSource;
            _database.Text = b.InitialCatalog;
            _trustCertificate.Checked = b.TrustServerCertificate;
        }
        catch (ArgumentException)
        {
            _server.Text = ".\\SQLEXPRESS";
            _database.Text = "HotelReservationDB";
        }

        var test = Ui.Button("Test connection", ButtonKind.Secondary, (_, _) => Test());
        var create = Ui.Button("Create / update database", ButtonKind.Secondary, (_, _) => CreateDatabase());
        var save = Ui.Button("Save", ButtonKind.Primary, (_, _) => Save());
        var cancel = Ui.Button("Cancel", ButtonKind.Secondary, (_, _) => DialogResult = DialogResult.Cancel);
        AcceptButton = save;
        CancelButton = cancel;

        var intro = new Label
        {
            Text = "Enter the SQL Server instance that hosts the HotelReservationDB database. " +
                   "Windows authentication is used. Examples: .\\SQLEXPRESS, (localdb)\\MSSQLLocalDB, SERVER01\\SQLEXPRESS",
            AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = Theme.MutedText, Margin = new Padding(0, 0, 0, 14)
        };

        var stack = Ui.FieldStack(
            ("", Ui.Label("Connect to SQL Server", Theme.Section)),
            ("", intro),
            ("Server", _server),
            ("Database", _database),
            ("", _trustCertificate));

        if (error is not null)
        {
            stack.Controls.Add(new Label
            {
                Text = $"Could not connect using the current settings ({current.Source}):\n{error}",
                AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = Theme.Danger, Margin = new Padding(0, 8, 0, 0)
            });
        }

        stack.Controls.Add(_status);
        stack.Controls.Add(Ui.ButtonRow(test, create));
        stack.Controls.Add(Ui.ButtonRow(save, cancel));
        stack.Controls.Add(Ui.Muted($"Saved to {AppSettings.UserSettingsPath}"));
        Controls.Add(stack);
    }

    private string BuildConnectionString() => new SqlConnectionStringBuilder
    {
        DataSource = _server.Text.Trim(),
        InitialCatalog = _database.Text.Trim(),
        IntegratedSecurity = true,
        Encrypt = SqlConnectionEncryptOption.Mandatory,
        TrustServerCertificate = _trustCertificate.Checked,
        ConnectTimeout = 8
    }.ConnectionString;

    private bool Test()
    {
        if (string.IsNullOrWhiteSpace(_server.Text) || string.IsNullOrWhiteSpace(_database.Text))
        {
            ShowStatus("Server and database are required.", ok: false);
            return false;
        }

        UseWaitCursor = true;
        try
        {
            var ok = new SqlConnectionFactory(BuildConnectionString()).TryConnect(out var error);
            ShowStatus(ok ? "Connection succeeded." : $"Connection failed: {error}", ok);
            return ok;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    /// <summary>Creates the database (or adds missing tables) using the scripts embedded in the application.</summary>
    private void CreateDatabase()
    {
        if (string.IsNullOrWhiteSpace(_server.Text) || string.IsNullOrWhiteSpace(_database.Text))
        {
            ShowStatus("Server and database are required.", ok: false);
            return;
        }

        var answer = MessageBox.Show(this,
            $"Create or update the database \"{_database.Text.Trim()}\" on {_server.Text.Trim()}?\n\n" +
            "Existing data is never changed.\n\n" +
            "Load the fictional demo data (rooms, guests, reservations and the demo accounts admin / staff)?\n" +
            "Yes = with demo data, No = empty database (you will create the administrator account).",
            "Create database", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
        if (answer == DialogResult.Cancel) return;

        UseWaitCursor = true;
        try
        {
            DatabaseInstaller.Install(BuildConnectionString(), includeDemoData: answer == DialogResult.Yes);
            ShowStatus("The database is ready. Click Save to use it.", ok: true);
        }
        catch (Exception ex) when (ex is SqlException or ArgumentException or InvalidOperationException)
        {
            ShowStatus($"The database could not be created: {ex.Message}", ok: false);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void Save()
    {
        if (!Test() && !Dialogs.Confirm(this, "The connection test failed. Save these settings anyway?"))
            return;
        try
        {
            AppSettings.SaveUserConnectionString(BuildConnectionString());
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"Could not save settings: {ex.Message}", ok: false);
        }
    }

    private void ShowStatus(string text, bool ok)
    {
        _status.Text = text;
        _status.ForeColor = ok ? Theme.Success : Theme.Danger;
    }
}
