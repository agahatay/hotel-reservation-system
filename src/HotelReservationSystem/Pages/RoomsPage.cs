namespace HotelReservationSystem.Pages;

/// <summary>Rooms and room types (administrators only).</summary>
internal sealed class RoomsPage : PageBase
{
    // Rooms
    private readonly DataGridView _roomGrid = Ui.Grid("No rooms yet.");
    private readonly TextBox _roomNumber = Ui.TextBox(10);
    private readonly ComboBox _roomType = Ui.DropDown();
    private readonly TextBox _floor = Ui.TextBox(4);
    private readonly CheckBox _active = new() { Text = "Active (can be reserved)", AutoSize = true, Checked = true, Margin = new Padding(0, 2, 0, 6) };
    private readonly Label _roomTitle = EditorTitle("New room");
    private readonly Label _roomError = Ui.ErrorLabel();
    private readonly Button _toggleActive;
    private readonly Button _deleteRoom;
    private int _editingRoomId;

    // Room types
    private readonly DataGridView _typeGrid = Ui.Grid("No room types yet.");
    private readonly TextBox _typeName = Ui.TextBox(50);
    private readonly NumericUpDown _capacity = new() { Minimum = 1, Maximum = 20, Value = 2, Margin = new Padding(0, 0, 0, 10) };
    private readonly TextBox _basePrice = Ui.TextBox(12);
    private readonly Label _typeTitle = EditorTitle("New room type");
    private readonly Label _typeError = Ui.ErrorLabel();
    private readonly Button _deleteType;
    private int _editingTypeId;

