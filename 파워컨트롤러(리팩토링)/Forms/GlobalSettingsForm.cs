using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Web.Script.Serialization;

namespace ShowroomPowerController
{
    public class GlobalSettingsForm : Form
    {
        private PowerControllerForm mainForm;
        private List<DeviceItem> allDevices;
        private List<string> allSpaces;
        private ScheduleSettings scheduleSettings;

        // Custom Modern Segmented Tabs
        private FlowLayoutPanel tabHeaderPanel;
        private Button btnTabGeneral;
        private Button btnTabSchedule;
        private Button btnTabDevices;
        private Button btnTabMaintenance;

        private Panel contentContainer;
        private Panel panelGeneral;
        private Panel panelSchedule;
        private Panel panelDevices;
        private Panel panelMaintenance;

        // Tab 1: General
        private RadioButton rbDarkTheme;
        private RadioButton rbLightTheme;
        private CheckBox chkAutoStart;
        private CheckBox chkConfirmAction;
        private CheckBox chkRealNetwork;

        // Tab 2: Schedule
        private TextBox txtWeekdayStart;
        private TextBox txtWeekdayEnd;
        private TextBox txtSaturdayStart;
        private TextBox txtSaturdayEnd;
        private CheckBox chkMon, chkTue, chkWed, chkThu, chkFri, chkSat, chkSun;
        private CheckBox chkAutoScheduleEnabled;

        // Tab 3: Devices
        private DataGridView gridDevices;
        private Button btnAddDevice;
        private Button btnEditDevice;
        private Button btnDeleteDevice;

        // Tab 4: Maintenance
        private Button btnBackupConfig;
        private Button btnRestoreConfig;
        private Button btnExportLogs;
        private Button btnClearLogs;

        // Bottom
        private Button btnSave;
        private Button btnClose;

        public GlobalSettingsForm(PowerControllerForm parent)
        {
            this.mainForm = parent;
            this.allDevices = new List<DeviceItem>(parent.CurrentDevices);
            this.allSpaces = new List<string>(parent.CurrentSpaces);
            this.scheduleSettings = parent.CurrentScheduleSettings != null 
                ? parent.CurrentScheduleSettings 
                : new ScheduleSettings();

            this.Text = "⚙️ 통합 시스템 환경설정 (Global System Settings)";
            this.Size = new Size(680, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            InitializeSettingsUI();
            ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);
            SelectTab("General");
        }

        private void InitializeSettingsUI()
        {
            Font fontLabel = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);

            // 1. Modern Tab Header
            tabHeaderPanel = new FlowLayoutPanel();
            tabHeaderPanel.Location = new Point(15, 15);
            tabHeaderPanel.Size = new Size(635, 42);
            tabHeaderPanel.BackColor = Color.Transparent;
            tabHeaderPanel.WrapContents = false;

            btnTabGeneral = CreateTabHeaderButton("⚙️ 일반 / 테마", "General");
            btnTabSchedule = CreateTabHeaderButton("⏰ 자동 스케줄", "Schedule");
            btnTabDevices = CreateTabHeaderButton("🖥️ 장비 목록", "Devices");
            btnTabMaintenance = CreateTabHeaderButton("💾 백업 및 로그", "Maintenance");

            tabHeaderPanel.Controls.Add(btnTabGeneral);
            tabHeaderPanel.Controls.Add(btnTabSchedule);
            tabHeaderPanel.Controls.Add(btnTabDevices);
            tabHeaderPanel.Controls.Add(btnTabMaintenance);

            // 2. Content Container
            contentContainer = new Panel();
            contentContainer.Location = new Point(15, 60);
            contentContainer.Size = new Size(635, 385);
            contentContainer.BackColor = Color.Transparent;

