using System.Reflection;

namespace HotelReservationSystem.Common;

internal enum ButtonKind { Primary, Secondary, Danger }

/// <summary>Factory helpers so every screen uses the same controls, spacing and styles.</summary>
internal static class Ui
{
    public static void InitForm(Form form, string title, int width, int height)
    {
        form.AutoScaleDimensions = new SizeF(96f, 96f);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Text = title;
        form.Font = Theme.Base;
        form.BackColor = Theme.Background;
        form.ForeColor = Theme.Text;
        form.ClientSize = new Size(width, height);
        form.StartPosition = FormStartPosition.CenterScreen;
    }

    public static void InitDialog(Form form, string title, int width, int height)
    {
        InitForm(form, title, width, height);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
        form.BackColor = Theme.Surface;
    }

    // ---------- Buttons ----------

    public static Button Button(string text, ButtonKind kind = ButtonKind.Secondary, EventHandler? onClick = null)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(96, 34),
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 0, 8, 8),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = Theme.Bold,
            UseVisualStyleBackColor = false
        };
        ApplyButtonKind(b, kind);
        if (onClick is not null) b.Click += onClick;
        return b;
    }

    public static void ApplyButtonKind(Button b, ButtonKind kind)
    {
        switch (kind)
        {
            case ButtonKind.Primary:
                b.BackColor = Theme.Primary;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = Theme.PrimaryDark;
                b.FlatAppearance.MouseDownBackColor = Theme.PrimaryDark;
                break;
            case ButtonKind.Danger:
                b.BackColor = Theme.Surface;
                b.ForeColor = Theme.Danger;
                b.FlatAppearance.BorderColor = Theme.Danger;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Theme.DangerLight;
                b.FlatAppearance.MouseDownBackColor = Theme.DangerLight;
                break;
            default:
                b.BackColor = Theme.Surface;
                b.ForeColor = Theme.Text;
                b.FlatAppearance.BorderColor = Theme.Border;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Theme.Background;
                b.FlatAppearance.MouseDownBackColor = Theme.Border;
                break;
        }
    }

    public static FlowLayoutPanel ButtonRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 0),
            Padding = Padding.Empty
        };
        row.Controls.AddRange(controls);
        return row;
    }

    // ---------- Text ----------

    public static Label Label(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Font = font ?? Theme.Base,
        ForeColor = color ?? Theme.Text,
        Margin = new Padding(0, 0, 0, 4)
    };

    public static Label Muted(string text) => Label(text, Theme.Small, Theme.MutedText);

    /// <summary>A red, wrapping label for inline validation messages. Hidden while empty.</summary>
    public static Label ErrorLabel() => new()
    {
        AutoSize = true,
        MaximumSize = new Size(560, 0),
        ForeColor = Theme.Danger,
        Font = Theme.Base,
        Visible = false,
        Margin = new Padding(0, 8, 0, 0)
    };

    public static void SetError(Label label, string? message)
    {
        label.Text = message ?? "";
        label.Visible = !string.IsNullOrEmpty(message);
    }

    // ---------- Inputs ----------

    public static TextBox TextBox(int maxLength = 100) => new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        MaxLength = maxLength,
        Font = Theme.Base,
        Margin = new Padding(0, 0, 0, 10)
    };

    public static ComboBox DropDown() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.System,
        Font = Theme.Base,
        Margin = new Padding(0, 0, 0, 10)
    };

    public static DateTimePicker DatePicker(int width = 130) => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd.MM.yyyy",
        Width = width,
        Font = Theme.Base,
        Margin = new Padding(0, 0, 0, 10)
    };

    /// <summary>Single-column stack of "label above input" fields that stretch to the available width.</summary>
    public static TableLayoutPanel FieldStack(params (string Label, Control Input)[] fields)
    {
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (label, input) in fields)
        {
            if (!string.IsNullOrEmpty(label))
            {
                stack.Controls.Add(Label(label, Theme.Bold, Theme.MutedText));
                // Screen readers (and UI automation) identify the input by its visible label.
                input.AccessibleName ??= label.TrimEnd(' ', '*');
            }
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            stack.Controls.Add(input);
        }
        return stack;
    }

    // ---------- Containers ----------

    /// <summary>White panel with a 1px border used for editor panels and cards.</summary>
    public static Panel Card(Padding? padding = null)
    {
        var p = new Panel
        {
            BackColor = Theme.Surface,
            Padding = padding ?? new Padding(16),
            Margin = new Padding(0)
        };
        p.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        p.Resize += (_, _) => p.Invalidate();
        return p;
    }

    /// <summary>A KPI tile: caption, big value and a muted sub-line.</summary>
    public static (Panel Card, Label Value, Label Sub) KpiCard(string caption)
    {
        var card = Card(new Padding(16, 12, 16, 12));
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 12, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme.Surface };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var cap = Label(caption.ToUpperInvariant(), Theme.Small, Theme.MutedText);
        var value = new Label
        {
            Text = "–",
            Font = Theme.Kpi,
            ForeColor = Theme.Text,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        var sub = new Label
        {
            Text = "",
            Font = Theme.Small,
            ForeColor = Theme.MutedText,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 34,
            AutoEllipsis = true
        };
        layout.Controls.Add(cap, 0, 0);
        layout.Controls.Add(value, 0, 1);
        layout.Controls.Add(sub, 0, 2);
        card.Controls.Add(layout);
        return (card, value, sub);
    }

    // ---------- Grids ----------

    public static DataGridView Grid(string emptyText = "No records to display.")
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToOrderColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Theme.Surface,
            GridColor = Theme.Border,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 38,
            EnableHeadersVisualStyles = false,
            StandardTab = true
        };
        g.RowTemplate.Height = 32;
        g.DefaultCellStyle.Font = Theme.Base;
        g.DefaultCellStyle.ForeColor = Theme.Text;
        g.DefaultCellStyle.BackColor = Theme.Surface;
        g.DefaultCellStyle.SelectionBackColor = Theme.PrimaryLight;
        g.DefaultCellStyle.SelectionForeColor = Theme.Text;
        g.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = Theme.GridAlternate;
        g.ColumnHeadersDefaultCellStyle.BackColor = Theme.GridHeader;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.MutedText;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.GridHeader;
        g.ColumnHeadersDefaultCellStyle.Font = Theme.Bold;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

        EnableDoubleBuffering(g);

        // Empty state message drawn in the middle of the grid.
        g.Paint += (_, e) =>
        {
            if (g.Rows.Count > 0 || g.Columns.Count == 0) return;
            var area = new Rectangle(0, g.ColumnHeadersHeight, g.Width, Math.Max(60, g.Height - g.ColumnHeadersHeight));
            TextRenderer.DrawText(e.Graphics, emptyText, Theme.Base, area, Theme.MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        };
        g.DataError += (_, e) => e.ThrowException = false;
        return g;
    }

    public static DataGridViewTextBoxColumn Column(string property, string header, float weight = 100,
        string? format = null, DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
    {
        var c = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            Name = property,
            HeaderText = header,
            FillWeight = weight,
            // Never narrower than the header or ~80% of the column's share; when the window is too
            // narrow the grid scrolls horizontally instead of truncating every value to "…".
            MinimumWidth = Math.Max(TextRenderer.MeasureText(header, Theme.Bold).Width + 20, (int)(weight * 0.8f)),
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        c.DefaultCellStyle.Alignment = align;
        c.HeaderCell.Style.Alignment = align;
        if (format is not null) c.DefaultCellStyle.Format = format;
        return c;
    }

    public static DataGridViewTextBoxColumn DateColumn(string property, string header, float weight = 80) =>
        AtLeast(Column(property, header, weight, "dd.MM.yyyy", DataGridViewContentAlignment.MiddleCenter), "00.00.0000");

    public static DataGridViewTextBoxColumn MoneyColumn(string property, string header, float weight = 85) =>
        AtLeast(Column(property, header, weight, "N2", DataGridViewContentAlignment.MiddleRight), "000,000.00");

    private static DataGridViewTextBoxColumn AtLeast(DataGridViewTextBoxColumn c, string sample)
    {
        c.MinimumWidth = Math.Max(c.MinimumWidth, TextRenderer.MeasureText(sample, Theme.Base).Width + 24);
        return c;
    }

    public static DataGridViewCheckBoxColumn CheckColumn(string property, string header, float weight = 55)
    {
        var c = new DataGridViewCheckBoxColumn
        {
            DataPropertyName = property, Name = property, HeaderText = header, FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        return c;
    }

    /// <summary>
    /// Binds items to a grid. Selects the row with key <paramref name="select"/>, or keeps the
    /// currently selected row when possible.
    /// </summary>
    public static void Bind<T>(DataGridView grid, IReadOnlyList<T> items, Func<T, int>? key = null, int? select = null) where T : class
    {
        var selectedKey = select ?? (key is not null && Selected<T>(grid) is { } current ? key(current) : (int?)null);

        // While binding, WinForms temporarily selects the first row. Those intermediate selection
        // events are suppressed so an editor never shows a different record than the highlighted row.
        BindingGrids.Add(grid);
        try
        {
            grid.DataSource = items.ToList();
            grid.ClearSelection();
            grid.CurrentCell = null;
            if (selectedKey is not null && key is not null)
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.DataBoundItem is T item && key(item) == selectedKey)
                    {
                        grid.CurrentCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                        row.Selected = true;
                        break;
                    }
                }
            }
        }
        finally
        {
            BindingGrids.Remove(grid);
        }

        if (SelectionHandlers.TryGetValue(grid, out var notify)) notify();
    }

    private static readonly HashSet<DataGridView> BindingGrids = [];
    private static readonly Dictionary<DataGridView, Action> SelectionHandlers = [];

    /// <summary>
    /// Calls <paramref name="handler"/> with the selected item whenever the user selects a row,
    /// and once after each <see cref="Bind"/> if a row is selected.
    /// </summary>
    public static void OnSelected<T>(DataGridView grid, Action<T> handler) where T : class
    {
        void Notify() { if (Selected<T>(grid) is { } item) handler(item); }
        SelectionHandlers[grid] = Notify;
        grid.SelectionChanged += (_, _) => { if (!BindingGrids.Contains(grid)) Notify(); };
        grid.Disposed += (_, _) => SelectionHandlers.Remove(grid);
    }

    public static T? Selected<T>(DataGridView grid) where T : class =>
        grid.CurrentRow is { Selected: true, DataBoundItem: T item } ? item : null;

    /// <summary>Tints whole rows by reservation status.</summary>
    public static void TintRowsByStatus(DataGridView grid, Func<object, ReservationStatus?> statusOf)
    {
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.CellStyle is null) return;
            if (grid.Rows[e.RowIndex].DataBoundItem is not { } item || statusOf(item) is not { } status) return;
            e.CellStyle.BackColor = Theme.StatusTint(status);
            if (grid.Columns[e.ColumnIndex].DataPropertyName == "Status")
            {
                e.Value = Theme.StatusText(status);
                e.FormattingApplied = true;
            }
        };
    }

    public static void EnableDoubleBuffering(Control control) =>
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(control, true);

    // ---------- Number parsing ----------

    /// <summary>Accepts both "1234.50" and "1234,50" so input works with Turkish and English keyboards.</summary>
    public static bool TryParseMoney(string text, out decimal value)
    {
        text = text.Trim().Replace(" ", "");
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (text.Contains(',') && !text.Contains('.')) text = text.Replace(',', '.');
        else if (text.Contains(',') && text.Contains('.'))
        {
            // Treat the last separator as the decimal separator.
            var decimalSep = text.LastIndexOf(',') > text.LastIndexOf('.') ? ',' : '.';
            var thousands = decimalSep == ',' ? "." : ",";
            text = text.Replace(thousands, "").Replace(',', '.');
        }
        return decimal.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint, culture, out value);
    }
}
