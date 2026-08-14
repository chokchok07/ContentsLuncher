using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Web.Script.Serialization;

namespace ShowroomPowerController
{
    public class DeviceCompositeEditForm : Form
    {
        private DeviceItem parentDevice;
        private List<DeviceItem> childDevices;
        private List<DeviceItem> allDevices;
        private PowerControllerForm mainForm;

        private TextBox txtName;
        private TextBox txtIp;
        private TextBox txtMac;
        private TextBox txtPort;
        private TextBox txtDesc;

        private RadioButton rbModeProjFirst;
        private TextBox txtProjDelay;
        private Label lblProjSec;

        private RadioButton rbModePcFirst;
        private TextBox txtPcDelay;
        private Label lblPcSec;

        private RadioButton rbModeSimultaneous;

        private GroupBox gbSubs;

        private Button btnSave;
        private Button btnCancel;
        private Button btnConnectTest;

        public DeviceCompositeEditForm(string parentId, List<DeviceItem> currentAllDevices, PowerControllerForm parentForm)
        {
            this.mainForm = parentForm;
            this.allDevices = currentAllDevices;
            
            this.parentDevice = currentAllDevices.Find(d => d.Id == parentId);
            if (this.parentDevice == null) return;

            this.childDevices = currentAllDevices.FindAll(d => d.Type == "Projector" && d.AssociatedDeviceId == parentId);

            this.Text = string.Format("⚙️ 통합 기동 세부 설정 - [{0}]", parentDevice.Name);
            this.Size = new Size(620, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeFormControls(boxClr);
            ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);
        }

        private void InitializeFormControls(Color boxClr)
        {
            Font fontLabel = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);

            GroupBox gbParent = new GroupBox();
            gbParent.Text = string.Format(" 🖥️ 메인 PC 설정 및 기동 방식 ({0}) ", parentDevice.Type);
            gbParent.Font = fontLabel;
            gbParent.Size = new Size(560, 248);
            gbParent.Location = new Point(20, 10);

            int xL = 15; int xI = 140; int yStart = 24; int yGap = 28;

            Label lName = new Label() { Text = "장비 이름:", Location = new Point(xL, yStart), AutoSize = true };
            txtName = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(180, 22), Font = fontInput, Text = parentDevice.Name };

