using System.Diagnostics;

namespace OpenMic;

/// <summary>
/// USB direct connection: tunnel the phone's TCP port to localhost with
/// `adb forward`, then connect to 127.0.0.1. Lower latency, no Wi-Fi jitter.
/// Needs Android platform-tools (adb) on PATH + USB debugging on the phone.
/// </summary>
public static class Usb
{
    /// <summary>Returns "" on success, otherwise a message describing the problem.</summary>
    public static string Forward(int port)
    {
        try
        {
            var psi = new ProcessStartInfo("adb", $"forward tcp:{port} tcp:{port}")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "Could not start adb.";
            proc.WaitForExit(10000);
            if (proc.ExitCode != 0)
            {
                string err = proc.StandardError.ReadToEnd().Trim();
                return "adb forward failed (phone plugged in, USB debugging on, "
                       + "authorized?): " + err;
            }
            return "";
        }
        catch (Exception e)
        {
            return "adb not found. Install Android platform-tools and add it to "
                   + "PATH. (" + e.Message + ")";
        }
    }
}
