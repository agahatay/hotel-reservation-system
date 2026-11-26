namespace HotelReservationSystem.Pages;

internal sealed class ReservationsPage : PageBase
{
    private sealed record GuestOption(int Id, string Text);
    private sealed record StatusOption(ReservationStatus? Status, string Text);

    private readonly DateTimePicker _checkIn = Ui.DatePicker();
    private readonly DateTimePicker _checkOut = Ui.DatePicker();
    private readonly ComboBox _guest = Ui.DropDown();
    private readonly ComboBox _room = Ui.DropDown();
    private readonly Label _quote = new() { AutoSize = true, MaximumSize = new Size(290, 0), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 4) };
    private readonly Label _error = Ui.ErrorLabel();
    private readonly Button _create;

    private readonly DataGridView _grid = Ui.Grid("No reservations match the filter.");
    private readonly ComboBox _statusFilter = Ui.DropDown();
    private readonly TextBox _search = Ui.TextBox(100);
    private readonly Button _cancel;
    private readonly System.Windows.Forms.Timer _searchDelay = new() { Interval = 300 };
    private bool _loading;

    public ReservationsPage(AppServices services, AuthenticatedUser user) : base(services, user)
    {
        _create = Ui.Button("Create reservation", ButtonKind.Primary, (_, _) => Create());
        _cancel = Ui.Button("Cancel reservation", ButtonKind.Danger, (_, _) => CancelSelected());
        _cancel.Enabled = false;

        _checkIn.Value = DateTime.Today;
        _checkOut.Value = DateTime.Today.AddDays(1);
        _checkIn.ValueChanged += (_, _) =>
        {
            if (_checkOut.Value.Date <= _checkIn.Value.Date) _checkOut.Value = _checkIn.Value.Date.AddDays(1);
            LoadAvailableRooms();
        };
        _checkOut.ValueChanged += (_, _) => LoadAvailableRooms();
        _room.SelectedIndexChanged += (_, _) => UpdateQuote();

        var dates = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
        dates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dates.Controls.Add(Ui.FieldStack(("Check-in *", _checkIn)), 0, 0);
        dates.Controls.Add(Ui.FieldStack(("Check-out *", _checkOut)), 1, 0);
        dates.Controls[0].Margin = new Padding(0, 0, 8, 0);

        var stack = Ui.FieldStack(
            ("", EditorTitle("New reservation")),
            ("", dates),
            ("Guest *", _guest),
            ("Available room *", _room),
            ("", _quote));
        stack.Controls.Add(_error);
        stack.Controls.Add(Ui.ButtonRow(_create));
        stack.Controls.Add(Ui.Muted("Only active rooms without an overlapping booking\nare listed. Availability is checked again when\nthe reservation is saved."));

        _statusFilter.Width = 150;
        _statusFilter.DisplayMember = nameof(StatusOption.Text);
        _statusFilter.DataSource = new List<StatusOption>
        {
            new(null, "All statuses"),
            new(ReservationStatus.Booked, "Booked"),
            new(ReservationStatus.CheckedIn, "Checked in"),
            new(ReservationStatus.Completed, "Completed"),
            new(ReservationStatus.Cancelled, "Cancelled")
        };
        _statusFilter.SelectedIndexChanged += (_, _) => { if (!_loading) LoadGrid(); };
        _search.Width = 260;
        _search.PlaceholderText = "Search guest, room or reservation no.…";
        _search.AccessibleName = "Search";
        _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); LoadGrid(); };

        _grid.Columns.AddRange(
            Ui.Column(nameof(ReservationListItem.ReservationId), "No.", 45, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(ReservationListItem.GuestName), "Guest", 150),
            Ui.Column(nameof(ReservationListItem.RoomNumber), "Room", 55, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.DateColumn(nameof(ReservationListItem.CheckInDate), "Check-in"),
            Ui.DateColumn(nameof(ReservationListItem.CheckOutDate), "Check-out"),
            Ui.Column(nameof(ReservationListItem.Nights), "Nights", 50, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.MoneyColumn(nameof(ReservationListItem.TotalPrice), "Total"),
            Ui.Column(nameof(ReservationListItem.Currency), "Cur.", 40, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(ReservationListItem.Status), "Status", 80, align: DataGridViewContentAlignment.MiddleCenter));
        Ui.TintRowsByStatus(_grid, o => (o as ReservationListItem)?.Status);
        _grid.SelectionChanged += (_, _) =>
            _cancel.Enabled = Ui.Selected<ReservationListItem>(_grid) is { } r && ReservationRules.CanCancel(r.Status);

        var toolbar = Toolbar(Ui.Label("Status"), _statusFilter, _search, _cancel);
        Controls.Add(EditorLayout(EditorCard(stack), ToolbarAndGrid(toolbar, _grid), 360));
    }

    public override string Title => "Reservations";
    public override string Subtitle => "Book rooms for guests and manage existing reservations.";

    public override void RefreshData()
    {
        ErrorHandler.Try(this, () =>
        {
            Services.Reservations.RefreshStatuses();

            var selectedGuest = _guest.SelectedValue;
            _guest.DisplayMember = nameof(GuestOption.Text);
            _guest.ValueMember = nameof(GuestOption.Id);
            _guest.DataSource = Services.Guests.Search(null)
                .Select(g => new GuestOption(g.GuestId, string.IsNullOrEmpty(g.Phone) ? g.FullName : $"{g.FullName}  ·  {g.Phone}"))
                .OrderBy(g => g.Text, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (selectedGuest is not null) _guest.SelectedValue = selectedGuest;
        });
        LoadAvailableRooms();
        LoadGrid();
    }

    private void LoadGrid(int? select = null)
    {
        var status = (_statusFilter.SelectedItem as StatusOption)?.Status;
        ErrorHandler.Try(this, () => Ui.Bind(_grid, Services.Reservations.List(status, _search.Text), r => r.ReservationId, select));
    }

    private void LoadAvailableRooms()
    {
        Ui.SetError(_error, null);
        var error = ReservationRules.ValidateNewStay(_checkIn.Value, _checkOut.Value, DateTime.Today);
        if (error is not null)
        {
            _room.DataSource = null;
            Ui.SetError(_error, error);
            UpdateQuote();
            return;
        }

        ErrorHandler.Try(this, () =>
        {
            var previous = _room.SelectedValue;
            var rooms = Services.Reservations.ListAvailableRooms(_checkIn.Value, _checkOut.Value);
            _room.DisplayMember = nameof(Room.DisplayText);
            _room.ValueMember = nameof(Room.RoomId);
            _room.DataSource = rooms;
            if (previous is not null) _room.SelectedValue = previous;
            if (rooms.Count == 0) Ui.SetError(_error, "No rooms are available for the selected dates.");
        });
        UpdateQuote();
    }

    private void UpdateQuote()
    {
        if (_room.SelectedItem is not Room room || _checkOut.Value.Date <= _checkIn.Value.Date)
        {
            _quote.Text = "";
            _create.Enabled = false;
            return;
        }
        var nights = ReservationRules.Nights(_checkIn.Value, _checkOut.Value);
        var total = ReservationRules.CalculateTotal(_checkIn.Value, _checkOut.Value, room.BasePrice);
        _quote.Text = $"{nights} night(s) × {room.BasePrice:N2} = {total:N2} {Currencies.Default}";
        _create.Enabled = true;
    }

    private void Create()
    {
        Ui.SetError(_error, null);
        if (_guest.SelectedValue is not int guestId) { Ui.SetError(_error, "Select a guest."); return; }
        if (_room.SelectedValue is not int roomId) { Ui.SetError(_error, "Select a room."); return; }

        try
        {
            var id = Services.Reservations.Create(CurrentUser, new NewReservation(guestId, roomId, _checkIn.Value, _checkOut.Value));
            _loading = true;
            _statusFilter.SelectedIndex = 0;
            _loading = false;
            _search.Clear();
            _searchDelay.Stop();
            LoadGrid(id);
            LoadAvailableRooms();
            Dialogs.Info(this, $"Reservation #{id} was created.");
        }
        catch (BusinessRuleException ex)
        {
            Ui.SetError(_error, ex.Message);
            LoadAvailableRooms();
        }
        catch (Exception ex) { ErrorHandler.Show(this, ex); }
    }

    private void CancelSelected()
    {
        if (Ui.Selected<ReservationListItem>(_grid) is not { } r) return;
        if (!Dialogs.Confirm(this, $"Cancel reservation #{r.ReservationId} for {r.GuestName} (room {r.RoomNumber}, " +
                                   $"{r.CheckInDate:dd.MM.yyyy} – {r.CheckOutDate:dd.MM.yyyy})?\n\nThe room becomes available again."))
            return;
        if (ErrorHandler.Try(this, () => Services.Reservations.Cancel(CurrentUser, r.ReservationId)))
        {
            LoadGrid(r.ReservationId);
            LoadAvailableRooms();
        }
    }
}
