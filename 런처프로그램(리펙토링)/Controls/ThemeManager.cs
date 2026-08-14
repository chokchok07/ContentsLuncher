using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShowroomLauncher
{
    public static class ThemeManager
    {
        private static bool _isDark = true;
        public static bool IsDark
        {
            get { return _isDark; }
            set { _isDark = value; }
        }

        public static Color WarningTextColor
        {
            get { return IsDark ? Color.FromArgb(245, 158, 11) : Color.FromArgb(192, 133, 50); }
        }
        public static Color GetDisabledBgColor(bool isDark)
        {
            return isDark ? Color.FromArgb(20, 21, 30) : Color.FromArgb(235, 234, 229);
        }

        public static Color GetDisabledTextColor(bool isDark)
        {
            return isDark ? Color.FromArgb(140, 142, 155) : Color.FromArgb(160, 160, 165);
        }

        public static void SetControlEnabledState(Control ctrl, bool isEnabled, bool isDark)
        {
            if (ctrl is TextBox)
            {
                TextBox txt = (TextBox)ctrl;
                txt.ReadOnly = !isEnabled;
                txt.TabStop = isEnabled;
                txt.BackColor = isEnabled 
                    ? (isDark ? Color.FromArgb(24, 25, 38) : Color.FromArgb(250, 250, 247)) 
                    : GetDisabledBgColor(isDark);
                txt.ForeColor = isEnabled 
                    ? (isDark ? Color.White : Color.FromArgb(38, 37, 30)) 
                    : GetDisabledTextColor(isDark);
            }
            else if (ctrl is Button)
            {
                Button btn = (Button)ctrl;
                btn.Enabled = isEnabled;
                if (btn.FlatStyle == FlatStyle.Flat)
                {
                    if (isEnabled)
                    {
                        if (btn.Name.Contains("Ping") || btn.Text.Contains("테스트"))
                        {
                            btn.BackColor = isDark ? Color.FromArgb(24, 25, 38) : Color.FromArgb(255, 255, 255);
                            btn.ForeColor = Color.FromArgb(16, 185, 129);
                            btn.FlatAppearance.BorderColor = isDark ? Color.FromArgb(37, 39, 54) : Color.FromArgb(230, 229, 224);
                        }
                        else
                        {
                            btn.BackColor = isDark ? Color.FromArgb(24, 25, 38) : Color.FromArgb(255, 255, 255);
                            btn.ForeColor = isDark ? Color.FromArgb(123, 97, 255) : Color.FromArgb(245, 78, 0);
                            btn.FlatAppearance.BorderColor = isDark ? Color.FromArgb(37, 39, 54) : Color.FromArgb(230, 229, 224);
                        }
                    }
                    else
                    {
                        btn.BackColor = GetDisabledBgColor(isDark);
                        btn.ForeColor = GetDisabledTextColor(isDark);
                        btn.FlatAppearance.BorderColor = isDark ? Color.FromArgb(30, 31, 42) : Color.FromArgb(215, 214, 209);
                    }
                }
            }
            else
            {
                ctrl.Enabled = isEnabled;
                ctrl.BackColor = isEnabled 
                    ? (isDark ? Color.FromArgb(28, 29, 43) : Color.FromArgb(255, 255, 255)) 
                    : GetDisabledBgColor(isDark);
                ctrl.ForeColor = isEnabled 
                    ? (isDark ? Color.White : Color.FromArgb(38, 37, 30)) 
                    : GetDisabledTextColor(isDark);
            }
        }
    }
}
