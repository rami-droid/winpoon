using System.Drawing;
using System.Windows.Forms;

internal sealed class KeybindEditorForm : Form
{
    private readonly Dictionary<KeybindAction, Keybind> _pendingBindings;
    private readonly Dictionary<KeybindAction, Button> _bindingButtons = new();
    private Button? _capturingButton;
    private KeybindAction _capturingAction;

    public KeybindEditorForm()
    {
        _pendingBindings = new Dictionary<KeybindAction, Keybind>(KeybindManager.GetAll());

        Text = "WinPoon Keybinds";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        ClientSize = new Size(500, 590);
        KeyDown += CaptureKey;

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(20, 18),
            Text = "Custom keybinds"
        };

        var help = new Label
        {
            AutoSize = false,
            Location = new Point(20, 47),
            Size = new Size(455, 34),
            Text = "Select a shortcut and press the desired modifier combination and key."
        };

        var rows = new FlowLayoutPanel
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            Location = new Point(20, 88),
            Size = new Size(455, 420),
            WrapContents = false
        };

        foreach (var action in Enum.GetValues<KeybindAction>())
        {
            var row = new Panel { Size = new Size(430, 29), Margin = new Padding(0, 0, 0, 3) };
            var label = new Label
            {
                AutoSize = false,
                Location = new Point(0, 5),
                Size = new Size(210, 22),
                Text = GetActionName(action)
            };
            var button = new Button
            {
                Location = new Point(215, 1),
                Size = new Size(205, 26),
                Text = _pendingBindings[action].ToString(),
                Tag = action
            };
            button.Click += BeginCapture;
            _bindingButtons[action] = button;
            row.Controls.Add(label);
            row.Controls.Add(button);
            rows.Controls.Add(row);
        }

        var resetButton = new Button
        {
            Location = new Point(20, 525),
            Size = new Size(100, 30),
            Text = "Reset"
        };
        resetButton.Click += (_, _) =>
        {
            _pendingBindings.Clear();
            foreach (var binding in KeybindManager.GetDefaults())
            {
                _pendingBindings[binding.Key] = binding.Value;
            }
            RefreshButtons();
        };

        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(275, 525),
            Size = new Size(95, 30),
            Text = "Cancel"
        };

        var applyButton = new Button
        {
            Location = new Point(375, 525),
            Size = new Size(100, 30),
            Text = "Apply"
        };
        applyButton.Click += ApplyChanges;

        Controls.AddRange([title, help, rows, resetButton, cancelButton, applyButton]);
        CancelButton = cancelButton;
    }

    private void BeginCapture(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not KeybindAction action)
        {
            return;
        }

        _capturingButton = button;
        _capturingAction = action;
        button.Text = "Press shortcut...";
        Focus();
    }

    private void CaptureKey(object? sender, KeyEventArgs e)
    {
        if (_capturingButton is null || KeybindManager.IsModifierKey((int)e.KeyCode))
        {
            return;
        }

        var keybind = new Keybind((int)e.KeyCode, KeybindManager.GetModifiers(e.Modifiers));
        _pendingBindings[_capturingAction] = keybind;
        _capturingButton.Text = keybind.ToString();
        _capturingButton = null;
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private void ApplyChanges(object? sender, EventArgs e)
    {
        if (!KeybindManager.TryApply(_pendingBindings, out var error))
        {
            MessageBox.Show(this, error, "Invalid keybinds", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Program.WriteLog("Custom keybinds applied.");
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RefreshButtons()
    {
        foreach (var binding in _pendingBindings)
        {
            _bindingButtons[binding.Key].Text = binding.Value.ToString();
        }
    }

    private static string GetActionName(KeybindAction action)
    {
        return action switch
        {
            KeybindAction.NextWindow => "Next bookmarked window",
            KeybindAction.PreviousWindow => "Previous bookmarked window",
            KeybindAction.PinWindow => "Pin focused window",
            KeybindAction.UnpinWindow => "Unpin focused window",
            KeybindAction.Exit => "Exit WinPoon",
            _ => $"Jump to bookmark slot {(int)action - (int)KeybindAction.Slot1 + 1}"
        };
    }
}
