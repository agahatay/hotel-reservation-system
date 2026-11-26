using HotelReservationSystem.Forms;

namespace HotelReservationSystem.Pages;

/// <summary>Staff accounts (administrators only).</summary>
internal sealed class UsersPage : PageBase
{
    private readonly DataGridView _grid = Ui.Grid("No users.");
    private readonly TextBox _username = Ui.TextBox(50);
    private readonly TextBox _password = Ui.TextBox(128);
    private readonly Label _passwordLabel = Ui.Label("Password *", Theme.Bold, Theme.MutedText);
    private readonly ComboBox _role = Ui.DropDown();
    private readonly CheckBox _active = new() { Text = "Account is active", AutoSize = true, Checked = true, Margin = new Padding(0, 2, 0, 6) };
    private readonly Label _title = EditorTitle("New user");
    private readonly Label _error = Ui.ErrorLabel();
    private readonly Button _reset;
    private int _editingId;

    public UsersPage(AppServices services, AuthenticatedUser user) : base(services, user)
    {
        _password.UseSystemPasswordChar = true;
        _password.AccessibleName = "Password";
        _role.AccessibleName = "Role";
        _role.DataSource = Enum.GetValues<UserRole>();

        var save = Ui.Button("Save", ButtonKind.Primary, (_, _) => Save());
        var clear = Ui.Button("New user", ButtonKind.Secondary, (_, _) => ClearEditor());
        _reset = Ui.Button("Reset password…", ButtonKind.Secondary, (_, _) => ResetPassword());

        var stack = Ui.FieldStack(("", _title), ("Username *", _username));
        stack.Controls.Add(_passwordLabel);
        _password.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        stack.Controls.Add(_password);
        stack.Controls.Add(Ui.Label("Role", Theme.Bold, Theme.MutedText));
        _role.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        stack.Controls.Add(_role);
        stack.Controls.Add(_active);
        stack.Controls.Add(_error);
        stack.Controls.Add(Ui.ButtonRow(save, clear));
        stack.Controls.Add(Ui.ButtonRow(_reset));
        stack.Controls.Add(Ui.Muted("Admin: everything.\nStaff: guests, reservations, payments, dashboard.\nPasswords are stored as salted PBKDF2 hashes.\nNew and reset passwords must be changed at\nthe next sign-in."));

        _grid.Columns.AddRange(
            Ui.Column(nameof(UserAccount.Username), "Username", 140),
            Ui.Column(nameof(UserAccount.Role), "Role", 70),
            Ui.CheckColumn(nameof(UserAccount.IsActive), "Active", 50),
            Ui.CheckColumn(nameof(UserAccount.MustChangePassword), "Must change password", 80),
            Ui.Column(nameof(UserAccount.LockoutEndUtc), "Locked until", 110),
            Ui.Column(nameof(UserAccount.CreatedAt), "Created", 110, "dd.MM.yyyy HH:mm"));
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(UserAccount.LockoutEndUtc)) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is UserAccount u)
            {
                e.Value = u.LockoutEndUtc is { } end && end > DateTime.UtcNow ? end.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "";
                e.FormattingApplied = true;
            }
        };
        Ui.OnSelected<UserAccount>(_grid, LoadEditor);

        Controls.Add(EditorLayout(EditorCard(stack), GridCard(_grid)));
        ClearEditor();
    }

    public override string Title => "Users";
    public override string Subtitle => "Staff accounts and roles.";

    public override void RefreshData() => RefreshData(null);

    private void RefreshData(int? select) =>
        ErrorHandler.Try(this, () => Ui.Bind(_grid, Services.Auth.ListUsers(CurrentUser), u => u.UserId, select));

    private void LoadEditor(UserAccount u)
    {
        _editingId = u.UserId;
        _title.Text = $"Edit user {u.Username}";
        _username.Text = u.Username;
        _username.ReadOnly = true;
        _password.Visible = _passwordLabel.Visible = false;
        _role.SelectedItem = u.Role;
        _active.Checked = u.IsActive;
        _reset.Enabled = true;
        Ui.SetError(_error, null);
    }

    private void ClearEditor()
    {
        _editingId = 0;
        _title.Text = "New user";
        _username.ReadOnly = false;
        _username.Clear();
        _password.Clear();
        _password.Visible = _passwordLabel.Visible = true;
        _role.SelectedItem = UserRole.Staff;
        _active.Checked = true;
        _reset.Enabled = false;
        Ui.SetError(_error, null);
        _grid.ClearSelection();
    }

    private void Save()
    {
        Ui.SetError(_error, null);
        var role = (UserRole)(_role.SelectedItem ?? UserRole.Staff);
        try
        {
            int id;
            if (_editingId == 0)
            {
                id = Services.Auth.CreateUser(CurrentUser, _username.Text, _password.Text, role);
                if (!_active.Checked) Services.Auth.UpdateUser(CurrentUser, id, role, isActive: false);
            }
            else
            {
                Services.Auth.UpdateUser(CurrentUser, _editingId, role, _active.Checked);
                id = _editingId;
            }
            RefreshData(id);
        }
        catch (BusinessRuleException ex) { Ui.SetError(_error, ex.Message); }
        catch (Exception ex) { ErrorHandler.Show(this, ex); }
    }

    private void ResetPassword()
    {
        if (_editingId == 0) return;
        var userId = _editingId;
        using var dlg = new FrmPasswordDialog("Reset password", $"Set a temporary password for {_username.Text}. Any lockout is cleared and the user must choose a new password at the next sign-in.",
            requireCurrent: false, save: (_, next) => Services.Auth.ResetPassword(CurrentUser, userId, next));
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            Dialogs.Info(this, "The password was reset.");
            RefreshData(userId);
        }
    }
}
