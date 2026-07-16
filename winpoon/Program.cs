using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

class Program {

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public static void Main(string[] args) {
        var openWindows = Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle) );
        var savedWindows = new List<Process>();

        foreach (var p in openWindows) { 
            Console.WriteLine(p.MainWindowTitle);
        }
    }
}