    public RoomsPage(AppServices services, AuthenticatedUser user) : base(services, user)
    {
        _toggleActive = Ui.Button("Deactivate", ButtonKind.Secondary, (_, _) => ToggleActive());
        _deleteRoom = Ui.Button("Delete", ButtonKind.Danger, (_, _) => DeleteRoom());
        _deleteType = Ui.Button("Delete", ButtonKind.Danger, (_, _) => DeleteType());

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 6) };
        tabs.TabPages.Add(BuildRoomsTab());
        tabs.TabPages.Add(BuildTypesTab());
        Controls.Add(tabs);

        ClearRoomEditor();
        ClearTypeEditor();
    }

    public override string Title => "Rooms";
    public override string Subtitle => "Manage rooms, room types and nightly prices.";

    public override void RefreshData() => ErrorHandler.Try(this, () =>
    {
        var types = Services.Rooms.ListRoomTypes();
        var selectedType = _roomType.SelectedValue;
        _roomType.DisplayMember = nameof(RoomType.TypeName);
        _roomType.ValueMember = nameof(RoomType.RoomTypeId);
        _roomType.DataSource = types;
        if (selectedType is not null) _roomType.SelectedValue = selectedType;

        Ui.Bind(_typeGrid, types, t => t.RoomTypeId);
        Ui.Bind(_roomGrid, Services.Rooms.ListRooms(), r => r.RoomId);
    });

    // ---------------- Rooms tab ----------------

    private TabPage BuildRoomsTab()
    {
        var save = Ui.Button("Save", ButtonKind.Primary, (_, _) => SaveRoom());
        var clear = Ui.Button("New room", ButtonKind.Secondary, (_, _) => ClearRoomEditor());

        var stack = Ui.FieldStack(
            ("", _roomTitle),
            ("Room number *", _roomNumber),
            ("Room type *", _roomType),
            ("Floor", _floor),
            ("", _active));
        stack.Controls.Add(_roomError);
        stack.Controls.Add(Ui.ButtonRow(save, clear));
        stack.Controls.Add(Ui.ButtonRow(_toggleActive, _deleteRoom));
        stack.Controls.Add(Ui.Muted("Rooms with reservation history are deactivated\ninstead of deleted."));

        _roomGrid.Columns.AddRange(
            Ui.Column(nameof(Room.RoomNumber), "Room", 60, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(Room.TypeName), "Type", 170),
            Ui.Column(nameof(Room.Capacity), "Guests", 55, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.Column(nameof(Room.Floor), "Floor", 50, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.MoneyColumn(nameof(Room.BasePrice), "Price / night (TRY)", 90),
            Ui.CheckColumn(nameof(Room.IsActive), "Active"));
        Ui.OnSelected<Room>(_roomGrid, LoadRoom);
        _roomGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.CellStyle is not null && _roomGrid.Rows[e.RowIndex].DataBoundItem is Room { IsActive: false })
                e.CellStyle.ForeColor = Theme.MutedText;
        };

        var page = new TabPage("Rooms") { BackColor = Theme.Background, Padding = new Padding(0, 14, 0, 0) };
        page.Controls.Add(EditorLayout(EditorCard(stack), GridCard(_roomGrid)));
        return page;
    }

    private void LoadRoom(Room r)
    {
        _editingRoomId = r.RoomId;
        _roomTitle.Text = $"Edit room {r.RoomNumber}";
        _roomNumber.Text = r.RoomNumber;
        _roomType.SelectedValue = r.RoomTypeId;
        _floor.Text = r.Floor?.ToString() ?? "";
        _active.Checked = r.IsActive;
        _toggleActive.Text = r.IsActive ? "Deactivate" : "Activate";
        _toggleActive.Enabled = _deleteRoom.Enabled = true;
        Ui.SetError(_roomError, null);
    }

    private void ClearRoomEditor()
    {
        _editingRoomId = 0;
        _roomTitle.Text = "New room";
        _roomNumber.Clear();
        _floor.Clear();
        _active.Checked = true;
        _toggleActive.Text = "Deactivate";
        _toggleActive.Enabled = _deleteRoom.Enabled = false;
        Ui.SetError(_roomError, null);
        _roomGrid.ClearSelection();
    }

    private void SaveRoom()
    {
        Ui.SetError(_roomError, null);
        int? floor = null;
        if (!string.IsNullOrWhiteSpace(_floor.Text))
        {
            if (!int.TryParse(_floor.Text.Trim(), out var f)) { Ui.SetError(_roomError, "Floor must be a whole number."); return; }
            floor = f;
        }

        var room = new Room
        {
            RoomId = _editingRoomId, RoomNumber = _roomNumber.Text,
            RoomTypeId = _roomType.SelectedValue is int typeId ? typeId : 0,
            Floor = floor, IsActive = _active.Checked
        };
        try
        {
            var id = Services.Rooms.SaveRoom(CurrentUser, room);
            Ui.Bind(_roomGrid, Services.Rooms.ListRooms(), r => r.RoomId, id);
        }
        catch (BusinessRuleException ex) { Ui.SetError(_roomError, ex.Message); }
        catch (Exception ex) { ErrorHandler.Show(this, ex); }
    }

    private void ToggleActive()
    {
        if (Ui.Selected<Room>(_roomGrid) is not { } room) return;
        var activate = !room.IsActive;
        if (!activate && !Dialogs.Confirm(this, $"Deactivate room {room.RoomNumber}? It will no longer be offered for new reservations.")) return;
        if (ErrorHandler.Try(this, () => Services.Rooms.SetActive(CurrentUser, room.RoomId, activate)))
            ErrorHandler.Try(this, () => Ui.Bind(_roomGrid, Services.Rooms.ListRooms(), r => r.RoomId, room.RoomId));
    }

    private void DeleteRoom()
    {
        if (Ui.Selected<Room>(_roomGrid) is not { } room) return;
        if (!Dialogs.Confirm(this, $"Delete room {room.RoomNumber}?\n\nIf the room has reservation history it will be deactivated instead.")) return;
        var deleted = false;
        if (!ErrorHandler.Try(this, () => deleted = Services.Rooms.DeleteOrDeactivate(CurrentUser, room.RoomId))) return;
        Dialogs.Info(this, deleted ? $"Room {room.RoomNumber} was deleted." : $"Room {room.RoomNumber} has reservation history and was deactivated.");
        ClearRoomEditor();
        RefreshData();
    }

    // ---------------- Room types tab ----------------

    private TabPage BuildTypesTab()
    {
        var save = Ui.Button("Save", ButtonKind.Primary, (_, _) => SaveType());
        var clear = Ui.Button("New type", ButtonKind.Secondary, (_, _) => ClearTypeEditor());

        var stack = Ui.FieldStack(
            ("", _typeTitle),
            ("Name *", _typeName),
            ("Capacity (guests) *", _capacity),
            ("Base price per night (TRY) *", _basePrice));
        stack.Controls.Add(_typeError);
        stack.Controls.Add(Ui.ButtonRow(save, clear, _deleteType));
        stack.Controls.Add(Ui.Muted("Price changes apply to new reservations only.\nExisting reservations keep their booked price."));

        _typeGrid.Columns.AddRange(
            Ui.Column(nameof(RoomType.TypeName), "Room type", 220),
            Ui.Column(nameof(RoomType.Capacity), "Capacity", 70, align: DataGridViewContentAlignment.MiddleCenter),
            Ui.MoneyColumn(nameof(RoomType.BasePrice), "Price / night (TRY)", 100));
        Ui.OnSelected<RoomType>(_typeGrid, LoadType);

        var page = new TabPage("Room types") { BackColor = Theme.Background, Padding = new Padding(0, 14, 0, 0) };
        page.Controls.Add(EditorLayout(EditorCard(stack), GridCard(_typeGrid)));
        return page;
    }

    private void LoadType(RoomType t)
    {
        _editingTypeId = t.RoomTypeId;
        _typeTitle.Text = "Edit room type";
        _typeName.Text = t.TypeName;
        _capacity.Value = Math.Clamp(t.Capacity, 1, 20);
        _basePrice.Text = t.BasePrice.ToString("0.00");
        _deleteType.Enabled = true;
        Ui.SetError(_typeError, null);
    }

    private void ClearTypeEditor()
    {
        _editingTypeId = 0;
        _typeTitle.Text = "New room type";
        _typeName.Clear();
        _capacity.Value = 2;
        _basePrice.Clear();
        _deleteType.Enabled = false;
        Ui.SetError(_typeError, null);
        _typeGrid.ClearSelection();
    }

    private void SaveType()
    {
        Ui.SetError(_typeError, null);
        if (!Ui.TryParseMoney(_basePrice.Text, out var price)) { Ui.SetError(_typeError, "Enter a valid base price, e.g. 3800.00"); return; }
        var type = new RoomType { RoomTypeId = _editingTypeId, TypeName = _typeName.Text, Capacity = (int)_capacity.Value, BasePrice = price };
        try
        {
            var id = Services.Rooms.SaveRoomType(CurrentUser, type);
            RefreshData();
            Ui.Bind(_typeGrid, Services.Rooms.ListRoomTypes(), t => t.RoomTypeId, id);
        }
        catch (BusinessRuleException ex) { Ui.SetError(_typeError, ex.Message); }
        catch (Exception ex) { ErrorHandler.Show(this, ex); }
    }

    private void DeleteType()
    {
        if (_editingTypeId == 0) return;
        if (!Dialogs.Confirm(this, $"Delete room type \"{_typeName.Text}\"?")) return;
        if (ErrorHandler.Try(this, () => Services.Rooms.DeleteRoomType(CurrentUser, _editingTypeId)))
        {
            ClearTypeEditor();
            RefreshData();
        }
    }
}
