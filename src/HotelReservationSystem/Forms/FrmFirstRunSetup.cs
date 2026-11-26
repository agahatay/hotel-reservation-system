namespace HotelReservationSystem.Forms;

/// <summary>Shown when the database has no users: creates the first administrator account.</summary>
internal sealed class FrmFirstRunSetup : Form
{
    private readonly AppServices _services;
    private readonly TextBox _username = Ui.TextBox(50);
    private readonly TextBox _password = Ui.TextBox(128);
    private readonly TextBox _confirm = Ui.TextBox(128);
    private readonly Label _error = Ui.ErrorLabel();

    public FrmFirstRunSetup(AppServices services)
    {
        _services = services;
        Ui.InitDialog(this, "First-time setup", 460, 470);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Padding = new Padding(28);

        _username.Text = "admin";
        _password.UseSystemPasswordChar = true;
        _confirm.UseSystemPasswordChar = true;

        var create = Ui.Button("Create administrator", ButtonKind.Primary, (_, _) => Create());
        var cancel = Ui.Button("Cancel", ButtonKind.Secondary, (_, _) => DialogResult = DialogResult.Cancel);
        AcceptButton = create;
        CancelButton = cancel;

        var stack = Ui.FieldStack(
            ("", Ui.Label("Create the administrator account", Theme.Section)),
            ("", new Label
            {
                Text = "The database has no user accounts yet. The administrator can add staff accounts later.",
                AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = Theme.MutedText, Margin = new Padding(0, 0, 0, 16)
            }),
            ("Username", _username),
            ("Password (min. 8 characters, letters and digits)", _password),
            ("Confirm password", _confirm));
        stack.Controls.Add(_error);
        stack.Controls.Add(Ui.ButtonRow(create, cancel));
        Controls.Add(stack);
    }

    private void Create()
    {
        Ui.SetError(_error, null);
        if (_password.Text != _confirm.Text)
        {
            Ui.SetError(_error, "The passwords do not match.");
            return;
        }

        try
        {
            _services.Auth.CreateInitialAdmin(_username.Text, _password.Text);
            Dialogs.Info(this, "The administrator account was created. You can now sign in.");
            DialogResult = DialogResult.OK;
        }
        catch (BusinessRuleException ex)
        {
            Ui.SetError(_error, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Show(this, ex);
        }
    }
}
