namespace HotelReservationSystem.Pages;

/// <summary>A screen hosted inside the main window.</summary>
internal abstract class PageBase : UserControl
{
    protected readonly AppServices Services;
    protected readonly AuthenticatedUser CurrentUser;

    protected PageBase(AppServices services, AuthenticatedUser user)
    {
        Services = services;
        CurrentUser = user;
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        Font = Theme.Base;
        Margin = Padding.Empty;
    }

    public abstract string Title { get; }
    public virtual string Subtitle => "";

    /// <summary>Reloads the page's data. Called every time the page is shown.</summary>
    public abstract void RefreshData();

    protected bool Can(Permission permission) => RolePermissions.Has(CurrentUser.Role, permission);

    /// <summary>Editor card on the left (fixed width), main content on the right.</summary>
    protected static TableLayoutPanel EditorLayout(Control editor, Control main, int editorWidth = 340)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, editorWidth));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.Dock = DockStyle.Fill;
        editor.Margin = new Padding(0, 0, 16, 0);
        main.Dock = DockStyle.Fill;
        main.Margin = Padding.Empty;
        layout.Controls.Add(editor, 0, 0);
        layout.Controls.Add(main, 1, 0);
        return layout;
    }

    /// <summary>A toolbar row above a grid card.</summary>
    protected static TableLayoutPanel ToolbarAndGrid(Control toolbar, DataGridView grid)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.Dock = DockStyle.Fill;
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(GridCard(grid), 0, 1);
        return layout;
    }

    protected static Panel GridCard(DataGridView grid)
    {
        var card = Ui.Card(new Padding(1));
        card.Dock = DockStyle.Fill;
        card.Controls.Add(grid);
        return card;
    }

    protected static FlowLayoutPanel Toolbar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
            Margin = new Padding(0, 0, 0, 10), Padding = Padding.Empty
        };
        foreach (var c in controls)
        {
            if (c is Label) c.Margin = new Padding(0, 8, 6, 0);
            else if (c is not Button) c.Margin = new Padding(0, 4, 14, 0);
            bar.Controls.Add(c);
        }
        return bar;
    }

    /// <summary>Editor card whose content is a vertical stack (title, fields, messages, buttons).</summary>
    protected static Panel EditorCard(TableLayoutPanel stack)
    {
        var card = Ui.Card(new Padding(20, 18, 20, 18));
        var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Surface };
        scroller.Controls.Add(stack);
        card.Controls.Add(scroller);
        return card;
    }

    protected static Label EditorTitle(string text)
    {
        var l = Ui.Label(text, Theme.Section);
        l.Margin = new Padding(0, 0, 0, 14);
        return l;
    }
}