            Label lIp = new Label() { Text = "IP 주소:", Location = new Point(xL, yStart + yGap), AutoSize = true };
            txtIp = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(180, 22), Font = fontInput, Text = parentDevice.IpAddress };

            Label lMac = new Label() { Text = "MAC 주소:", Location = new Point(xL, yStart + yGap * 2), AutoSize = true };
            txtMac = new TextBox() { Location = new Point(xI, yStart + yGap * 2 - 3), Size = new Size(180, 22), Font = fontInput, Text = parentDevice.MacAddress };

            Label lPort = new Label() { Text = "TCP 포트:", Location = new Point(xL, yStart + yGap * 3), AutoSize = true };
            txtPort = new TextBox() { Location = new Point(xI, yStart + yGap * 3 - 3), Size = new Size(180, 22), Font = fontInput, Text = parentDevice.Port.ToString() };

            Label lDesc = new Label() { Text = "설명:", Location = new Point(345, yStart), AutoSize = true };
            txtDesc = new TextBox() { Location = new Point(345, yStart + 20), Size = new Size(200, 85), Multiline = true, Font = fontInput, Text = parentDevice.Description };

            Label lModeTitle = new Label() { Text = "⚡ 전원 기동 시퀀스 설정:", Location = new Point(xL, 138), AutoSize = true, ForeColor = ColorTranslator.FromHtml("#f54e00"), Font = FontHelper.GetFont(9f, FontStyle.Bold) };

            int initialProjDelay = 10;
            if (childDevices.Count > 0 && childDevices[0].BootDelaySeconds > 0) initialProjDelay = childDevices[0].BootDelaySeconds;
            int initialPcDelay = parentDevice.BootDelaySeconds > 0 ? parentDevice.BootDelaySeconds : 10;

            rbModeProjFirst = new RadioButton() 
            { 
                Text = "프로젝터 우선 가동 ➡️ 대기시간:", 
                Location = new Point(25, 160), 
                AutoSize = true, 
                Font = fontInput, 
                Cursor = Cursors.Hand 
            };
            rbModeProjFirst.CheckedChanged += (s, e) => UpdateDelayControlsEnabled();

            txtProjDelay = new TextBox() 
            { 
                Location = new Point(245, 158), 
                Size = new Size(40, 22), 
                Font = fontInput, 
                Text = initialProjDelay.ToString(),
                TextAlign = HorizontalAlignment.Center
            };

            lblProjSec = new Label() 
            { 
                Text = "초 후 PC 켬 (권장: 신호 인식용)", 
                Location = new Point(290, 162), 
                AutoSize = true, 
                Font = fontInput
            };

            rbModePcFirst = new RadioButton() 
            { 
                Text = "PC 우선 가동 ➡️ 대기시간:", 
                Location = new Point(25, 186), 
                AutoSize = true, 
                Font = fontInput, 
                Cursor = Cursors.Hand 
            };
            rbModePcFirst.CheckedChanged += (s, e) => UpdateDelayControlsEnabled();

            txtPcDelay = new TextBox() 
            { 
                Location = new Point(210, 184), 
                Size = new Size(40, 22), 
                Font = fontInput, 
                Text = initialPcDelay.ToString(),
                TextAlign = HorizontalAlignment.Center
            };

            lblPcSec = new Label() 
            { 
                Text = "초 후 프로젝터 켬 (부팅 화면 숨김용)", 
                Location = new Point(255, 188), 
                AutoSize = true, 
                Font = fontInput
            };

            rbModeSimultaneous = new RadioButton() 
            { 
                Text = "동시 가동 (대기시간 없이 즉시 동시 기동)", 
                Location = new Point(25, 214), 
                AutoSize = true, 
                Font = fontInput, 
                Cursor = Cursors.Hand 
            };
            rbModeSimultaneous.CheckedChanged += (s, e) => UpdateDelayControlsEnabled();

            if (parentDevice.PowerOnSequenceMode == "PROJ_FIRST") rbModeProjFirst.Checked = true;
            else if (parentDevice.PowerOnSequenceMode == "SIMULTANEOUS") rbModeSimultaneous.Checked = true;
            else rbModePcFirst.Checked = true;

            gbParent.Controls.Add(lName); gbParent.Controls.Add(txtName);
            gbParent.Controls.Add(lIp); gbParent.Controls.Add(txtIp);
            gbParent.Controls.Add(lMac); gbParent.Controls.Add(txtMac);
            gbParent.Controls.Add(lPort); gbParent.Controls.Add(txtPort);
            gbParent.Controls.Add(lDesc); gbParent.Controls.Add(txtDesc);
            gbParent.Controls.Add(lModeTitle);
            gbParent.Controls.Add(rbModeProjFirst);
            gbParent.Controls.Add(txtProjDelay);
            gbParent.Controls.Add(lblProjSec);
            gbParent.Controls.Add(rbModePcFirst);
            gbParent.Controls.Add(txtPcDelay);
            gbParent.Controls.Add(lblPcSec);
            gbParent.Controls.Add(rbModeSimultaneous);

            gbSubs = new GroupBox();
            gbSubs.Text = " 📹 연동된 하위 빔 프로젝터 목록 ";
            gbSubs.ForeColor = Color.White;
            gbSubs.Font = fontLabel;
            gbSubs.Size = new Size(560, 190);
            gbSubs.Location = new Point(20, 265);

            Button btnAddProj = new Button() 
            { 
                Text = "➕ 프로젝터 추가", 
                Size = new Size(130, 24), 
                Location = new Point(415, 12), 
                BackColor = ColorTranslator.FromHtml("#1f8a65"), 
                ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat, 
                Cursor = Cursors.Hand, 
                Font = FontHelper.GetFont(8.5f, FontStyle.Bold) 
            };
            btnAddProj.FlatAppearance.BorderSize = 0;
            btnAddProj.Click += BtnAddProj_Click;
            gbSubs.Controls.Add(btnAddProj);

            PopulateSubProjRows();

            btnConnectTest = new Button() 
            { 
                Name = "btnConnectTest",
                Text = "📡 런처 통신 진단", 
                Size = new Size(160, 38), 
                Location = new Point(20, 475), 
                BackColor = ColorTranslator.FromHtml("#35374a"), 
                ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat, 
                Cursor = Cursors.Hand, 
                Font = FontHelper.GetFont(9f, FontStyle.Bold) 
            };
            btnConnectTest.Click += BtnConnectTest_Click;

            btnSave = new Button() { Text = "💾 변경사항 일괄 저장", Size = new Size(220, 38), Location = new Point(210, 475), BackColor = ColorTranslator.FromHtml("#1f8a65"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(130, 38), Location = new Point(450, 475), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(gbParent);
            this.Controls.Add(gbSubs);
            this.Controls.Add(btnConnectTest);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            UpdateDelayControlsEnabled();
        }

        private void PopulateSubProjRows()
        {
            List<Control> toRemove = new List<Control>();
            foreach (Control c in gbSubs.Controls)
            {
                if (c.Text != "➕ 프로젝터 추가") toRemove.Add(c);
            }
            foreach (var c in toRemove) gbSubs.Controls.Remove(c);

            Font fontLabel = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);

            if (childDevices.Count == 0)
            {
                Label lblEmpty = new Label() 
                { 
                    Text = "연동된 하위 빔 프로젝터 장치가 없습니다.\n(우측 상단의 [➕ 프로젝터 추가] 버튼을 눌러 이 PC에 귀속할 프로젝터를 등록하세요.)", 
                    Location = new Point(20, 65), 
                    Size = new Size(520, 60), 
                    ForeColor = Color.Gray, 
                    Font = FontHelper.GetFont(9.5f, FontStyle.Italic),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                gbSubs.Controls.Add(lblEmpty);
            }
            else
            {
                int yOffset = 40;
                foreach (var proj in childDevices)
                {
                    Label lblProjName = new Label() { Text = string.Format("📹 {0}", proj.Name), Location = new Point(20, yOffset), Size = new Size(180, 20), ForeColor = ColorTranslator.FromHtml("#f54e00"), AutoEllipsis = true, Font = fontLabel };
                    Label lblProjIp = new Label() { Text = string.Format("IP: {0}:{1}", proj.IpAddress, proj.Port), Location = new Point(210, yOffset), Size = new Size(180, 20), AutoSize = true, ForeColor = Color.LightGray, Font = fontInput };
                    Label lblProjStatus = new Label() { Text = string.Format("상태: {0}", proj.RuntimeStatus), Location = new Point(410, yOffset), Size = new Size(120, 20), AutoSize = true, ForeColor = proj.RuntimeStatus == "ONLINE" ? Color.FromArgb(16, 185, 129) : Color.Gray, Font = fontInput };

                    gbSubs.Controls.Add(lblProjName);
                    gbSubs.Controls.Add(lblProjIp);
                    gbSubs.Controls.Add(lblProjStatus);

                    yOffset += 35;
                }
            }
        }

        private void BtnAddProj_Click(object sender, EventArgs e)
        {
            using (DeviceAddForm addForm = new DeviceAddForm(this.allDevices, mainForm.CurrentSpaces, parentDevice.Id, parentDevice.Space, "Projector"))
            {
                if (addForm.ShowDialog(this) == DialogResult.OK && addForm.AddedDevice != null)
                {
                    this.allDevices.Add(addForm.AddedDevice);
                    this.childDevices.Add(addForm.AddedDevice);
                    
                    PopulateSubProjRows();
                    UpdateDelayControlsEnabled();
                    ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);

                    MessageBox.Show(string.Format("빔 프로젝터 '{0}'(ID: {1})가 성공적으로 추가되어 '{2}'에 연동되었습니다.\n하단의 '변경사항 일괄 저장'을 누르면 영구 기록됩니다.", 
                        addForm.AddedDevice.Name, addForm.AddedDevice.Id, parentDevice.Name), "프로젝터 추가 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void UpdateDelayControlsEnabled()
        {
            bool isProjFirst = rbModeProjFirst != null && rbModeProjFirst.Checked;
            bool isPcFirst = rbModePcFirst != null && rbModePcFirst.Checked;

            if (txtProjDelay != null)
            {
                txtProjDelay.Visible = isProjFirst;
                txtProjDelay.Enabled = isProjFirst;
            }
            if (lblProjSec != null)
            {
                lblProjSec.Visible = isProjFirst;
            }

            if (txtPcDelay != null)
            {
                txtPcDelay.Visible = isPcFirst;
                txtPcDelay.Enabled = isPcFirst;
            }
            if (lblPcSec != null)
            {
                lblPcSec.Visible = isPcFirst;
            }
        }

        private async void BtnConnectTest_Click(object sender, EventArgs e)
        {
            if (parentDevice == null || mainForm == null) return;
            
            int targetPort = 9999;
            int.TryParse(txtPort.Text, out targetPort);
            string targetIp = txtIp.Text.Trim();
            string targetName = txtName.Text.Trim();

            btnConnectTest.Enabled = false;
            btnConnectTest.Text = "⏳ 진단 중...";
            try
            {
                await mainForm.TestLauncherConnectionAsync(targetIp, targetPort > 0 ? targetPort : 9999, targetName);
            }
            finally
            {
                btnConnectTest.Enabled = true;
                btnConnectTest.Text = "📡 런처 통신 진단";
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            int parentPort;
            if (!int.TryParse(txtPort.Text, out parentPort))
            {
                MessageBox.Show("포트는 숫자만 입력 가능합니다.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            parentDevice.Name = txtName.Text.Trim();
            parentDevice.IpAddress = txtIp.Text.Trim();
            parentDevice.MacAddress = txtMac.Text.Trim();
            parentDevice.Port = parentPort;
            parentDevice.Description = txtDesc.Text;

            int projDelay = 10;
            if (!int.TryParse(txtProjDelay.Text, out projDelay)) projDelay = 10;
            int pcDelay = 10;
            if (!int.TryParse(txtPcDelay.Text, out pcDelay)) pcDelay = 10;

            if (rbModeProjFirst.Checked)
            {
                parentDevice.PowerOnSequenceMode = "PROJ_FIRST";
                parentDevice.BootDelaySeconds = pcDelay;
            }
            else if (rbModeSimultaneous.Checked)
            {
                parentDevice.PowerOnSequenceMode = "SIMULTANEOUS";
                parentDevice.BootDelaySeconds = 0;
            }
            else
            {
                parentDevice.PowerOnSequenceMode = "PC_FIRST";
                parentDevice.BootDelaySeconds = pcDelay;
            }

            foreach (var proj in childDevices)
            {
                proj.BootDelaySeconds = projDelay;
                proj.PowerOnSequenceMode = parentDevice.PowerOnSequenceMode;
            }

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            try
            {
                DeviceConfig wrap = new DeviceConfig() 
                { 
                    Spaces = mainForm.CurrentSpaces,
                    Devices = allDevices,
                    Schedules = mainForm.CurrentScheduleSettings
                };
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string jsonText = serializer.Serialize(wrap);

                File.WriteAllText(configPath, jsonText, Encoding.UTF8);
                mainForm.ApplyUpdatedDevices(allDevices, mainForm.CurrentSpaces);

                MessageBox.Show("성공적으로 변경사항이 devices.json에 일괄 영구 저장되었습니다.", "저장 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // --- 8. 스케줄 상세 조정 전용 다이얼로그 (ScheduleConfigForm) ---
}
