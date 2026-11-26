using HotelReservationSystem.Core.Configuration;

namespace HotelReservationSystem.Forms;

internal sealed class FrmLogin : Form
{
    private readonly AppServices _services;
    private readonly AppSettings _settings;
    private readonly TextBox _username = Ui.TextBox(50);
    private readonly TextBox _password = Ui.TextBox(128);
    private readonly Label _error = Ui.ErrorLabel();
    private AuthenticatedUser? _user;
    private bool _mustChangePassword;

    public FrmLogin(AppServices services, AppSettings settings)
    {
        _services = services;
        _settings = settings;

        Ui.InitForm(this, "Hotel Reservation System — Sign in", 820, 500);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Theme.Surface;

        BuildUi();
        Shown += (_, _) => _username.Focus();
    }

    public AuthenticatedUser? SignedInUser => _user;

    /// <summary>True when the signed-in account has a temporary or demo password.</summary>
    public bool MustChangePassword => _mustChangePassword;

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        Controls.Add(root);

        // Brand panel
        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(36, 48, 36, 36), Margin = Padding.Empty };
        var brandText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Theme.Sidebar };
        brandText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brandText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brandText.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        brandText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brandText.Controls.Add(new Label
        {
            Text = "Hotel\nReservation\nSystem", AutoSize = true, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 22f), Margin = new Padding(0, 0, 0, 16)
        });
        brandText.Controls.Add(new Label
        {
            Text = "Rooms, guests, reservations and payments\nin one place.", AutoSize = true,
            ForeColor = Theme.SidebarText, Font = Theme.Base
        });
        brandText.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar });
        brandText.Controls.Add(new Label
        {
            Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true,
            ForeColor = Theme.SidebarText, Font = Theme.Small
        });
        brand.Controls.Add(brandText);
        root.Controls.Add(brand, 0, 0);

        // Sign-in form
        var formPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(56, 64, 56, 24), BackColor = Theme.Surface };
        _password.UseSystemPasswordChar = true;

        var signIn = Ui.Button("Sign in", ButtonKind.Primary, (_, _) => SignIn());
        signIn.Dock = DockStyle.Fill;
        signIn.Margin = new Padding(0, 8, 0, 0);
        AcceptButton = signIn;

        var stack = Ui.FieldStack(
            ("", Ui.Label("Sign in", Theme.Title)),
            ("", Ui.Muted("Use your staff account to continue.")),
            ("Username", _username),
            ("Password", _password));
        stack.Controls[1].Margin = new Padding(0, 0, 0, 20);
        stack.Controls.Add(_error);
        stack.Controls.Add(signIn);

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1 };
        footer.Controls.Add(Ui.Muted($"Database: {_services.Database.Describe()}"));
        var settingsLink = new LinkLabel { Text = "Database settings…", AutoSize = true, LinkColor = Theme.Primary, Font = Theme.Small };
        settingsLink.LinkClicked += (_, _) => OpenDatabaseSettings();
        footer.Controls.Add(settingsLink);

        formPanel.Controls.Add(stack);
        formPanel.Controls.Add(footer);
        root.Controls.Add(formPanel, 1, 0);
    }

    private void SignIn()
    {
        Ui.SetError(_error, null);
        LoginResult? result = null;
        if (!ErrorHandler.Try(this, () => result = _services.Auth.Login(_username.Text, _password.Text)) || result is null)
            return;

        if (!result.Succeeded)
        {
            Ui.SetError(_error, result.Error);
            _password.SelectAll();
            _password.Focus();
            return;
        }

        _user = result.User;
        _mustChangePassword = result.MustChangePassword;
        DialogResult = DialogResult.OK;
    }

    private void OpenDatabaseSettings()
    {
        using var dlg = new FrmDatabaseSettings(_settings, error: null);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            DialogResult = DialogResult.Retry; // restart with the new connection
    }
}
