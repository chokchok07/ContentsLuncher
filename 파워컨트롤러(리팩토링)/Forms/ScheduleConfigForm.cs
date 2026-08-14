using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Web.Script.Serialization;

namespace ShowroomPowerController
{
    public class ScheduleConfigForm : Form
    {
        private ScheduleSettings settings;
        private List<DeviceItem> allDevices;
        private List<string> allSpaces;
        private PowerControllerForm mainForm;

        private TextBox txtWeekdayStart;
        private TextBox txtWeekdayEnd;
        private TextBox txtSaturdayStart;
        private TextBox txtSaturdayEnd;

        private CheckBox chkMon;
        private CheckBox chkTue;
        private CheckBox chkWed;
        private CheckBox chkThu;
        private CheckBox chkFri;
        private CheckBox chkSat;
        private CheckBox chkSun;

        private Button btnSave;
        private Button btnCancel;

        public ScheduleConfigForm(ScheduleSettings currentSettings, List<DeviceItem> devs, List<string> sps, PowerControllerForm parent)
        {
            this.settings = currentSettings;
            this.allDevices = devs;
            this.allSpaces = sps;
            this.mainForm = parent;

            this.Text = "⏰ 자동 스케줄 세부 시간 조율 (Auto Schedule Config)";
            this.Size = new Size(420, 390);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeConfigUI(boxClr);
            ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);
        }

        private void InitializeConfigUI(Color boxClr)
        {
            Font fontLabel = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);

            GroupBox gbTimes = new GroupBox();
            gbTimes.Text = " 요일별 전원 기동/종료 예약 시각 ";
            gbTimes.Size = new Size(360, 150);
            gbTimes.Location = new Point(20, 15);
            gbTimes.ForeColor = Color.White;
            gbTimes.Font = fontLabel;

            int xL = 15; int xI = 140; int yStart = 30; int yGap = 50;

            Label lWd = new Label() { Text = "평일(화~금) 시간:", Location = new Point(xL, yStart), AutoSize = true };
            txtWeekdayStart = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.WeekdayStart };
            Label lWdWave = new Label() { Text = "~", Location = new Point(xI + 85, yStart - 1), AutoSize = true };
            txtWeekdayEnd = new TextBox() { Location = new Point(xI + 105, yStart - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.WeekdayEnd };

            Label lSat = new Label() { Text = "토요일 시간:", Location = new Point(xL, yStart + yGap), AutoSize = true };
            txtSaturdayStart = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.SaturdayStart };
            Label lSatWave = new Label() { Text = "~", Location = new Point(xI + 85, yStart + yGap - 1), AutoSize = true };
            txtSaturdayEnd = new TextBox() { Location = new Point(xI + 105, yStart + yGap - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.SaturdayEnd };

            gbTimes.Controls.Add(lWd); gbTimes.Controls.Add(txtWeekdayStart); gbTimes.Controls.Add(lWdWave); gbTimes.Controls.Add(txtWeekdayEnd);
            gbTimes.Controls.Add(lSat); gbTimes.Controls.Add(txtSaturdayStart); gbTimes.Controls.Add(lSatWave); gbTimes.Controls.Add(txtSaturdayEnd);

            GroupBox gbIgnore = new GroupBox();
            gbIgnore.Text = " 자동 기동 무조건 차단 요일 (Ignore Days) ";
            gbIgnore.Size = new Size(360, 110);
            gbIgnore.Location = new Point(20, 180);
            gbIgnore.ForeColor = Color.White;
            gbIgnore.Font = fontLabel;

            chkMon = CreateDayCheck("월", "월요일", 15, 35);
            chkTue = CreateDayCheck("화", "화요일", 65, 35);
            chkWed = CreateDayCheck("수", "수요일", 115, 35);
            chkThu = CreateDayCheck("목", "목요일", 165, 35);
            chkFri = CreateDayCheck("금", "금요일", 215, 35);
            chkSat = CreateDayCheck("토", "토요일", 265, 35);
            chkSun = CreateDayCheck("일", "일요일", 315, 35);

            gbIgnore.Controls.Add(chkMon);
            gbIgnore.Controls.Add(chkTue);
            gbIgnore.Controls.Add(chkWed);
            gbIgnore.Controls.Add(chkThu);
            gbIgnore.Controls.Add(chkFri);
            gbIgnore.Controls.Add(chkSat);
            gbIgnore.Controls.Add(chkSun);

            Label lNote = new Label() { Text = "* 지정된 차단 요일은 스케줄 시각이 도달해도 전원을 제어하지 않습니다.", Location = new Point(15, 75), Size = new Size(330, 25), ForeColor = Color.Gray, Font = FontHelper.GetFont(7.5f, FontStyle.Italic) };
            gbIgnore.Controls.Add(lNote);

            btnSave = new Button() { Text = "💾 시간표 저장", Size = new Size(160, 36), Location = new Point(60, 305), BackColor = ColorTranslator.FromHtml("#1f8a65"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(100, 36), Location = new Point(240, 305), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(gbTimes);
            this.Controls.Add(gbIgnore);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
        }

        private CheckBox CreateDayCheck(string label, string tag, int x, int y)
        {
            CheckBox chk = new CheckBox();
            chk.Text = label;
            chk.Tag = tag;
            chk.Location = new Point(x, y);
            chk.Size = new Size(42, 20);
            chk.ForeColor = Color.White;
            chk.Checked = settings.IgnoreDays.Contains(tag);
            return chk;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!IsValidTimeFormat(txtWeekdayStart.Text) || !IsValidTimeFormat(txtWeekdayEnd.Text) ||
                !IsValidTimeFormat(txtSaturdayStart.Text) || !IsValidTimeFormat(txtSaturdayEnd.Text))
            {
                MessageBox.Show("시간 형식이 올바르지 않습니다. 반드시 '시:분(예: 08:50)' 규격으로 입력해 주십시오.", "형식 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            settings.WeekdayStart = txtWeekdayStart.Text.Trim();
            settings.WeekdayEnd = txtWeekdayEnd.Text.Trim();
            settings.SaturdayStart = txtSaturdayStart.Text.Trim();
            settings.SaturdayEnd = txtSaturdayEnd.Text.Trim();

            settings.IgnoreDays.Clear();
            List<CheckBox> chks = new List<CheckBox> { chkMon, chkTue, chkWed, chkThu, chkFri, chkSat, chkSun };
            foreach (var chk in chks)
            {
                if (chk.Checked) settings.IgnoreDays.Add(chk.Tag.ToString());
            }

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            try
            {
                DeviceConfig wrap = new DeviceConfig() 
                { 
                    Spaces = allSpaces,
                    Devices = allDevices,
                    Schedules = settings
                };
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string jsonText = serializer.Serialize(wrap);

                File.WriteAllText(configPath, jsonText, Encoding.UTF8);
                mainForm.ApplyScheduleSettings(settings);

                MessageBox.Show("자동 스케줄 설정 시간이 성공적으로 저장되었습니다.\n상세 가이드 텍스트가 실시간 업데이트됩니다.", "설정 저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsValidTimeFormat(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr)) return false;
            string[] parts = timeStr.Split(':');
            if (parts.Length != 2) return false;
            
            int hh, mm;
            if (!int.TryParse(parts[0], out hh) || !int.TryParse(parts[1], out mm)) return false;
            if (hh < 0 || hh > 23 || mm < 0 || mm > 59) return false;
            
            return true;
        }
    }

    // --- 9. REAL DEVICE CONFIG POPUP FORM ---
}
