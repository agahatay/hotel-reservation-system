namespace HotelReservationSystem.Forms;

/// <summary>
/// Collects a new password (and optionally the current one) and passes it to <c>save</c>.
/// Rule violations are shown inline and the dialog stays open.
/// </summary>
internal sealed class FrmPasswordDialog : Form
{
    private readonly TextBox _current = Ui.TextBox(128);
    private readonly TextBox _new = Ui.TextBox(128);
    private readonly TextBox _confirm = Ui.TextBox(128);
    private readonly Label _error = Ui.ErrorLabel();
    private readonly Action<string, string> _save;
    private readonly bool _requireCurrent;

    public FrmPasswordDialog(string title, string intro, bool requireCurrent, Action<string, string> save)
    {
        _save = save;
        _requireCurrent = requireCurrent;
        Ui.InitDialog(this, title, 440, requireCurrent ? 430 : 360);
        Padding = new Padding(28);

        foreach (var t in new[] { _current, _new, _confirm }) t.UseSystemPasswordChar = true;

        var ok = Ui.Button("Save password", ButtonKind.Primary, (_, _) => Submit());
        var cancel = Ui.Button("Cancel", ButtonKind.Secondary, (_, _) => DialogResult = DialogResult.Cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        var fields = new List<(string, Control)>
        {
            ("", Ui.Label(title, Theme.Section)),
            ("", new Label { Text = intro, AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = Theme.MutedText, Margin = new Padding(0, 0, 0, 14) })
        };
        if (requireCurrent) fields.Add(("Current password", _current));
        fields.Add(("New password", _new));
        fields.Add(("Confirm new password", _confirm));

        var stack = Ui.FieldStack([.. fields]);
        stack.Controls.Add(_error);
        stack.Controls.Add(Ui.ButtonRow(ok, cancel));
        Controls.Add(stack);
    }

    private void Submit()
    {
        Ui.SetError(_error, null);
        if (_new.Text != _confirm.Text)
        {
            Ui.SetError(_error, "The new passwords do not match.");
            return;
        }
        try
        {
            _save(_requireCurrent ? _current.Text : "", _new.Text);
            DialogResult = DialogResult.OK;
        }
        catch (BusinessRuleException ex)
        {
            Ui.SetError(_error, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Show(this, ex);
        }
    }
}
