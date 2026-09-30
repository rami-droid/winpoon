# WinPoon

WinPoon is a lightweight Windows window switcher. It runs in the notification area and replaces the normal Alt+Tab window switcher with a simple top-level-window cycling mode.

## Usage

1. Build and run the project on Windows.
2. WinPoon appears in the notification area.
3. Press Alt+Tab to cycle through visible application windows.
4. Hold Shift while pressing Alt+Tab to cycle backwards.
5. Press Ctrl+Alt+Q to exit.
6. Double-click the tray icon or select Settings from its context menu to open the UI.

The settings window can enable pass-through mode and can register WinPoon to start with Windows.

## Requirements

- Windows 10 or later
- .NET 10 SDK/runtime

The global keyboard hook and startup option are Windows-specific. The application uses Windows Forms and has no third-party dependencies.

## Harpoon mode

WinPoon uses a session-local ordered list of bookmarked windows, similar to the Neovim Harpoon workflow. Alt+Tab and Shift+Alt+Tab cycle only through windows in this list.

- Press Ctrl+Alt+H while a window is focused to add it to the list.
- Press Ctrl+Alt+Backspace while a bookmarked window is focused to remove it.
- Use Settings to review the list, remove a selected window, or clear all bookmarks.
- The list is cleared when WinPoon exits.

- Press Ctrl+Alt+Numpad 1 through 9 to jump directly to a bookmarked slot.
