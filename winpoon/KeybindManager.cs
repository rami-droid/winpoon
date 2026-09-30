using System.Collections.ObjectModel;
using System.Windows.Forms;

[Flags]
internal enum ShortcutModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4
}

internal enum KeybindAction
{
    NextWindow,
    PreviousWindow,
    PinWindow,
    UnpinWindow,
    Exit,
    Slot1,
    Slot2,
    Slot3,
    Slot4,
    Slot5,
    Slot6,
    Slot7,
    Slot8,
    Slot9
}

internal readonly record struct Keybind(int KeyCode, ShortcutModifiers Modifiers)
{
    public override string ToString()
    {
        var modifiers = new List<string>();
        if (Modifiers.HasFlag(ShortcutModifiers.Control)) modifiers.Add("Ctrl");
        if (Modifiers.HasFlag(ShortcutModifiers.Alt)) modifiers.Add("Alt");
        if (Modifiers.HasFlag(ShortcutModifiers.Shift)) modifiers.Add("Shift");

        var key = KeyCode >= 0x61 && KeyCode <= 0x69
            ? $"Numpad {KeyCode - 0x60}"
            : new KeysConverter().ConvertToString((Keys)KeyCode) ?? $"Key {KeyCode:X2}";

        modifiers.Add(key);
        return string.Join(" + ", modifiers);
    }
}

internal static class KeybindManager
{
    private static readonly object SyncRoot = new();
    private static Dictionary<KeybindAction, Keybind> _bindings = CreateDefaults();
    internal static event Action? BindingsChanged;

    internal static IReadOnlyDictionary<KeybindAction, Keybind> GetAll()
    {
        lock (SyncRoot)
        {
            return new ReadOnlyDictionary<KeybindAction, Keybind>(
                new Dictionary<KeybindAction, Keybind>(_bindings));
        }
    }

    internal static Keybind Get(KeybindAction action)
    {
        lock (SyncRoot)
        {
            return _bindings[action];
        }
    }

    internal static bool TryApply(IReadOnlyDictionary<KeybindAction, Keybind> bindings, out string error)
    {
        var duplicate = bindings
            .GroupBy(pair => pair.Value)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            error = $"The shortcut '{duplicate.Key}' is assigned more than once.";
            return false;
        }

        lock (SyncRoot)
        {
            _bindings = new Dictionary<KeybindAction, Keybind>(bindings);
        }

        BindingsChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    internal static IReadOnlyDictionary<KeybindAction, Keybind> GetDefaults()
    {
        return new ReadOnlyDictionary<KeybindAction, Keybind>(CreateDefaults());
    }

    internal static void Reset()
    {
        lock (SyncRoot)
        {
            _bindings = CreateDefaults();
        }
    }

    internal static KeybindAction? Resolve(int keyCode, int? numpadSlot, ShortcutModifiers modifiers)
    {
        var logicalKey = numpadSlot is >= 0 and <= 8 ? 0x61 + numpadSlot.Value : keyCode;
        lock (SyncRoot)
        {
            foreach (var binding in _bindings)
            {
                if (binding.Value.KeyCode == logicalKey && binding.Value.Modifiers == modifiers)
                {
                    return binding.Key;
                }
            }
        }

        return null;
    }

    internal static bool IsModifierKey(int keyCode)
    {
        return keyCode is (int)Keys.Control or (int)Keys.ControlKey or
            (int)Keys.LControlKey or (int)Keys.RControlKey or
            (int)Keys.Menu or (int)Keys.LMenu or (int)Keys.RMenu or
            (int)Keys.Shift or (int)Keys.ShiftKey or
            (int)Keys.LShiftKey or (int)Keys.RShiftKey;
    }

    internal static ShortcutModifiers GetModifiers(Keys modifiers)
    {
        var result = ShortcutModifiers.None;
        if (modifiers.HasFlag(Keys.Control)) result |= ShortcutModifiers.Control;
        if (modifiers.HasFlag(Keys.Alt)) result |= ShortcutModifiers.Alt;
        if (modifiers.HasFlag(Keys.Shift)) result |= ShortcutModifiers.Shift;
        return result;
    }

    private static Dictionary<KeybindAction, Keybind> CreateDefaults()
    {
        var bindings = new Dictionary<KeybindAction, Keybind>
        {
            [KeybindAction.NextWindow] = new(0x09, ShortcutModifiers.Alt),
            [KeybindAction.PreviousWindow] = new(0x09, ShortcutModifiers.Alt | ShortcutModifiers.Shift),
            [KeybindAction.PinWindow] = new(0x48, ShortcutModifiers.Control | ShortcutModifiers.Alt),
            [KeybindAction.UnpinWindow] = new(0x08, ShortcutModifiers.Control | ShortcutModifiers.Alt),
            [KeybindAction.Exit] = new(0x51, ShortcutModifiers.Control | ShortcutModifiers.Alt)
        };

        for (var index = 0; index < 9; index++)
        {
            bindings[(KeybindAction)((int)KeybindAction.Slot1 + index)] =
                new(0x61 + index, ShortcutModifiers.Control | ShortcutModifiers.Alt);
        }

        return bindings;
    }
}
