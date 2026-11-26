namespace HotelReservationSystem.Pages;

internal sealed class PaymentsPage : PageBase
{
    private readonly DateTimePicker _from = Ui.DatePicker();
    private readonly DateTimePicker _to = Ui.DatePicker();
    private readonly CheckBox _outstandingOnly = new() { Text = "Outstanding balance only", AutoSize = true };
    private readonly TextBox _search = Ui.TextBox(100);
    private readonly DataGridView _grid = Ui.Grid("No reservations in the selected period.");

    private readonly Label _summary = new() { AutoSize = true, MaximumSize = new Size(300, 0), Margin = new Padding(0, 0, 0, 12) };
    private readonly TextBox _amount = Ui.TextBox(14);
    private readonly ComboBox _method = Ui.DropDown();
    private readonly Label _currency = Ui.Label(Currencies.Default, Theme.Bold);
    private readonly Label _error = Ui.ErrorLabel();
    private readonly Button _record;
    private readonly Button _fillBalance;
    private readonly DataGridView _history = Ui.Grid("No payments recorded.");
    private readonly Button _void;
    private readonly System.Windows.Forms.Timer _searchDelay = new() { Interval = 300 };

    public PaymentsPage(AppServices services, AuthenticatedUser user) : base(services, user)
    {
        _from.Value = DateTime.Today.AddDays(-30);
        _to.Value = DateTime.Today.AddDays(30);

        _record = Ui.Button("Record payment", ButtonKind.Primary, (_, _) => Record());
        _fillBalance = Ui.Button("Full balance", ButtonKind.Secondary, (_, _) =>
        {
            if (Ui.Selected<ReservationBalance>(_grid) is { } r) _amount.Text = r.Balance.ToString("0.00");
        });
        _void = Ui.Button("Void selected payment", ButtonKind.Danger, (_, _) => VoidPayment());
        _void.Visible = Can(Permission.VoidPayments);

        _method.DataSource = Enum.GetValues<PaymentMethod>();

        var amountRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
        amountRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        amountRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _amount.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _amount.AccessibleName = "Amount";
        _currency.Margin = new Padding(8, 4, 0, 0);
        amountRow.Controls.Add(_amount, 0, 0);
        amountRow.Controls.Add(_currency, 1, 0);

        var form = Ui.FieldStack(
            ("", EditorTitle("Record payment")),
            ("", _summary),
            ("Amount *", amountRow),
            ("Method", _method));
        form.Controls.Add(_error);
        var payButtons = Ui.ButtonRow(_record, _fillBalance);
        payButtons.WrapContents = false; // both fit; avoids a wrapped (taller) measurement
        form.Controls.Add(payButtons);

        _history.Columns.AddRange(
            Ui.Column(nameof(Payment.PaidAt), "Date", 120, "dd.MM.yyyy HH:mm"),
            Ui.Column(nameof(Payment.Method), "Method", 70),
            Ui.MoneyColumn(nameof(Payment.Amount), "Amount", 90));
        _history.RowTemplate.Height = 28;

        var editor = Ui.Card(new Padding(20, 18, 20, 18));
        var editorLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Surface };
        editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.Dock = DockStyle.Fill;
        editorLayout.Controls.Add(form, 0, 0);
        var historyTitle = Ui.Label("Payment history", Theme.Bold, Theme.MutedText);
        historyTitle.Margin = new Padding(0, 14, 0, 6);
        editorLayout.Controls.Add(historyTitle, 0, 1);
        var historyCard = GridCard(_history);
        historyCard.MinimumSize = new Size(0, 120);
        editorLayout.Controls.Add(historyCard, 0, 2);
        var voidRow = Ui.ButtonRow(_void);
        editorLayout.Controls.Add(voidRow, 0, 3);
        editor.Controls.Add(editorLayout);

