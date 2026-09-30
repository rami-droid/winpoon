# WinPoon

WinPoon is a Windows window switcher inspired by the Neovim Harpoon workflow. Pin the windows you use most often, then cycle through them with `Alt+Tab` or jump directly to a bookmarked slot.

## Features

- Harpoon-style, session-local window bookmarks
- Custom `Alt+Tab` switching limited to bookmarked windows
- Reverse switching with `Alt+Shift+Tab`
- Direct switching to bookmark slots 1 through 9
- Notification-area tray application
- Settings window with bookmark management
- Live activity log with shortcut and window-switching events
- Optional Windows startup registration
- Native Win32 keyboard hook with no third-party runtime dependencies

## Keybinds

| Shortcut | Action |
| --- | --- |
| `Alt+Tab` | Switch to the next bookmarked window |
| `Alt+Shift+Tab` | Switch to the previous bookmarked window |
| `Ctrl+Alt+H` | Pin the currently focused window |
| `Ctrl+Alt+Backspace` | Unpin the currently focused window |
| `Ctrl+Alt+Numpad 1-9` | Jump directly to bookmark slot 1-9 |
| `Ctrl+Alt+Q` | Exit WinPoon |

Numpad shortcuts work with Num Lock enabled or disabled. Bookmark slots use the order in which windows were pinned. Bookmarks are currently stored for the active session and are cleared when WinPoon exits.

## Settings

Open Settings by double-clicking the WinPoon tray icon or selecting **Settings...** from its context menu.

The Settings window provides:

- Alt+Tab interception toggle
- Start with Windows toggle
- Harpoon bookmark list
- Add current, remove selected, and clear bookmark controls
- Live activity log

The **Keybinds** button in the top settings ribbon opens the custom keybind editor. Each action can be assigned a modifier combination and key, duplicate shortcuts are rejected, and **Reset** restores the defaults. Changes apply immediately for the current session and are not persisted yet.

If the Settings window is active, **Add current** uses the last external application window so the WinPoon UI is not pinned accidentally.

## Installation

TODO: Complete the installation and packaging process.


## Requirements

- Windows 10 or later
- .NET 10 SDK/runtime

WinPoon is Windows-specific because it uses the Win32 global keyboard hook and Windows Forms notification-area APIs.
