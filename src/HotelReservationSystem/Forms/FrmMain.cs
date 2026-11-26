using HotelReservationSystem.Pages;

namespace HotelReservationSystem.Forms;

/// <summary>Application shell: sidebar navigation on the left, the selected page on the right.</summary>
internal sealed class FrmMain : Form
{
    private readonly AppServices _services;
    private readonly AuthenticatedUser _user;
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };
    private readonly Label _pageTitle = Ui.Label("", Theme.Title);
    private readonly Label _pageSubtitle = Ui.Muted("");
    private readonly List<(Button Button, PageBase Page)> _nav = [];
    private PageBase? _current;
    private bool _signOut;

    public FrmMain(AppServices services, AuthenticatedUser user)
    {
        _services = services;
        _user = user;

        Ui.InitForm(this, "Hotel Reservation System", 1320, 820);
        MinimumSize = new Size(1100, 680);
        WindowState = FormWindowState.Maximized;

        BuildUi();
        Shown += (_, _) => Navigate(_nav[0].Page);
    }

    public bool SignOutRequested => _signOut;

    private void BuildUi()
    {
        // Content area (added first so the docked sidebar takes its space from the left).
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(28, 20, 28, 20), BackColor = Theme.Background
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0, 0, 0, 14) };
        _pageTitle.Margin = Padding.Empty;
        header.Controls.Add(_pageTitle);
        header.Controls.Add(_pageSubtitle);
        content.Controls.Add(header, 0, 0);
        content.Controls.Add(_pageHost, 0, 1);
        Controls.Add(content);

        Controls.Add(BuildSidebar());
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Left, Width = 232, BackColor = Theme.Sidebar, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(0, 20, 0, 16)
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brand = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(22, 0, 16, 18) };
        brand.Controls.Add(new Label { Text = "Hotel Reservation", AutoSize = true, ForeColor = Color.White, Font = Theme.Brand });
        brand.Controls.Add(new Label { Text = "Management System", AutoSize = true, ForeColor = Theme.SidebarText, Font = Theme.Small });
        sidebar.Controls.Add(brand, 0, 0);

        var menu = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 0, 10, 0) };
        var pages = new (Permission Permission, Func<PageBase> Create)[]
        {
            (Permission.ViewDashboard, () => new DashboardPage(_services, _user)),
            (Permission.ManageReservations, () => new ReservationsPage(_services, _user)),
            (Permission.ManageGuests, () => new GuestsPage(_services, _user)),
            (Permission.TakePayments, () => new PaymentsPage(_services, _user)),
            (Permission.ViewDashboard, () => new OccupancyPage(_services, _user)),
            (Permission.ManageRooms, () => new RoomsPage(_services, _user)),
            (Permission.ManageUsers, () => new UsersPage(_services, _user))
        };

        // Pages are created up front so that the form's DPI scaling applies to all of them.
        foreach (var (permission, create) in pages)
        {
            if (!RolePermissions.Has(_user.Role, permission)) continue;
            var page = create();
            page.Visible = false;
            _pageHost.Controls.Add(page);

            var button = NavButton(page.Title);
            button.Click += (_, _) => Navigate(page);
            menu.Controls.Add(button);
            _nav.Add((button, page));
        }
        sidebar.Controls.Add(menu, 0, 1);

        var account = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(22, 12, 16, 0) };
        account.Controls.Add(new Label { Text = _user.Username, AutoSize = true, ForeColor = Color.White, Font = Theme.Bold });
        account.Controls.Add(new Label { Text = _user.Role.ToString(), AutoSize = true, ForeColor = Theme.SidebarText, Font = Theme.Small, Margin = new Padding(0, 0, 0, 10) });
        account.Controls.Add(SidebarLink("Change password", ChangePassword));
        account.Controls.Add(SidebarLink("Sign out", SignOut));
        sidebar.Controls.Add(account, 0, 2);

        return sidebar;
    }

    private static Button NavButton(string text)
    {
        var b = new Button
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Width = 212, Height = 42,
            Margin = new Padding(0, 0, 0, 4),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.SidebarText,
            BackColor = Theme.Sidebar,
            Font = Theme.Base,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.SidebarHover;
        b.FlatAppearance.MouseDownBackColor = Theme.SidebarActive;
        return b;
    }

    private static LinkLabel SidebarLink(string text, Action onClick)
    {
        var link = new LinkLabel
        {
            Text = text, AutoSize = true, Font = Theme.Small,
            LinkColor = Theme.SidebarText, ActiveLinkColor = Color.White, VisitedLinkColor = Theme.SidebarText,
            LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 0, 0, 6)
        };
        link.LinkClicked += (_, _) => onClick();
        return link;
    }

    private void Navigate(PageBase page)
    {
        if (_current == page) { page.RefreshData(); return; }

        foreach (var (button, p) in _nav)
        {
            var active = p == page;
            button.BackColor = active ? Theme.SidebarActive : Theme.Sidebar;
            button.ForeColor = active ? Color.White : Theme.SidebarText;
            button.Font = active ? Theme.Bold : Theme.Base;
        }

        _current?.Hide();
        _current = page;
        _pageTitle.Text = page.Title;
        _pageSubtitle.Text = page.Subtitle;
        page.Show();
        page.BringToFront();
        page.RefreshData();
    }

    private void ChangePassword()
    {
        using var dlg = new FrmPasswordDialog("Change password", "Enter your current password and choose a new one.",
            requireCurrent: true, save: (current, next) => _services.Auth.ChangeOwnPassword(_user, current, next));
        if (dlg.ShowDialog(this) == DialogResult.OK)
            Dialogs.Info(this, "Your password was changed.");
    }

    private void SignOut()
    {
        _signOut = true;
        Close();
    }
}