            panelGeneral = new Panel() { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
            panelSchedule = new Panel() { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
            panelDevices = new Panel() { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
            panelMaintenance = new Panel() { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };

            InitGeneralTab(fontLabel, fontInput);
            InitScheduleTab(fontLabel, fontInput);
            InitDevicesTab(fontLabel, fontInput);
            InitMaintenanceTab(fontLabel, fontInput);

            contentContainer.Controls.Add(panelGeneral);
            contentContainer.Controls.Add(panelSchedule);
            contentContainer.Controls.Add(panelDevices);
            contentContainer.Controls.Add(panelMaintenance);

            // 3. Bottom Action Buttons
            btnSave = new Button()
            {
                Text = "💾 설정 일괄 저장 및 적용",
                Size = new Size(190, 38),
                Location = new Point(240, 465),
                BackColor = ColorTranslator.FromHtml("#1f8a65"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = FontHelper.GetFont(9.5f, FontStyle.Bold)
            };
            btnSave.Click += BtnSave_Click;

            btnClose = new Button()
            {
                Text = "✕ 닫기",
                Size = new Size(110, 38),
                Location = new Point(445, 465),
                BackColor = Color.FromArgb(35, 37, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = FontHelper.GetFont(9.5f, FontStyle.Bold)
            };
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(tabHeaderPanel);
            this.Controls.Add(contentContainer);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnClose);
        }

        private Button CreateTabHeaderButton(string text, string tabKey)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Tag = tabKey;
            btn.Size = new Size(150, 36);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.Font = FontHelper.GetFont(9f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Click += (s, e) => SelectTab(tabKey);
            return btn;
        }

        private void SelectTab(string tabKey)
        {
            panelGeneral.Visible = (tabKey == "General");
            panelSchedule.Visible = (tabKey == "Schedule");
            panelDevices.Visible = (tabKey == "Devices");
            panelMaintenance.Visible = (tabKey == "Maintenance");

            Button[] tabBtns = new Button[] { btnTabGeneral, btnTabSchedule, btnTabDevices, btnTabMaintenance };
            foreach (var b in tabBtns)
            {
                bool isSelected = (b.Tag != null && b.Tag.ToString() == tabKey);
                if (isSelected)
                {
                    b.BackColor = ColorTranslator.FromHtml("#1f8a65");
                    b.ForeColor = Color.White;
                    b.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#10b981");
                }
                else
                {
                    b.BackColor = ThemeManager.CardBgColor;
                    b.ForeColor = ThemeManager.MutedTextColor;
                    b.FlatAppearance.BorderColor = ThemeManager.BorderColorSoft;
                }
            }
        }

        private void InitGeneralTab(Font fontLabel, Font fontInput)
        {
            GroupBox gbTheme = new GroupBox()
            {
                Text = " 🎨 UI 테마 모드 설정 ",
                Size = new Size(625, 90),
                Location = new Point(5, 5),
                Font = fontLabel
            };

            rbDarkTheme = new RadioButton() { Text = "🌙 다크 모드 (Dark Theme - 고대비 어두운 테마)", Location = new Point(25, 28), AutoSize = true, Font = fontInput, Checked = ThemeManager.IsDark, Cursor = Cursors.Hand };
            rbDarkTheme.CheckedChanged += (s, e) => { 
                if (rbDarkTheme.Checked) 
                {
                    ThemeManager.ApplyThemeTo(this, true);
                    mainForm.SetDarkMode(true);
                    SelectTab("General");
                }
            };

            rbLightTheme = new RadioButton() { Text = "☀️ 라이트 모드 (Light Theme - 산뜻한 밝은 테마)", Location = new Point(25, 54), AutoSize = true, Font = fontInput, Checked = !ThemeManager.IsDark, Cursor = Cursors.Hand };
            rbLightTheme.CheckedChanged += (s, e) => { 
                if (rbLightTheme.Checked) 
                {
                    ThemeManager.ApplyThemeTo(this, false);
                    mainForm.SetDarkMode(false);
                    SelectTab("General");
                }
            };

            gbTheme.Controls.Add(rbDarkTheme);
            gbTheme.Controls.Add(rbLightTheme);

            GroupBox gbSystem = new GroupBox()
            {
                Text = " 🚀 시스템 동작 및 보안 설정 ",
                Size = new Size(625, 150),
                Location = new Point(5, 105),
                Font = fontLabel
            };

            chkAutoStart = new CheckBox()
            {
                Text = "윈도우 시작 시 PowerController 자동 실행 (Startup)",
                Location = new Point(25, 30),
                AutoSize = true,
                Font = fontInput,
                Checked = StartupManager.IsStartupEnabled(),
                Cursor = Cursors.Hand
            };

            chkConfirmAction = new CheckBox()
            {
                Text = "전체 전원 켜기/끄기 일괄 제어 시 안전 확인 팝업창 표시",
                Location = new Point(25, 65),
                AutoSize = true,
                Font = fontInput,
                Checked = mainForm.IsConfirmRequired,
                Cursor = Cursors.Hand
            };

            chkRealNetwork = new CheckBox()
            {
                Text = "실장비 네트워크 패킷 전송 활성화 (WOL / PJLink / TCP)",
                Location = new Point(25, 100),
                AutoSize = true,
                Font = fontInput,
                Checked = mainForm.IsRealNetworkControlMode,
                Cursor = Cursors.Hand
            };

            gbSystem.Controls.Add(chkAutoStart);
            gbSystem.Controls.Add(chkConfirmAction);
            gbSystem.Controls.Add(chkRealNetwork);

            panelGeneral.Controls.Add(gbTheme);
            panelGeneral.Controls.Add(gbSystem);
        }

        private void InitScheduleTab(Font fontLabel, Font fontInput)
        {
            GroupBox gbTimes = new GroupBox()
            {
                Text = " 요일별 전원 기동/종료 예약 시각 ",
                Size = new Size(625, 120),
                Location = new Point(5, 5),
                Font = fontLabel
            };

            int xL = 20; int xI = 160; int yStart = 30; int yGap = 42;

            Label lWd = new Label() { Text = "평일(화~금) 시간:", Location = new Point(xL, yStart), AutoSize = true };
            txtWeekdayStart = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(80, 22), Font = fontInput, Text = scheduleSettings.WeekdayStart };
            Label lWdWave = new Label() { Text = "~", Location = new Point(xI + 88, yStart - 1), AutoSize = true };
            txtWeekdayEnd = new TextBox() { Location = new Point(xI + 110, yStart - 3), Size = new Size(80, 22), Font = fontInput, Text = scheduleSettings.WeekdayEnd };

            Label lSat = new Label() { Text = "토요일 시간:", Location = new Point(xL, yStart + yGap), AutoSize = true };
            txtSaturdayStart = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(80, 22), Font = fontInput, Text = scheduleSettings.SaturdayStart };
            Label lSatWave = new Label() { Text = "~", Location = new Point(xI + 88, yStart + yGap - 1), AutoSize = true };
            txtSaturdayEnd = new TextBox() { Location = new Point(xI + 110, yStart + yGap - 3), Size = new Size(80, 22), Font = fontInput, Text = scheduleSettings.SaturdayEnd };

            gbTimes.Controls.Add(lWd); gbTimes.Controls.Add(txtWeekdayStart); gbTimes.Controls.Add(lWdWave); gbTimes.Controls.Add(txtWeekdayEnd);
            gbTimes.Controls.Add(lSat); gbTimes.Controls.Add(txtSaturdayStart); gbTimes.Controls.Add(lSatWave); gbTimes.Controls.Add(txtSaturdayEnd);

            GroupBox gbIgnore = new GroupBox()
            {
                Text = " 자동 기동 무조건 차단 요일 (Ignore Days) ",
                Size = new Size(625, 95),
                Location = new Point(5, 135),
                Font = fontLabel
            };

            chkMon = CreateDayCheck("월", "월요일", 20, 32);
            chkTue = CreateDayCheck("화", "화요일", 95, 32);
            chkWed = CreateDayCheck("수", "수요일", 170, 32);
            chkThu = CreateDayCheck("목", "목요일", 245, 32);
            chkFri = CreateDayCheck("금", "금요일", 320, 32);
            chkSat = CreateDayCheck("토", "토요일", 395, 32);
            chkSun = CreateDayCheck("일", "일요일", 470, 32);

            gbIgnore.Controls.Add(chkMon);
            gbIgnore.Controls.Add(chkTue);
            gbIgnore.Controls.Add(chkWed);
            gbIgnore.Controls.Add(chkThu);
            gbIgnore.Controls.Add(chkFri);
            gbIgnore.Controls.Add(chkSat);
            gbIgnore.Controls.Add(chkSun);

            chkAutoScheduleEnabled = new CheckBox()
            {
                Text = "⏰ 자동 스케줄 타이머 상시 활성화 (정해진 시간에 자동 전원 켜짐/꺼짐)",
                Location = new Point(15, 245),
                AutoSize = true,
                Font = fontLabel,
                ForeColor = ColorTranslator.FromHtml("#10b981"),
                Checked = mainForm.IsAutoScheduleActive,
                Cursor = Cursors.Hand
            };

            panelSchedule.Controls.Add(gbTimes);
            panelSchedule.Controls.Add(gbIgnore);
            panelSchedule.Controls.Add(chkAutoScheduleEnabled);
        }

        private CheckBox CreateDayCheck(string shortName, string fullName, int x, int y)
        {
            CheckBox chk = new CheckBox()
            {
                Text = shortName,
                Tag = fullName,
                Location = new Point(x, y),
                AutoSize = true,
                Font = FontHelper.GetFont(9.5f),
                Cursor = Cursors.Hand
            };
            if (scheduleSettings.IgnoreDays != null && scheduleSettings.IgnoreDays.Contains(fullName))
            {
                chk.Checked = true;
            }
            return chk;
        }

        private void InitDevicesTab(Font fontLabel, Font fontInput)
        {
            gridDevices = new DataGridView()
            {
                Location = new Point(5, 5),
                Size = new Size(625, 305),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = FontHelper.GetFont(9f)
            };
            gridDevices.EnableHeadersVisualStyles = false;

            gridDevices.Columns.Add("Id", "ID");
            gridDevices.Columns.Add("Name", "장비 이름");
            gridDevices.Columns.Add("Type", "유형");
            gridDevices.Columns.Add("Space", "공간");
            gridDevices.Columns.Add("Ip", "IP 주소");
            gridDevices.Columns.Add("Port", "포트");
            gridDevices.Columns.Add("Assoc", "귀속 부모");
            gridDevices.Columns.Add("Mode", "기동 모드");

            RefreshDeviceGrid();

            btnAddDevice = new Button() { Text = "➕ 신규 장치 등록", Location = new Point(5, 325), Size = new Size(140, 34), BackColor = ColorTranslator.FromHtml("#1f8a65"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontLabel, Cursor = Cursors.Hand };
            btnAddDevice.Click += BtnAddDevice_Click;

            btnEditDevice = new Button() { Text = "⚙️ 선택 장치 세부 수정", Location = new Point(155, 325), Size = new Size(160, 34), BackColor = Color.FromArgb(45, 47, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontLabel, Cursor = Cursors.Hand };
            btnEditDevice.Click += BtnEditDevice_Click;

            btnDeleteDevice = new Button() { Text = "🗑️ 선택 장치 삭제", Location = new Point(325, 325), Size = new Size(130, 34), BackColor = ColorTranslator.FromHtml("#dc2626"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontLabel, Cursor = Cursors.Hand };
            btnDeleteDevice.Click += BtnDeleteDevice_Click;

            panelDevices.Controls.Add(gridDevices);
            panelDevices.Controls.Add(btnAddDevice);
            panelDevices.Controls.Add(btnEditDevice);
            panelDevices.Controls.Add(btnDeleteDevice);
        }

        private void RefreshDeviceGrid()
        {
            gridDevices.Rows.Clear();
            foreach (var d in allDevices)
            {
                gridDevices.Rows.Add(
                    d.Id,
                    d.Name,
                    d.Type == "PC" ? "🖥️ PC" : "📹 프로젝터",
                    d.Space,
                    d.IpAddress,
                    d.Port,
                    string.IsNullOrEmpty(d.AssociatedDeviceId) ? "-" : d.AssociatedDeviceId,
                    d.PowerOnSequenceMode ?? "PC_FIRST"
                );
            }
        }

        private void BtnAddDevice_Click(object sender, EventArgs e)
        {
            using (DeviceAddForm addForm = new DeviceAddForm(this.allDevices, this.allSpaces))
            {
                if (addForm.ShowDialog(this) == DialogResult.OK && addForm.AddedDevice != null)
                {
                    allDevices.Add(addForm.AddedDevice);
                    RefreshDeviceGrid();
                }
            }
        }

        private void BtnEditDevice_Click(object sender, EventArgs e)
        {
            if (gridDevices.SelectedRows.Count == 0)
            {
                MessageBox.Show("수정할 장치를 목록에서 먼저 선택하세요.", "선택 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedId = gridDevices.SelectedRows[0].Cells["Id"].Value.ToString();
            DeviceItem targetDev = allDevices.Find(d => d.Id == selectedId);
            if (targetDev == null) return;

            if (targetDev.Type == "PC")
            {
                using (DeviceCompositeEditForm compForm = new DeviceCompositeEditForm(selectedId, allDevices, mainForm))
                {
                    compForm.ShowDialog(this);
                    RefreshDeviceGrid();
                }
            }
            else
            {
                using (DeviceEditForm editForm = new DeviceEditForm(targetDev, allDevices, allSpaces))
                {
                    if (editForm.ShowDialog(this) == DialogResult.OK)
                    {
                        RefreshDeviceGrid();
                    }
                }
            }
        }

        private void BtnDeleteDevice_Click(object sender, EventArgs e)
        {
            if (gridDevices.SelectedRows.Count == 0)
            {
                MessageBox.Show("삭제할 장치를 목록에서 먼저 선택하세요.", "선택 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedId = gridDevices.SelectedRows[0].Cells["Id"].Value.ToString();
            DeviceItem targetDev = allDevices.Find(d => d.Id == selectedId);
            if (targetDev == null) return;

            if (MessageBox.Show(string.Format("정말 장비 '{0}'(ID: {1})를 삭제하시겠습니까?", targetDev.Name, targetDev.Id), "장치 삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                allDevices.Remove(targetDev);
                foreach (var d in allDevices)
                {
                    if (d.AssociatedDeviceId == selectedId) d.AssociatedDeviceId = "";
                }
                RefreshDeviceGrid();
            }
        }

        private void InitMaintenanceTab(Font fontLabel, Font fontInput)
        {
            GroupBox gbBackup = new GroupBox()
            {
                Text = " 💾 설정 파일(devices.json) 백업 및 복원 ",
                Size = new Size(625, 120),
                Location = new Point(5, 5),
                Font = fontLabel
            };

            btnBackupConfig = new Button() { Text = "📤 설정 파일 내보내기 (Backup)", Location = new Point(25, 40), Size = new Size(220, 38), BackColor = Color.FromArgb(45, 47, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontLabel, Cursor = Cursors.Hand };
            btnBackupConfig.Click += BtnBackupConfig_Click;

            btnRestoreConfig = new Button() { Text = "📥 백업 파일 불러오기 (Restore)", Location = new Point(265, 40), Size = new Size(220, 38), BackColor = ColorTranslator.FromHtml("#1f8a65"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontLabel, Cursor = Cursors.Hand };
            btnRestoreConfig.Click += BtnRestoreConfig_Click;

            gbBackup.Controls.Add(btnBackupConfig);
            gbBackup.Controls.Add(btnRestoreConfig);

            GroupBox gbLogs = new GroupBox()
            {
                Text = " 📋 실시간 시스템 로그 관리 ",
                Size = new Size(625, 120),
                Location = new Point(5, 135),
                Font = fontLabel
            };

            btnExportLogs = new Button() { Text = "📄 현재 로그 텍스트 파일 저장", Location = new Point(25, 40), Size = new Size(220, 38), BackColor = Color.FromArgb(45, 47, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontLabel, Cursor = Cursors.Hand };
            btnExportLogs.Click += BtnExportLogs_Click;

            btnClearLogs = new Button() { Text = "🧹 로그 기록 전체 비우기", Location = new Point(265, 40), Size = new Size(220, 38), BackColor = ColorTranslator.FromHtml("#dc2626"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontLabel, Cursor = Cursors.Hand };
            btnClearLogs.Click += (s, e) => { mainForm.ClearLogHistory(); MessageBox.Show("로그 창이 깨끗하게 초기화되었습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information); };

            gbLogs.Controls.Add(btnExportLogs);
            gbLogs.Controls.Add(btnClearLogs);

            panelMaintenance.Controls.Add(gbBackup);
            panelMaintenance.Controls.Add(gbLogs);
        }

        private void BtnBackupConfig_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON Files (*.json)|*.json";
                sfd.FileName = string.Format("devices_backup_{0}.json", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        DeviceConfig wrap = new DeviceConfig() { Spaces = allSpaces, Devices = allDevices, Schedules = scheduleSettings };
                        string json = new JavaScriptSerializer().Serialize(wrap);
                        File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                        MessageBox.Show("성공적으로 설정 파일이 백업되었습니다.", "백업 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("백업 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnRestoreConfig_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON Files (*.json)|*.json";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        DeviceConfig wrap = new JavaScriptSerializer().Deserialize<DeviceConfig>(json);
                        if (wrap != null && wrap.Devices != null)
                        {
                            this.allDevices = wrap.Devices;
                            if (wrap.Spaces != null) this.allSpaces = wrap.Spaces;
                            if (wrap.Schedules != null) this.scheduleSettings = wrap.Schedules;
                            RefreshDeviceGrid();
                            MessageBox.Show("백업 파일에서 설정을 성공적으로 불러왔습니다.\n하단의 '설정 일괄 저장 및 적용'을 누르면 즉시 전체 시스템에 반영됩니다.", "불러오기 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("복원 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnExportLogs_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text Files (*.txt)|*.txt";
                sfd.FileName = string.Format("powercontroller_log_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(sfd.FileName, mainForm.GetLogText(), Encoding.UTF8);
                        MessageBox.Show("로그 텍스트가 성공적으로 내보내기되었습니다.", "내보내기 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("로그 저장 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // 1. Validate Schedule Times
            if (!IsValidTime(txtWeekdayStart.Text) || !IsValidTime(txtWeekdayEnd.Text) ||
                !IsValidTime(txtSaturdayStart.Text) || !IsValidTime(txtSaturdayEnd.Text))
            {
                MessageBox.Show("스케줄 시간 형식이 올바르지 않습니다. (예: 09:30, 18:30)", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SelectTab("Schedule");
                return;
            }

            // 2. Apply Theme
            bool newIsDark = rbDarkTheme.Checked;
            if (newIsDark != ThemeManager.IsDark)
            {
                mainForm.SetDarkMode(newIsDark);
            }

            // 3. Apply Startup
            if (chkAutoStart.Checked != StartupManager.IsStartupEnabled())
            {
                StartupManager.SetStartup(chkAutoStart.Checked);
            }

            // 4. Apply Safety & Network
            mainForm.IsConfirmRequired = chkConfirmAction.Checked;
            mainForm.IsRealNetworkControlMode = chkRealNetwork.Checked;
            mainForm.IsAutoScheduleActive = chkAutoScheduleEnabled.Checked;

            // 5. Update Schedule
            scheduleSettings.WeekdayStart = txtWeekdayStart.Text.Trim();
            scheduleSettings.WeekdayEnd = txtWeekdayEnd.Text.Trim();
            scheduleSettings.SaturdayStart = txtSaturdayStart.Text.Trim();
            scheduleSettings.SaturdayEnd = txtSaturdayEnd.Text.Trim();

            scheduleSettings.IgnoreDays.Clear();
            List<CheckBox> chks = new List<CheckBox> { chkMon, chkTue, chkWed, chkThu, chkFri, chkSat, chkSun };
            foreach (var chk in chks)
            {
                if (chk.Checked) scheduleSettings.IgnoreDays.Add(chk.Tag.ToString());
            }

            // 6. Save to devices.json
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            try
            {
                DeviceConfig wrap = new DeviceConfig()
                {
                    Spaces = allSpaces,
                    Devices = allDevices,
                    Schedules = scheduleSettings
                };
                string json = new JavaScriptSerializer().Serialize(wrap);
                File.WriteAllText(configPath, json, Encoding.UTF8);

                mainForm.ApplyScheduleSettings(scheduleSettings);
                mainForm.ApplyUpdatedDevices(allDevices, allSpaces);

                MessageBox.Show("모든 환경설정이 성공적으로 저장 및 적용되었습니다.", "설정 저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsValidTime(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr)) return false;
            string[] parts = timeStr.Split(':');
            if (parts.Length != 2) return false;
            int hh, mm;
            if (!int.TryParse(parts[0], out hh) || !int.TryParse(parts[1], out mm)) return false;
            return (hh >= 0 && hh <= 23 && mm >= 0 && mm <= 59);
        }
    }
}
