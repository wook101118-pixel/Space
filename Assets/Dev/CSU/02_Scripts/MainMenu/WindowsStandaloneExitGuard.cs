using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Dev.CSU._02_Scripts.MainMenu
{
    /// <summary>
    /// Avoids a Unity 6000.3 Windows Player shutdown crash in the native
    /// accessibility cleanup path. Remove this guard after upgrading to an
    /// engine version whose PlatformAccessibilityManager teardown is safe.
    /// </summary>
    internal static class WindowsStandaloneExitGuard
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _installed = false;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            Application.wantsToQuit += ExitBeforeNativeCleanup;
        }

        private static bool ExitBeforeNativeCleanup()
        {
            try
            {
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            IntPtr process = GetCurrentProcess();
            if (!TerminateProcess(process, 0))
            {
                Debug.LogError(
                    "[Shutdown] Could not terminate the Windows Player " +
                    $"cleanly. Win32 error {Marshal.GetLastWin32Error()}.");
                return false;
            }

            return false;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(
            IntPtr process,
            uint exitCode);
#endif
    }
}
