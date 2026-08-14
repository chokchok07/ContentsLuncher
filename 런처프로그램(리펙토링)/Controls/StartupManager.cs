using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShowroomLauncher
{
    public static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(Application.ProductName, "\"" + Application.ExecutablePath + "\"");
                        }
                        else
                        {
                            if (key.GetValue(Application.ProductName) != null)
                            {
                                key.DeleteValue(Application.ProductName, false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError("SetStartup 실패", ex);
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(Application.ProductName);
                        return value != null;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError("IsStartupEnabled 실패", ex);
            }
            return false;
        }

        public static void CheckAndUpdatePath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(Application.ProductName);
                        if (value != null)
                        {
                            string registeredPath = value.ToString().Replace("\"", "");
                            string currentPath = Application.ExecutablePath;
                            if (registeredPath != currentPath)
                            {
                                key.SetValue(Application.ProductName, "\"" + currentPath + "\"");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError("CheckAndUpdatePath 실패", ex);
            }
        }
    }
}
