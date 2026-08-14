using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ShowroomLauncher
{
    public class SchedulerForm : Form
    {
        public bool SchedulerEnabled { get; private set; }
        public string AutoStartContentId { get; private set; }
        public string AutoShutdownTime { get; private set; }
        public bool IsPcShutdown { get; private set; }

        private Button btnScheduleOn;
        private Button btnScheduleOff;
        private ComboBox cbContent;
        private ComboBox cbHour;
        private ComboBox cbMinute;
        private CheckBox chkPc;
        private Label lblNote;
        private Button btnSave;
        private Button btnCancel;
        private List<ContentItem> items;

        private bool isPowerControllerEnabled = false;
        private bool isDarkMode = true;

        // Theme colors
        private Color colorCanvas;
        private Color colorHeader;
        private Color colorCard;
        private Color colorInk;
        private Color colorMuted;
        private Color colorHairline;
        private Color colorPrimary;
        private Color colorInputBg;

        public SchedulerForm(List<ContentItem> contentList, bool enabled, string autoStartId, string shutdownTime, bool pcShutdown, bool powerControllerEnabled, bool darkMode)
        {
            this.items = contentList;
            this.SchedulerEnabled = enabled;
            this.AutoStartContentId = autoStartId;
            this.AutoShutdownTime = shutdownTime;
            this.IsPcShutdown = pcShutdown;
            this.isPowerControllerEnabled = powerControllerEnabled;
            this.isDarkMode = darkMode;
            this.Load += SchedulerForm_Load;

            // Setup Theme Colors
            colorCanvas = isDarkMode ? Color.FromArgb(24, 25, 38) : Color.FromArgb(247, 247, 244);
            colorHeader = isDarkMode ? Color.FromArgb(18, 19, 28) : Color.FromArgb(250, 250, 247);
            colorCard = isDarkMode ? Color.FromArgb(28, 29, 43) : Color.FromArgb(255, 255, 255);
            colorInk = isDarkMode ? Color.White : Color.FromArgb(38, 37, 30);
            colorMuted = isDarkMode ? Color.FromArgb(156, 163, 175) : Color.FromArgb(128, 125, 114);
            colorHairline = isDarkMode ? Color.FromArgb(50, 50, 70) : Color.FromArgb(230, 229, 224);
            colorPrimary = isDarkMode ? Color.FromArgb(123, 97, 255) : Color.FromArgb(245, 78, 0);
            colorInputBg = isDarkMode ? Color.FromArgb(15, 16, 25) : Color.FromArgb(255, 255, 255);

            InitializeComponent();

            UpdateSchedulerToggleUI();
            ToggleControls(enabled);
            
            // If controller is linked, force checkoff for PC shutdown
            chkPc.Checked = isPowerControllerEnabled ? false : pcShutdown;

            // Populate ComboBox
            cbContent.Items.Add(new ComboBoxItem { Text = "사용 안 함 (직접 실행)", Value = "" });
            int selectIdx = 0;
            for (int i = 0; i < items.Count; i++)
            {
                cbContent.Items.Add(new ComboBoxItem { Text = items[i].name, Value = items[i].id });
                if (items[i].id == autoStartId)
                {
                    selectIdx = i + 1; // +1 because index 0 is "사용 안 함"
                }
            }
            cbContent.SelectedIndex = selectIdx;

            // Select hour/minute
            string h = "18";
            string m = "00";
            if (!string.IsNullOrEmpty(shutdownTime) && shutdownTime.Contains(":"))
            {
                string[] parts = shutdownTime.Split(':');
                if (parts.Length == 2)
                {
                    h = parts[0];
                    m = parts[1];
                }
            }

            int hIdx = cbHour.FindStringExact(h);
            cbHour.SelectedIndex = hIdx >= 0 ? hIdx : 18;

            int mIdx = cbMinute.FindStringExact(m);
            cbMinute.SelectedIndex = mIdx >= 0 ? mIdx : 0;

            btnScheduleOn.Click += (s, e) =>
            {
                this.SchedulerEnabled = true;
                UpdateSchedulerToggleUI();
                ToggleControls(true);
            };

            btnScheduleOff.Click += (s, e) =>
            {
                this.SchedulerEnabled = false;
                UpdateSchedulerToggleUI();
                ToggleControls(false);
            };
        }

        private void UpdateSchedulerToggleUI()
        {
            if (this.SchedulerEnabled)
            {
                btnScheduleOn.BackColor = colorPrimary;
                btnScheduleOn.ForeColor = Color.White;
                btnScheduleOff.BackColor = colorCard;
                btnScheduleOff.ForeColor = colorMuted;
            }
            else
            {
                btnScheduleOn.BackColor = colorCard;
                btnScheduleOn.ForeColor = colorMuted;
                btnScheduleOff.BackColor = isDarkMode ? Color.FromArgb(80, 80, 100) : Color.FromArgb(200, 200, 205);
                btnScheduleOff.ForeColor = colorInk;
            }
        }

        private void ToggleControls(bool enabled)
        {
            cbContent.Enabled = enabled;

            // If power controller is enabled, shut down controls must be disabled permanently
            if (isPowerControllerEnabled)
            {
                cbHour.Enabled = false;
                cbMinute.Enabled = false;
                chkPc.AutoCheck = false;
                chkPc.Checked = false;
                chkPc.ForeColor = ThemeManager.GetDisabledTextColor(isDarkMode);
                chkPc.Cursor = Cursors.Default;
            }
            else
            {
                cbHour.Enabled = enabled;
                cbMinute.Enabled = enabled;
                chkPc.AutoCheck = enabled;
                if (enabled)
                {
                    chkPc.ForeColor = colorInk;
                    chkPc.Cursor = Cursors.Hand;
                }
                else
                {
                    chkPc.ForeColor = ThemeManager.GetDisabledTextColor(isDarkMode);
                    chkPc.Cursor = Cursors.Default;
                    chkPc.Checked = false;
                }
            }
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(420, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = colorCanvas;

            Font fontLabel = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);

            // Custom Title
            Panel modalHeader = new Panel();
            modalHeader.Dock = DockStyle.Top;
            modalHeader.Height = 50;
            modalHeader.BackColor = colorHeader;

            Label lblTitle = new Label();
            lblTitle.Text = "⏰  글로벌 자동 운용 설정";
            lblTitle.ForeColor = colorInk;
            lblTitle.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            lblTitle.Location = new Point(20, 15);
            lblTitle.AutoSize = true;
            modalHeader.Controls.Add(lblTitle);
            this.Controls.Add(modalHeader);

            // Scheduler toggle panel label and buttons
            CreateLabel("자동 운용 스케줄러 활성화", 25, 75, colorMuted, fontLabel);

            btnScheduleOn = new Button();
            btnScheduleOn.Text = "ON";
            btnScheduleOn.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnScheduleOn.FlatStyle = FlatStyle.Flat;
            btnScheduleOn.FlatAppearance.BorderSize = 0;
            btnScheduleOn.Size = new Size(60, 24);
            btnScheduleOn.Location = new Point(205, 72);
            btnScheduleOn.Cursor = Cursors.Hand;
            this.Controls.Add(btnScheduleOn);

            btnScheduleOff = new Button();
            btnScheduleOff.Text = "OFF";
            btnScheduleOff.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnScheduleOff.FlatStyle = FlatStyle.Flat;
            btnScheduleOff.FlatAppearance.BorderSize = 0;
            btnScheduleOff.Size = new Size(60, 24);
            btnScheduleOff.Location = new Point(270, 72);
            btnScheduleOff.Cursor = Cursors.Hand;
            this.Controls.Add(btnScheduleOff);

            // Auto-start content selection
            CreateLabel("기동 시 자동 실행할 콘텐츠", 25, 120, colorMuted, fontLabel);
            cbContent = new ComboBox();
            cbContent.Location = new Point(25, 140);
            cbContent.Width = 370;
            cbContent.BackColor = colorInputBg;
            cbContent.ForeColor = colorInk;
            cbContent.Font = fontInput;
            cbContent.DropDownStyle = ComboBoxStyle.DropDownList;
            cbContent.FlatStyle = FlatStyle.Flat;
            this.Controls.Add(cbContent);

            // Auto-shutdown hour/minute dropdowns
            CreateLabel("자동 종료 시간", 25, 190, colorMuted, fontLabel);
            
            cbHour = new ComboBox();
            cbHour.Location = new Point(25, 210);
            cbHour.Width = 60;
            cbHour.BackColor = colorInputBg;
            cbHour.ForeColor = colorInk;
            cbHour.Font = fontInput;
            cbHour.DropDownStyle = ComboBoxStyle.DropDownList;
            cbHour.FlatStyle = FlatStyle.Flat;
            for (int i = 0; i < 24; i++) cbHour.Items.Add(i.ToString("D2"));
            this.Controls.Add(cbHour);

            CreateLabel("시", 90, 212, colorMuted, fontLabel);

            cbMinute = new ComboBox();
            cbMinute.Location = new Point(115, 210);
            cbMinute.Width = 60;
            cbMinute.BackColor = colorInputBg;
            cbMinute.ForeColor = colorInk;
            cbMinute.Font = fontInput;
            cbMinute.DropDownStyle = ComboBoxStyle.DropDownList;
            cbMinute.FlatStyle = FlatStyle.Flat;
            for (int i = 0; i < 60; i++) cbMinute.Items.Add(i.ToString("D2"));
            this.Controls.Add(cbMinute);

            CreateLabel("분", 180, 212, colorMuted, fontLabel);

            // PC Shutdown checkbox
            chkPc = new CheckBox();
            chkPc.Text = "종료 시 PC도 끄기";
            chkPc.Font = fontInput;
            chkPc.ForeColor = colorInk;
            chkPc.Location = new Point(240, 210);
            chkPc.Size = new Size(150, 24);
            chkPc.Cursor = Cursors.Hand;
            this.Controls.Add(chkPc);

            // Integration mode notification message
            lblNote = new Label();
            lblNote.Text = "※ 제어기 연동 모드에서는 자동 종료 일정을 제어기에서 중앙 제어하므로 로컬 자동 종료 설정은 비활성화됩니다.";
            lblNote.Font = FontHelper.GetFont(7.5f, FontStyle.Bold);
            ThemeManager.IsDark = isDarkMode;
            lblNote.ForeColor = ThemeManager.WarningTextColor;
            lblNote.Location = new Point(25, 245);
            lblNote.Size = new Size(370, 45);
            lblNote.AutoSize = false;
            lblNote.Visible = false;
            this.Controls.Add(lblNote);

            // Action buttons
            btnSave = new Button();
            btnSave.Text = "저장하기";
            btnSave.Font = FontHelper.GetFont(9.5f, FontStyle.Bold);
            btnSave.BackColor = colorPrimary;
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Size = new Size(100, 36);
            btnSave.Location = new Point(190, 300);
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            // Cancel button
            btnCancel = new Button();
            btnCancel.Text = "취소";
            btnCancel.Font = FontHelper.GetFont(9.5f, FontStyle.Bold);
            btnCancel.BackColor = colorCard;
            btnCancel.ForeColor = colorMuted;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = colorHairline;
            btnCancel.Size = new Size(90, 36);
            btnCancel.Location = new Point(305, 300);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            // Border
            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(colorHairline, 1.5f))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };
        }

        private void CreateLabel(string text, int x, int y, Color color, Font font)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.ForeColor = color;
            lbl.Font = font;
            lbl.Location = new Point(x, y);
            lbl.AutoSize = true;
            this.Controls.Add(lbl);
        }

        private void SchedulerForm_Load(object sender, EventArgs e)
        {
            if (isPowerControllerEnabled)
            {
                lblNote.Visible = true;
                cbContent.Enabled = this.SchedulerEnabled;
                cbHour.Enabled = false;
                cbMinute.Enabled = false;
                chkPc.Enabled = true;
                chkPc.AutoCheck = false;
                chkPc.ForeColor = ThemeManager.GetDisabledTextColor(isDarkMode);
            }
            else
            {
                lblNote.Visible = false;
                cbContent.Enabled = this.SchedulerEnabled;
                cbHour.Enabled = this.SchedulerEnabled;
                cbMinute.Enabled = this.SchedulerEnabled;
                chkPc.Enabled = this.SchedulerEnabled;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (this.SchedulerEnabled)
            {
                ComboBoxItem selectedItem = cbContent.SelectedItem as ComboBoxItem;
                this.AutoStartContentId = selectedItem != null ? selectedItem.Value : "";
                
                if (isPowerControllerEnabled)
                {
                    this.AutoShutdownTime = "";
                    this.IsPcShutdown = false;
                }
                else
                {
                    string hourStr = cbHour.SelectedItem != null ? cbHour.SelectedItem.ToString() : "18";
                    string minStr = cbMinute.SelectedItem != null ? cbMinute.SelectedItem.ToString() : "00";
                    this.AutoShutdownTime = hourStr + ":" + minStr;
                    this.IsPcShutdown = chkPc.Checked;
                }
            }
            else
            {
                this.AutoStartContentId = "";
                this.AutoShutdownTime = "";
                this.IsPcShutdown = false;
            }

            this.DialogResult = DialogResult.OK;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                Point pos = this.PointToClient(new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16));
                int border = 8; // 테두리 감지 마진 (px)

                bool left = pos.X <= border;
                bool right = pos.X >= this.ClientSize.Width - border;
                bool top = pos.Y <= border;
                bool bottom = pos.Y >= this.ClientSize.Height - border;

                if (left && top) m.Result = (IntPtr)HTTOPLEFT;
                else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
                else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            }
        }
    }
}