        _grid.Columns.AddRange(
            Ui.Column(nameof(ReservationBalance.ReservationId), "No.", 45, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(ReservationBalance.GuestName), "Guest", 140),
            Ui.Column(nameof(ReservationBalance.RoomNumber), "Room", 50, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.DateColumn(nameof(ReservationBalance.CheckInDate), "Check-in", 75),
            Ui.DateColumn(nameof(ReservationBalance.CheckOutDate), "Check-out", 75),
            Ui.Column(nameof(ReservationBalance.Status), "Status", 75, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.MoneyColumn(nameof(ReservationBalance.TotalPrice), "Total"),
            Ui.MoneyColumn(nameof(ReservationBalance.PaidAmount), "Paid"),
            Ui.MoneyColumn(nameof(ReservationBalance.Balance), "Balance"),
            Ui.Column(nameof(ReservationBalance.Currency), "Cur.", 40, align: DataGridViewContentAlignment.MiddleCenter));
        Ui.TintRowsByStatus(_grid, o => (o as ReservationBalance)?.Status);
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.CellStyle is null || _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ReservationBalance.Balance)) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is ReservationBalance { Balance: > 0, Status: not ReservationStatus.Cancelled })
            {
                e.CellStyle.ForeColor = Theme.Danger;
                e.CellStyle.Font = Theme.Bold;
            }
        };
        _grid.AccessibleName = "Reservations";
        _history.AccessibleName = "Payment history";
        _grid.SelectionChanged += (_, _) => ShowSelected();

        _search.Width = 220;
        _search.PlaceholderText = "Search guest, room or no.…";
        _search.AccessibleName = "Search";
        _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); RefreshData(); };
        _outstandingOnly.CheckedChanged += (_, _) => RefreshData();
        var refresh = Ui.Button("Refresh", ButtonKind.Secondary, (_, _) => RefreshData());

        var toolbar = Toolbar(Ui.Label("Stay from"), _from, Ui.Label("to"), _to, refresh, _outstandingOnly, _search);
        Controls.Add(EditorLayout(editor, ToolbarAndGrid(toolbar, _grid), 360));
        ShowSelected();
    }

    public override string Title => "Payments";
    public override string Subtitle => "Track payments and outstanding balances. Each reservation is paid in its own currency.";

    public override void RefreshData() => RefreshData(null);

    private void RefreshData(int? select) => ErrorHandler.Try(this, () =>
    {
        Services.Reservations.RefreshStatuses();
        var rows = Services.Payments.ListBalances(_from.Value, _to.Value, _outstandingOnly.Checked, _search.Text);
        Ui.Bind(_grid, rows, r => r.ReservationId, select);
        ShowSelected();
    });

    private void ShowSelected()
    {
        Ui.SetError(_error, null);
        if (Ui.Selected<ReservationBalance>(_grid) is not { } r)
        {
            _summary.Text = "Select a reservation from the list.";
            _summary.ForeColor = Theme.MutedText;
            _record.Enabled = _fillBalance.Enabled = _amount.Enabled = _method.Enabled = false;
            _history.DataSource = null;
            _void.Enabled = false;
            return;
        }

        _summary.ForeColor = Theme.Text;
        _summary.Text = $"#{r.ReservationId} · {r.GuestName} · Room {r.RoomNumber}\n" +
                        $"Total {r.TotalPrice:N2}  ·  Paid {r.PaidAmount:N2}\n" +
                        $"Balance {r.Balance:N2} {r.Currency}";
        _currency.Text = r.Currency;
        var canPay = r.Status != ReservationStatus.Cancelled && r.Balance > 0;
        _record.Enabled = _fillBalance.Enabled = _amount.Enabled = _method.Enabled = canPay;
        if (!canPay) _amount.Clear();

        ErrorHandler.Try(this, () => Ui.Bind(_history, Services.Payments.ListPayments(r.ReservationId), p => p.PaymentId));
        _void.Enabled = _history.Rows.Count > 0;
    }

    private void Record()
    {
        Ui.SetError(_error, null);
        if (Ui.Selected<ReservationBalance>(_grid) is not { } r) return;
        if (!Ui.TryParseMoney(_amount.Text, out var amount)) { Ui.SetError(_error, "Enter a valid amount, e.g. 1500.00"); return; }
        var method = (PaymentMethod)(_method.SelectedItem ?? PaymentMethod.Cash);

        if (!Dialogs.Confirm(this, $"Record a {method.ToString().ToLowerInvariant()} payment of {amount:N2} {r.Currency} for reservation #{r.ReservationId}?"))
            return;
        try
        {
            Services.Payments.AddPayment(CurrentUser, r.ReservationId, amount, method);
            _amount.Clear();
            RefreshData(r.ReservationId);
        }
        catch (BusinessRuleException ex) { Ui.SetError(_error, ex.Message); }
        catch (Exception ex) { ErrorHandler.Show(this, ex); }
    }

    private void VoidPayment()
    {
        if (Ui.Selected<ReservationBalance>(_grid) is not { } r) return;
        var payment = Ui.Selected<Payment>(_history) ?? (_history.CurrentRow?.DataBoundItem as Payment);
        if (payment is null) { Dialogs.Info(this, "Select a payment in the history list first."); return; }
        if (!Dialogs.Confirm(this, $"Void the payment of {payment.Amount:N2} {payment.Currency} from {payment.PaidAt:dd.MM.yyyy HH:mm}?\n\nUse this only for payments recorded by mistake."))
            return;
        if (ErrorHandler.Try(this, () => Services.Payments.VoidPayment(CurrentUser, payment.PaymentId)))
            RefreshData(r.ReservationId);
    }
}
