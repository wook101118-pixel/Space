using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace Dev.CSU._02_Scripts.MainMenu
{
    [Preserve]
    [DisallowMultipleComponent]
    public sealed class MontagemWindowTitle : MonoBehaviour
    {
        private const string PlayerFacingTitle = "몬타잼";
        private const float RefreshInterval = 1f;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static bool _installed;
        private float _nextRefreshTime;
        private bool _successLogged;
        private bool _failureLogged;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _installed = false;
        }
#endif

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_installed)
            {
                return;
            }

            _installed = true;
            GameObject root = new GameObject(
                nameof(MontagemWindowTitle),
                typeof(MontagemWindowTitle));
            root.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(root);
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            ApplyPlayerFacingTitle();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void ApplyPlayerFacingTitle()
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    process.Refresh();
                    IntPtr windowHandle = process.MainWindowHandle;
                    if (windowHandle == IntPtr.Zero)
                    {
                        return;
                    }

                    if (SetWindowText(windowHandle, PlayerFacingTitle))
                    {
                        if (!_successLogged)
                        {
                            Debug.Log(
                                "[Branding] Windows title set to 몬타잼. " +
                                "The legacy PlayerSettings product name " +
                                "remains unchanged for PlayerPrefs " +
                                "compatibility.");
                            _successLogged = true;
                        }

                        return;
                    }

                    LogFailureOnce(
                        $"Win32 error {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception exception)
            {
                LogFailureOnce(
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        private void LogFailureOnce(string reason)
        {
            if (_failureLogged)
            {
                return;
            }

            _failureLogged = true;
            Debug.LogWarning(
                $"[Branding] Could not set the Windows title to 몬타잼 " +
                $"without changing the PlayerPrefs identity. {reason}",
                this);
        }

        [DllImport(
            "user32.dll",
            EntryPoint = "SetWindowTextW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowText(
            IntPtr windowHandle,
            string title);
#endif
    }
}
