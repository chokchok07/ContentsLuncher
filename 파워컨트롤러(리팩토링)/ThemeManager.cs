using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ShowroomPowerController
{
    public static class ThemeManager
    {
        private static bool _isDark = true;
        public static bool IsDark 
        { 
            get { return _isDark; } 
            set { _isDark = value; } 
        }

        // Theme colors
        public static Color FormBgColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#0f1016") : ColorTranslator.FromHtml("#f7f7f4"); } 
        }

        public static Color CardBgColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#232536") : ColorTranslator.FromHtml("#ffffff"); } 
        }

        public static Color TextColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#f8fafc") : ColorTranslator.FromHtml("#26251e"); } 
        }

        public static Color MutedTextColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#9ca3af") : ColorTranslator.FromHtml("#807d72"); } 
        }

        public static Color BorderColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#373954") : ColorTranslator.FromHtml("#e6e5e0"); } 
        }

        public static Color BorderColorSoft 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#333546") : ColorTranslator.FromHtml("#efeee8"); } 
        }

        public static Color InputBgColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#1a1c26") : ColorTranslator.FromHtml("#ffffff"); } 
        }

        public static Color PointColor 
        { 
            get { return ColorTranslator.FromHtml("#f54e00"); } 
        }

        // Hover / Down Colors
        public static Color ButtonMouseOverColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#333546") : ColorTranslator.FromHtml("#efeee8"); } 
        }

        public static Color ButtonMouseDownColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#1a1c26") : ColorTranslator.FromHtml("#e6e5e0"); } 
        }

        // Disabled colors (HSL tailored)
        public static Color DisabledTextColor 
        { 
            get { return IsDark ? Color.FromArgb(90, 88, 82) : Color.FromArgb(180, 180, 175); } 
        }

        public static Color DisabledBgColor 
        { 
            get { return IsDark ? ColorTranslator.FromHtml("#1a1c26") : ColorTranslator.FromHtml("#efeee8"); } 
        }

        public static void ApplyThemeTo(Control parent, bool isDark)
        {
            IsDark = isDark;

            // Form specific
            if (parent is Form)
            {
                Form form = (Form)parent;
                form.BackColor = FormBgColor;
                form.ForeColor = TextColor;
            }

            foreach (Control ctrl in parent.Controls)
            {
                ApplyControlStyle(ctrl);
                ApplyThemeTo(ctrl, isDark);
            }
        }

        private static void ApplyControlStyle(Control ctrl)
        {
            if (ctrl is Label)
            {
                Label label = (Label)ctrl;
                if (label.Name == "titleLabel" || label.Name == "nextScheduleLabel")
                {
                    label.ForeColor = PointColor;
                }
                else if (label.Name.Contains("TitleLabel") || label.Name.Contains("Title") || label.Text.Contains("제어반") || label.Text.Contains("모니터") || label.Text.Contains("로그"))
                {
                    label.ForeColor = TextColor;
                }
                else if (label.Name.EndsWith("Muted") || label.Name.Contains("Sub") || label.Name.Contains("Version") || label.Name.Contains("virtualDay") || label.Name.Contains("nextSchedule") || label.ForeColor == Color.Gray)
                {
                    label.ForeColor = MutedTextColor;
                }
                else
                {
                    label.ForeColor = TextColor;
                }
                label.BackColor = Color.Transparent;
            }
            else if (ctrl is GroupBox)
            {
                GroupBox gb = (GroupBox)ctrl;
                gb.ForeColor = TextColor;
                gb.BackColor = Color.Transparent;
            }
            else if (ctrl is ListView)
            {
                ListView lv = (ListView)ctrl;
                lv.BackColor = InputBgColor;
                lv.ForeColor = TextColor;
            }
            else if (ctrl is Button)
            {
                Button btn = (Button)ctrl;
                if (btn.FlatStyle == FlatStyle.Flat)
                {
                    if (btn.Name.Contains("Tab") || btn.Tag != null)
                    {
                        btn.FlatAppearance.BorderSize = 1;
                    }
                    else
                    {
                        btn.FlatAppearance.BorderSize = 0;
                    }
                    
                    // Determine button role by colors or text
                    if (btn.Name.Contains("Close") || btn.Name.Contains("Minimize") || btn.Name.Contains("Toggle") || btn.Name.Contains("theme") || btn.Name.Contains("Theme"))
                    {
                        btn.BackColor = Color.Transparent;
                        btn.ForeColor = MutedTextColor;
                        btn.FlatAppearance.MouseOverBackColor = ButtonMouseOverColor;
                        btn.FlatAppearance.MouseDownBackColor = ButtonMouseDownColor;
                    }
                    else if (btn.Name.Contains("Tab") || btn.Tag != null)
                    {
                        btn.FlatAppearance.MouseOverBackColor = ButtonMouseOverColor;
                        btn.FlatAppearance.MouseDownBackColor = ButtonMouseDownColor;
                    }
                    else if (btn.BackColor == ColorTranslator.FromHtml("#cf2d56") || btn.Name.Contains("Del") || btn.Name.Contains("Off") || btn.Name.Contains("delete") || btn.Name.Contains("Delete"))
                    {
                        btn.BackColor = ColorTranslator.FromHtml("#cf2d56");
                        btn.ForeColor = Color.White;
                        btn.FlatAppearance.MouseOverBackColor = IsDark ? ColorTranslator.FromHtml("#e11d48") : ColorTranslator.FromHtml("#dc2626");
                        btn.FlatAppearance.MouseDownBackColor = IsDark ? ColorTranslator.FromHtml("#be123c") : ColorTranslator.FromHtml("#991b1b");
                    }
                    else if (btn.BackColor == ColorTranslator.FromHtml("#1f8a65") || btn.Name.Contains("Save") || btn.Name.Contains("Apply") || btn.Name.Contains("save") || btn.Name.Contains("apply"))
                    {
                        btn.BackColor = ColorTranslator.FromHtml("#1f8a65");
                        btn.ForeColor = Color.White;
                        btn.FlatAppearance.MouseOverBackColor = IsDark ? ColorTranslator.FromHtml("#10b981") : ColorTranslator.FromHtml("#059669");
                        btn.FlatAppearance.MouseDownBackColor = IsDark ? ColorTranslator.FromHtml("#047857") : ColorTranslator.FromHtml("#065f46");
                    }
                    else if (btn.BackColor == ColorTranslator.FromHtml("#f54e00") || btn.Name.Contains("On") || btn.Name.Contains("Edit") || btn.Name.Contains("edit") || btn.Name.Contains("Add") || btn.Name.Contains("add"))
                    {
                        btn.BackColor = ColorTranslator.FromHtml("#f54e00");
                        btn.ForeColor = Color.White;
                        btn.FlatAppearance.MouseOverBackColor = IsDark ? ColorTranslator.FromHtml("#ea580c") : ColorTranslator.FromHtml("#dc2626");
                        btn.FlatAppearance.MouseDownBackColor = IsDark ? ColorTranslator.FromHtml("#be123c") : ColorTranslator.FromHtml("#991b1b");
                    }
                    else
                    {
                        btn.BackColor = IsDark ? ColorTranslator.FromHtml("#35374a") : ColorTranslator.FromHtml("#e6e5e0");
                        btn.ForeColor = TextColor;
                        btn.FlatAppearance.MouseOverBackColor = ButtonMouseOverColor;
                        btn.FlatAppearance.MouseDownBackColor = ButtonMouseDownColor;
                    }
                }
            }
            else if (ctrl is TextBox)
            {
                TextBox txt = (TextBox)ctrl;
                txt.BackColor = InputBgColor;
                txt.ForeColor = TextColor;
                if (txt.BorderStyle == BorderStyle.Fixed3D || txt.BorderStyle == BorderStyle.FixedSingle)
                {
                    txt.BorderStyle = BorderStyle.FixedSingle;
                }
            }
            else if (ctrl is RichTextBox)
            {
                RichTextBox rtb = (RichTextBox)ctrl;
                rtb.BackColor = InputBgColor;
                rtb.ForeColor = TextColor;
                rtb.BorderStyle = BorderStyle.None;
            }
            else if (ctrl is ComboBox)
            {
                ComboBox cb = (ComboBox)ctrl;
                cb.BackColor = InputBgColor;
                cb.ForeColor = TextColor;
            }
            else if (ctrl is RadioButton)
            {
                RadioButton rb = (RadioButton)ctrl;
                rb.ForeColor = TextColor;
                rb.BackColor = Color.Transparent;
            }
            else if (ctrl is CheckBox)
            {
                CheckBox chk = (CheckBox)ctrl;
                chk.ForeColor = TextColor;
                chk.BackColor = Color.Transparent;
            }
            else if (ctrl is DataGridView)
            {
                DataGridView dgv = (DataGridView)ctrl;
                dgv.BackgroundColor = CardBgColor;
                dgv.GridColor = BorderColorSoft;
                dgv.DefaultCellStyle.BackColor = CardBgColor;
                dgv.DefaultCellStyle.ForeColor = TextColor;
                dgv.DefaultCellStyle.SelectionBackColor = PointColor;
                dgv.DefaultCellStyle.SelectionForeColor = Color.White;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = IsDark ? ColorTranslator.FromHtml("#2d2f44") : ColorTranslator.FromHtml("#e2e8f0");
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
            }
            else if (ctrl is Panel)
            {
                Panel panel = (Panel)ctrl;
                if (panel.Name == "titlePanel")
                {
                    panel.BackColor = IsDark ? ColorTranslator.FromHtml("#161824") : ColorTranslator.FromHtml("#e6e5e0");
                }
                else if (panel.Name.Contains("Group") || panel.Name.Contains("Panel") || panel.Name.Contains("Container") || panel.GetType().Name == "DoubleBufferedPanel")
                {
                    panel.BackColor = CardBgColor;
                }
                else
                {
                    panel.BackColor = Color.Transparent;
                }
            }
            else if (ctrl is FlowLayoutPanel)
            {
                FlowLayoutPanel flp = (FlowLayoutPanel)ctrl;
                flp.BackColor = CardBgColor;
            }
            else if (ctrl is TabControl)
            {
                TabControl tabControl = (TabControl)ctrl;
                tabControl.BackColor = FormBgColor;
            }
            else if (ctrl is TabPage)
            {
                TabPage tabPage = (TabPage)ctrl;
                tabPage.BackColor = CardBgColor;
                tabPage.ForeColor = TextColor;
            }
        }

        public static void SetControlEnabledState(Control ctrl, bool isEnabled, bool isDark)
        {
            IsDark = isDark;
            if (ctrl is RichTextBox)
            {
                RichTextBox rtb = (RichTextBox)ctrl;
                rtb.ReadOnly = true;
                rtb.BackColor = InputBgColor;
                rtb.ForeColor = TextColor;
                return;
            }
            if (ctrl is TextBox)
            {
                TextBox txt = (TextBox)ctrl;
                txt.ReadOnly = !isEnabled;
                txt.TabStop = isEnabled;
                txt.BackColor = isEnabled ? InputBgColor : DisabledBgColor;
                txt.ForeColor = isEnabled ? TextColor : DisabledTextColor;
            }
            else if (ctrl is Button)
            {
                Button btn = (Button)ctrl;
                btn.Enabled = isEnabled;
                if (btn.FlatStyle == FlatStyle.Flat)
                {
                    if (isEnabled)
                    {
                        if (btn.Name.Contains("On"))
                        {
                            btn.BackColor = ColorTranslator.FromHtml("#f54e00");
                            btn.ForeColor = Color.White;
                        }
                        else if (btn.Name.Contains("Off"))
                        {
                            btn.BackColor = ColorTranslator.FromHtml("#cf2d56");
                            btn.ForeColor = Color.White;
                        }
                        else
                        {
                            btn.BackColor = IsDark ? ColorTranslator.FromHtml("#35374a") : ColorTranslator.FromHtml("#e6e5e0");
                            btn.ForeColor = TextColor;
                        }
                    }
                    else
                    {
                        btn.BackColor = DisabledBgColor;
                        btn.ForeColor = DisabledTextColor;
                    }
                }
            }
            else
            {
                ctrl.Enabled = isEnabled;
                ctrl.BackColor = isEnabled ? CardBgColor : DisabledBgColor;
                ctrl.ForeColor = isEnabled ? TextColor : DisabledTextColor;
            }
        }
    }
}
