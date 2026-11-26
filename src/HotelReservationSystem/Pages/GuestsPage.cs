namespace HotelReservationSystem.Pages;

internal sealed class GuestsPage : PageBase
{
    private readonly DataGridView _grid = Ui.Grid("No guests found.");
    private readonly TextBox _search = Ui.TextBox(100);
    private readonly TextBox _first = Ui.TextBox(50);
    private readonly TextBox _last = Ui.TextBox(50);
    private readonly TextBox _phone = Ui.TextBox(20);
    private readonly TextBox _email = Ui.TextBox(100);
    private readonly TextBox _nationalId = Ui.TextBox(30);
    private readonly Label _editorTitle = EditorTitle("New guest");
    private readonly Label _error = Ui.ErrorLabel();
    private readonly Button _delete;
    private readonly System.Windows.Forms.Timer _searchDelay = new() { Interval = 300 };
    private int _editingId;

    public GuestsPage(AppServices services, AuthenticatedUser user) : base(services, user)
    {
        var save = Ui.Button("Save", ButtonKind.Primary, (_, _) => Save());
        var clear = Ui.Button("New guest", ButtonKind.Secondary, (_, _) => ClearEditor());
        _delete = Ui.Button("Delete", ButtonKind.Danger, (_, _) => Delete());

        var stack = Ui.FieldStack(
            ("", _editorTitle),
            ("First name *", _first),
            ("Last name *", _last),
            ("Phone", _phone),
            ("Email", _email),
            ("National ID / Passport no.", _nationalId));
        stack.Controls.Add(_error);
        stack.Controls.Add(Ui.ButtonRow(save, clear, _delete));
        stack.Controls.Add(Ui.Muted("Personal data is visible only to signed-in staff.\nID numbers are masked in the list."));

        _grid.Columns.AddRange(
            Ui.Column(nameof(Guest.GuestId), "ID", 40, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(Guest.FullName), "Name", 140),
            Ui.Column(nameof(Guest.Phone), "Phone", 110),
            Ui.Column(nameof(Guest.Email), "Email", 170),
            Ui.Column(nameof(Guest.MaskedNationalId), "ID / Passport", 100),
            Ui.DateColumn(nameof(Guest.CreatedAt), "Registered", 80));
        Ui.OnSelected<Guest>(_grid, LoadEditor);

        _search.Width = 300;
        _search.PlaceholderText = "Search name, phone, email or ID…";
        _search.AccessibleName = "Search";
        _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); RefreshData(); };

        Controls.Add(EditorLayout(EditorCard(stack), ToolbarAndGrid(Toolbar(_search), _grid)));
        ClearEditor();
    }

    public override string Title => "Guests";
    public override string Subtitle => "Guest records are kept separately from reservations.";

    public override void RefreshData() => RefreshData(null);

    private void RefreshData(int? select) =>
        ErrorHandler.Try(this, () => Ui.Bind(_grid, Services.Guests.Search(_search.Text), g => g.GuestId, select));

    private void LoadEditor(Guest g)
    {
        _editingId = g.GuestId;
        _editorTitle.Text = $"Edit guest #{g.GuestId}";
        _first.Text = g.FirstName;
        _last.Text = g.LastName;
        _phone.Text = g.Phone;
        _email.Text = g.Email;
        _nationalId.Text = g.NationalId;
        _delete.Enabled = true;
        Ui.SetError(_error, null);
    }

    private void ClearEditor()
    {
        _editingId = 0;
        _editorTitle.Text = "New guest";
        foreach (var t in new[] { _first, _last, _phone, _email, _nationalId }) t.Clear();
        _delete.Enabled = false;
        Ui.SetError(_error, null);
        _grid.ClearSelection();
        _first.Focus();
    }

    private void Save()
    {
        Ui.SetError(_error, null);
        var guest = new Guest
        {
            GuestId = _editingId, FirstName = _first.Text, LastName = _last.Text,
            Phone = _phone.Text, Email = _email.Text, NationalId = _nationalId.Text
        };
        try
        {
            var id = Services.Guests.Save(CurrentUser, guest);
            RefreshData(id);
        }
        catch (BusinessRuleException ex) { Ui.SetError(_error, ex.Message); }
        catch (Exception ex) { ErrorHandler.Show(this, ex); }
    }

    private void Delete()
    {
        if (_editingId == 0) return;
        if (!Dialogs.Confirm(this, $"Delete guest \"{_first.Text} {_last.Text}\"? This cannot be undone.")) return;
        if (ErrorHandler.Try(this, () => Services.Guests.Delete(CurrentUser, _editingId)))
        {
            ClearEditor();
            RefreshData();
        }
    }
}
