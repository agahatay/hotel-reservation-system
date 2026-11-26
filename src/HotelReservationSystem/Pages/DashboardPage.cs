namespace HotelReservationSystem.Pages;

internal sealed class DashboardPage : PageBase
{
    private readonly DateTimePicker _from = Ui.DatePicker();
    private readonly DateTimePicker _to = Ui.DatePicker();
    private readonly Label _total, _totalSub, _active, _activeSub, _completed, _completedSub, _cancelled, _cancelledSub;
    private readonly Label _occupancy, _occupancySub, _income, _incomeSub, _topGuest, _topGuestSub;
    private readonly DataGridView _topRooms = Ui.Grid("No reservations in this period.");
    private readonly DataGridView _reservations = Ui.Grid("No reservations in this period.");

    public DashboardPage(AppServices services, AuthenticatedUser user) : base(services, user)
    {
        SetLast30Days(refresh: false);

        var refresh = Ui.Button("Refresh", ButtonKind.Primary, (_, _) => RefreshData());
        var thisMonth = Ui.Button("This month", ButtonKind.Secondary, (_, _) =>
        {
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            SetRange(start, start.AddMonths(1).AddDays(-1));
        });
        var last30 = Ui.Button("Last 30 days", ButtonKind.Secondary, (_, _) => SetLast30Days(refresh: true));
        var toolbar = Toolbar(Ui.Label("From"), _from, Ui.Label("to"), _to, refresh, thisMonth, last30);

        Panel c1, c2, c3, c4, c5, c6, c7;
        (c1, _total, _totalSub) = Ui.KpiCard("Reservations");
        (c2, _active, _activeSub) = Ui.KpiCard("Active");
        (c3, _completed, _completedSub) = Ui.KpiCard("Completed");
        (c4, _cancelled, _cancelledSub) = Ui.KpiCard("Cancelled");
        (c5, _occupancy, _occupancySub) = Ui.KpiCard("Occupancy");
        (c6, _income, _incomeSub) = Ui.KpiCard("Income received");
        (c7, _topGuest, _topGuestSub) = Ui.KpiCard("Top guest");
        _topGuest.Font = Theme.Section;

        var row1 = CardRow(c1, c2, c3, c4);
        var row2 = CardRow(c5, c6, c7);

        _topRooms.Columns.AddRange(
            Ui.Column(nameof(RoomUsage.RoomNumber), "Room", 60, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(RoomUsage.Reservations), "Reservations", 80, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(RoomUsage.Nights), "Nights", 60, align: DataGridViewContentAlignment.MiddleCenter));

        _reservations.Columns.AddRange(
            Ui.Column(nameof(ReservationListItem.ReservationId), "No.", 45, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(ReservationListItem.GuestName), "Guest", 150),
            Ui.Column(nameof(ReservationListItem.RoomNumber), "Room", 55, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.DateColumn(nameof(ReservationListItem.CheckInDate), "Check-in"),
            Ui.DateColumn(nameof(ReservationListItem.CheckOutDate), "Check-out"),
            Ui.MoneyColumn(nameof(ReservationListItem.TotalPrice), "Total"),
            Ui.Column(nameof(ReservationListItem.Status), "Status", 80, align: DataGridViewContentAlignment.MiddleCenter));
        Ui.TintRowsByStatus(_reservations, o => (o as ReservationListItem)?.Status);

        var grids = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 14, 0, 0) };
        grids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        grids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        var left = Section("Top rooms", _topRooms);
        left.Margin = new Padding(0, 0, 14, 0);
        grids.Controls.Add(left, 0, 0);
        grids.Controls.Add(Section("Reservations checking in during the period", _reservations), 1, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(row1, 0, 1);
        root.Controls.Add(row2, 0, 2);
        root.Controls.Add(grids, 0, 3);
        Controls.Add(root);
    }

    public override string Title => "Dashboard";
    public override string Subtitle => "Reservations, occupancy and income for the selected period.";

    public override void RefreshData()
    {
        if (_to.Value.Date < _from.Value.Date)
        {
            Dialogs.Warning(this, "The end date must not be before the start date.");
            return;
        }

        ErrorHandler.Try(this, () =>
        {
            Services.Reservations.RefreshStatuses();
            var d = Services.Reports.GetDashboard(_from.Value, _to.Value);

            _total.Text = d.TotalReservations.ToString("N0");
            _totalSub.Text = "check-ins in the period";
            _active.Text = d.ActiveReservations.ToString("N0");
            _activeSub.Text = "booked or checked in";
            _completed.Text = d.CompletedReservations.ToString("N0");
            _completedSub.Text = "stays finished";
            _cancelled.Text = d.CancelledReservations.ToString("N0");
            _cancelledSub.Text = "cancelled bookings";

            _occupancy.Text = d.Occupancy.Rate.ToString("P1");
            _occupancySub.Text = $"{d.Occupancy.OccupiedRoomNights:N0} of {d.Occupancy.AvailableRoomNights:N0} room-nights\n({d.Occupancy.RoomCount} active rooms)";

            // Never add different currencies together: one line per currency.
            if (d.IncomeByCurrency.Count == 0)
            {
                _income.Text = $"0.00 {Currencies.Default}";
                _incomeSub.Text = "no payments in the period";
            }
            else
            {
                var main = d.IncomeByCurrency.FirstOrDefault(c => c.Currency == Currencies.Default) ?? d.IncomeByCurrency[0];
                _income.Text = $"{main.Amount:N2} {main.Currency}";
                var others = d.IncomeByCurrency.Where(c => c != main).Select(c => $"{c.Amount:N2} {c.Currency}").ToList();
                _incomeSub.Text = others.Count == 0 ? "payments received in the period" : "also: " + string.Join("  ·  ", others);
            }

            _topGuest.Text = d.TopGuest ?? "–";
            _topGuestSub.Text = d.TopGuest is null ? "no reservations" : $"{d.TopGuestReservations} reservation(s) in the period";

            Ui.Bind(_topRooms, d.TopRooms);
            Ui.Bind(_reservations, d.Reservations);
        });
    }

    private void SetLast30Days(bool refresh)
    {
        _from.Value = DateTime.Today.AddDays(-29);
        _to.Value = DateTime.Today;
        if (refresh) RefreshData();
    }

    private void SetRange(DateTime from, DateTime to)
    {
        _from.Value = from;
        _to.Value = to;
        RefreshData();
    }

    private static TableLayoutPanel CardRow(params Panel[] cards)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = cards.Length, RowCount = 1, Margin = new Padding(0, 0, 0, 14) };
        for (var i = 0; i < cards.Length; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cards.Length));
            if (i == cards.Length - 1) cards[i].Margin = Padding.Empty;
            row.Controls.Add(cards[i], i, 0);
        }
        return row;
    }

    private static Control Section(string title, DataGridView grid)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = Ui.Label(title, Theme.Section);
        label.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(GridCard(grid), 0, 1);
        return layout;
    }
}
