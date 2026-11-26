namespace HotelReservationSystem.Pages;

/// <summary>Room × day occupancy calendar for one month.</summary>
internal sealed class OccupancyPage : PageBase
{
    private readonly DateTimePicker _month = new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "MM.yyyy", ShowUpDown = true, Width = 110, Font = Theme.Base
    };
    private readonly Label _summary = Ui.Label("", Theme.Bold);
    private readonly DataGridView _grid = Ui.Grid("No rooms to display.");
    private bool _loading;

    public OccupancyPage(AppServices services, AuthenticatedUser user) : base(services, user)
    {
        _month.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _month.ValueChanged += (_, _) => { if (!_loading) RefreshData(); };

        var prev = Ui.Button("◀", ButtonKind.Secondary, (_, _) => ShiftMonth(-1));
        var next = Ui.Button("▶", ButtonKind.Secondary, (_, _) => ShiftMonth(1));
        var today = Ui.Button("This month", ButtonKind.Secondary, (_, _) => SetMonth(DateTime.Today));
        prev.MinimumSize = next.MinimumSize = new Size(40, 34);

        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.ColumnHeadersHeight = 46;
        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DefaultCellStyle.SelectionBackColor = Theme.PrimaryLight;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.Surface;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _grid.RowTemplate.Height = 28;

        var legend = Toolbar(Swatch(Theme.Occupied), Ui.Muted("Occupied night"), Swatch(Theme.Weekend), Ui.Muted("Weekend"), Swatch(Theme.Today), Ui.Muted("Today"));
        var toolbar = Toolbar(prev, _month, next, today, _summary);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(legend, 0, 1);
        root.Controls.Add(GridCard(_grid), 0, 2);
        Controls.Add(root);
    }

    public override string Title => "Monthly occupancy";
    public override string Subtitle => "Which rooms are occupied on which nights. Check-out day is not counted as an occupied night.";

    public override void RefreshData() => ErrorHandler.Try(this, () =>
    {
        var data = Services.Reports.GetMonthlyOccupancy(_month.Value.Year, _month.Value.Month);
        BuildGrid(data);
    });

    private void BuildGrid(MonthlyOccupancy data)
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        _grid.Columns.Clear();

        var roomCol = Ui.Column("Room", "Room", 100);
        roomCol.Width = 70;
        roomCol.Frozen = true;
        roomCol.DefaultCellStyle.Font = Theme.Bold;
        roomCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.Columns.Add(roomCol);

        for (var day = 1; day <= data.DaysInMonth; day++)
        {
            var date = data.MonthStart.AddDays(day - 1);
            var weekday = date.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)[..2];
            var col = Ui.Column($"d{day}", $"{day}\n{weekday}", 30, align: DataGridViewContentAlignment.MiddleCenter);
            col.Width = 34;
            col.HeaderCell.Style.Font = Theme.Small;
            if (IsWeekend(date)) col.HeaderCell.Style.BackColor = Theme.Weekend;
            if (date == DateTime.Today) col.HeaderCell.Style.BackColor = Theme.Today;
            _grid.Columns.Add(col);
        }

        var nightsCol = Ui.Column("Nights", "Nights", 60, align: DataGridViewContentAlignment.MiddleCenter);
        nightsCol.Width = 64;
        nightsCol.DefaultCellStyle.Font = Theme.Bold;
        _grid.Columns.Add(nightsCol);

        var occupiedPerDay = new int[data.DaysInMonth + 1];
        var totalNights = 0;
        var activeRooms = 0;
        var anyInactive = false;

        foreach (var room in data.Rooms)
        {
            var row = new DataGridViewRow();
            row.CreateCells(_grid);
            row.Cells[0].Value = room.IsActive ? room.RoomNumber : $"{room.RoomNumber}*";
            row.Cells[0].ToolTipText = $"{room.TypeName}{(room.IsActive ? "" : " (inactive)")}";
            var days = data.OccupiedDays.TryGetValue(room.RoomId, out var set) ? set : new HashSet<int>();
            if (room.IsActive) activeRooms++; else anyInactive = true;

            for (var day = 1; day <= data.DaysInMonth; day++)
            {
                var date = data.MonthStart.AddDays(day - 1);
                var cell = row.Cells[day];
                if (days.Contains(day))
                {
                    cell.Style.BackColor = Theme.Occupied;
                    cell.Style.SelectionBackColor = Theme.Danger;
                    cell.ToolTipText = $"Room {room.RoomNumber} occupied on {date:dd.MM.yyyy}";
                    if (room.IsActive) occupiedPerDay[day]++;
                }
                else if (date == DateTime.Today) cell.Style.BackColor = Theme.Today;
                else if (IsWeekend(date)) cell.Style.BackColor = Theme.Weekend;
            }

            row.Cells[^1].Value = days.Count;
            if (room.IsActive) totalNights += days.Count;
            _grid.Rows.Add(row);
        }

        // Footer row: share of active rooms occupied each night.
        var footer = new DataGridViewRow();
        footer.CreateCells(_grid);
        footer.Cells[0].Value = "Occ. %";
        for (var day = 1; day <= data.DaysInMonth; day++)
        {
            footer.Cells[day].Value = activeRooms == 0 ? "–" : $"{100 * occupiedPerDay[day] / activeRooms}";
            footer.Cells[day].Style.Font = Theme.Small;
        }
        footer.DefaultCellStyle.BackColor = Theme.GridHeader;
        footer.DefaultCellStyle.ForeColor = Theme.MutedText;
        footer.Cells[^1].Value = totalNights;
        _grid.Rows.Add(footer);

        _grid.ResumeLayout();
        _grid.ClearSelection();

        var available = activeRooms * data.DaysInMonth;
        _summary.Text = available == 0
            ? "No active rooms."
            : $"{data.MonthStart:MMMM yyyy}: {(decimal)totalNights / available:P1} occupancy  ·  {totalNights:N0} of {available:N0} room-nights{(anyInactive ? "  ·  * inactive room" : "")}";
    }

    private static bool IsWeekend(DateTime d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private void ShiftMonth(int months) => SetMonth(_month.Value.AddMonths(months));

    private void SetMonth(DateTime date)
    {
        _loading = true;
        _month.Value = new DateTime(date.Year, date.Month, 1);
        _loading = false;
        RefreshData();
    }

    private static Panel Swatch(Color color)
    {
        var p = new Panel { Size = new Size(14, 14), BackColor = color, Margin = new Padding(0, 10, 6, 0) };
        p.Paint += (_, e) => { using var pen = new Pen(Theme.Border); e.Graphics.DrawRectangle(pen, 0, 0, 13, 13); };
        return p;
    }
}
