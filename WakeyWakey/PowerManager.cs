using System.Runtime.InteropServices;

namespace WakeyWakey;

/// <summary>
/// Wraps the Win32 SetThreadExecutionState API to prevent or restore
/// system sleep and away-mode.
/// </summary>
internal static class PowerManager
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    // Tells the system the state is in effect until the flag is cleared.
    private const uint ES_CONTINUOUS        = 0x80000000u;
    // Prevents the system from sleeping.
    private const uint ES_SYSTEM_REQUIRED   = 0x00000001u;
    // Prevents the system from entering away mode (e.g. for unattended operations).
    private const uint ES_AWAYMODE_REQUIRED = 0x00000040u;

    /// <summary>
    /// Prevents the system from sleeping or entering away mode.
    /// Returns true on success.
    /// </summary>
    public static bool SuppressSleep()
        => SetThreadExecutionState(
               ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED) != 0;

    /// <summary>
    /// Restores normal power-management behaviour.
    /// Returns true on success.
    /// </summary>
    public static bool RestoreSleep()
        => SetThreadExecutionState(ES_CONTINUOUS) != 0;
}
