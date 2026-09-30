using System.Drawing;
using System.Windows.Forms;

internal sealed class WinPoonApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private SettingsForm? _settingsForm;

    public WinPoonApplicationContext()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Text = "WinPoon - Alt+Tab switcher",
            Icon = SystemIcons.Application,
            ContextMenuStrip = _menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        _trayIcon.BalloonTipTitle = "WinPoon is running";
        _trayIcon.BalloonTipText = "Alt+Tab switching is active. Double-click this icon to open settings.";
        _trayIcon.ShowBalloonTip(2500);
    }

    private void ShowSettings()
    {
        Program.RememberExternalForegroundWindow();

        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm();
        }

        if (!_settingsForm.Visible)
        {
            _settingsForm.Show();
        }

        _settingsForm.WindowState = FormWindowState.Normal;
        _settingsForm.Activate();
    }

    internal void ExitApplication()
    {
        _settingsForm?.Close();
        _trayIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsForm?.Dispose();
            _trayIcon.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _interceptAltTab;
    private readonly CheckBox _startWithWindows;
    private readonly Label _statusLabel;
    private readonly ListBox _harpoonList;
    private readonly TextBox _logTextBox;

    public SettingsForm()
    {
        Text = "WinPoon Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(410, 779);
        ShowInTaskbar = true;

        var ribbon = new Panel
        {
            BackColor = SystemColors.ControlLight,
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(12, 7, 0, 0)
        };
        var keybindsButton = new Button
        {
            Size = new Size(105, 30),
            Text = "Keybinds"
        };
        keybindsButton.Click += (_, _) =>
        {
            using var editor = new KeybindEditorForm();
            editor.ShowDialog(this);
        };
        ribbon.Controls.Add(keybindsButton);

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(24, 20),
            Text = "WinPoon"
        };

        var description = new Label
        {
            AutoSize = false,
            Location = new Point(24, 52),
            Size = new Size(360, 42),
            Text = "A lightweight window switcher that replaces the standard Alt+Tab behavior."
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.DarkGreen,
            Location = new Point(24, 108),
            Text = "Status: Running"
        };

        _interceptAltTab = new CheckBox
        {
            AutoSize = true,
            Checked = Program.SuppressAltTab,
            Location = new Point(24, 137),
            Text = "Intercept Alt+Tab"
        };
        _interceptAltTab.CheckedChanged += (_, _) =>
        {
            Program.SuppressAltTab = _interceptAltTab.Checked;
            _statusLabel.Text = _interceptAltTab.Checked
                ? "Status: Running"
                : "Status: Pass-through mode";
        };

        _startWithWindows = new CheckBox
        {
            AutoSize = true,
            Checked = Program.IsStartupEnabled(),
            Location = new Point(24, 165),
            Text = "Start WinPoon with Windows"
        };
        _startWithWindows.CheckedChanged += (_, _) =>
        {
            if (_startWithWindows.Checked)
            {
                Program.InstallStartup();
                Program.WriteLog("Startup with Windows enabled from Settings.");
            }
            else
            {
                Program.RemoveStartup();
                Program.WriteLog("Startup with Windows disabled from Settings.");
            }
        };

        var keybinds = new GroupBox
        {
            Location = new Point(24, 198),
            Size = new Size(360, 164),
            Text = "Keybinds"
        };

        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(14, 25),
            Text = "Next window:"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(125, 25),
            Font = new Font(Font, FontStyle.Bold),
            Tag = KeybindAction.NextWindow,
            Text = "Alt + Tab"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(14, 49),
            Text = "Previous window:"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(125, 49),
            Font = new Font(Font, FontStyle.Bold),
            Tag = KeybindAction.PreviousWindow,
            Text = "Alt + Shift + Tab"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(14, 73),
            Text = "Exit WinPoon:"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(125, 73),
            Font = new Font(Font, FontStyle.Bold),
            Tag = KeybindAction.Exit,
            Text = "Ctrl + Alt + Q"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(14, 97),
            Text = "Pin window:"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(125, 97),
            Font = new Font(Font, FontStyle.Bold),
            Tag = KeybindAction.PinWindow,
            Text = "Ctrl + Alt + H"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(14, 121),
            Text = "Unpin window:"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(125, 121),
            Font = new Font(Font, FontStyle.Bold),
            Tag = KeybindAction.UnpinWindow,
            Text = "Ctrl + Alt + Backspace"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(14, 145),
            Text = "Jump to slot:"
        });
        keybinds.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(125, 145),
            Font = new Font(Font, FontStyle.Bold),
            Tag = KeybindAction.Slot1,
            Text = "Ctrl + Alt + Numpad 1-9"
        });

        var bookmarks = new GroupBox
        {
            Location = new Point(24, 372),
            Size = new Size(360, 105),
            Text = "Harpoon windows"
        };

        _harpoonList = new ListBox
        {
            Location = new Point(14, 22),
            Size = new Size(332, 44),
            IntegralHeight = false
        };
        bookmarks.Controls.Add(_harpoonList);

        var addButton = new Button
        {
            Location = new Point(14, 72),
            Size = new Size(92, 25),
            Text = "Add current"
        };
        addButton.Click += (_, _) =>
        {
            Program.WriteLog($"Settings Add current pressed; pin {(Program.AddForegroundWindow() ? "succeeded" : "failed") }.");
            RefreshHarpoonList();
        };
        bookmarks.Controls.Add(addButton);

        var removeButton = new Button
        {
            Location = new Point(112, 72),
            Size = new Size(105, 25),
            Text = "Remove selected"
        };
        removeButton.Click += (_, _) =>
        {
            if (_harpoonList.SelectedItem is WindowBookmark bookmark)
            {
                Program.RemoveWindow(bookmark.Handle);
                Program.WriteLog($"Settings removed bookmark '{bookmark.Title}'.");
                RefreshHarpoonList();
            }
        };
        bookmarks.Controls.Add(removeButton);

        var clearButton = new Button
        {
            Location = new Point(223, 72),
            Size = new Size(75, 25),
            Text = "Clear"
        };
        clearButton.Click += (_, _) =>
        {
            Program.ClearHarpoonWindows();
            Program.WriteLog("Settings cleared all bookmarks.");
            RefreshHarpoonList();
        };
        bookmarks.Controls.Add(clearButton);

        RefreshHarpoonList();

        var logGroup = new GroupBox
        {
            Location = new Point(24, 488),
            Size = new Size(360, 165),
            Text = "Activity log"
        };

        _logTextBox = new TextBox
        {
            Location = new Point(14, 22),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(332, 132),
            BackColor = SystemColors.Window
        };
        logGroup.Controls.Add(_logTextBox);

        var closeButton = new Button
        {
            DialogResult = DialogResult.OK,
            Location = new Point(270, 670),
            Size = new Size(114, 30),
            Text = "Close"
        };
        closeButton.Click += (_, _) => Hide();

        Controls.AddRange([title, description, _statusLabel, _interceptAltTab, _startWithWindows, keybinds, bookmarks, logGroup, closeButton]);
        Controls.Add(ribbon);
        foreach (Control control in Controls)
        {
            if (control != ribbon)
            {
                control.Top += ribbon.Height;
            }
        }
        AcceptButton = closeButton;
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };

        Program.LogWritten += AppendLog;
        Program.BookmarksChanged += RefreshHarpoonListFromHook;
        KeybindManager.BindingsChanged += UpdateKeybindSummary;
        UpdateKeybindSummary();
        foreach (var message in Program.GetLogHistory())
        {
            AppendLog(message);
        }
    }

    private void RefreshHarpoonListFromHook()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(RefreshHarpoonList);
            }

            return;
        }

        RefreshHarpoonList();
    }

    private void RefreshHarpoonList()
    {
        _harpoonList.Items.Clear();
        foreach (var bookmark in Program.GetHarpoonWindows())
        {
            _harpoonList.Items.Add(bookmark);
        }
    }

    private void AppendLog(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(() => AppendLog(message));
            }

            return;
        }

        _logTextBox.AppendText(message + Environment.NewLine);
    }

    private void UpdateKeybindSummary()
    {
        foreach (Control control in Controls)
        {
            UpdateKeybindSummary(control);
        }
    }

    private static void UpdateKeybindSummary(Control control)
    {
        if (control.Tag is KeybindAction action && control is Label label)
        {
            label.Text = KeybindManager.Get(action).ToString();
        }

        foreach (Control child in control.Controls)
        {
            UpdateKeybindSummary(child);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Program.LogWritten -= AppendLog;
            Program.BookmarksChanged -= RefreshHarpoonListFromHook;
            KeybindManager.BindingsChanged -= UpdateKeybindSummary;
        }

        base.Dispose(disposing);
    }
}
