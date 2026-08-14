using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ShowroomPowerController
{
    public class PowerControllerForm : Form
    {
        private List<DeviceItem> devices = new List<DeviceItem>();
        private List<string> spaces = new List<string>();
        private ScheduleSettings scheduleSettings = new ScheduleSettings(); 

        private string selectedSpaceTab = "ALL";

        private Panel titlePanel;
        private Label titleLabel;
        private Button btnClose;
        private Button btnMinimize;
        private Label statusSummaryLabel;

        private Panel schedulePanel;
        private CheckBox autoScheduleCheckBox;
        private Label nextScheduleLabel;
        private Label virtualDayLabel;
        private ComboBox virtualDayComboBox;
        private Button triggerScheduleTestBtn;
        private Button btnGlobalSettings;
        private bool isDarkMode = true;
        private string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        // 공간별 통합 제어반
        private DoubleBufferedPanel zoneControlGroup;
        private FlowLayoutPanel tabFlowPanel;
        private Button zoneOnBtn;
        private Button zoneOffBtn;
        private Button addSpaceBtn;
        private Button deleteSpaceBtn;

        private DoubleBufferedPanel dashboardGroup;
        private FlowLayoutPanel cardContainerPanel;

        private DoubleBufferedPanel logGroup;
        public class LogHistoryItem
        {
            public string Timestamp { get; set; }
            public string Message { get; set; }
            public LogType Type { get; set; }
        }

        public enum LogType
        {
            Normal,
            Success,
            Error
        }

        private List<LogHistoryItem> logHistory = new List<LogHistoryItem>();
        private RichTextBox logRichTextBox;

        // WOL 자동 재시도 큐 관련 데이터 구조 및 리스트
        public class WolRetryItem
        {
            public string DeviceId { get; set; }
            public DateTime FirstSentTime { get; set; }
            public DateTime LastSentTime { get; set; }
            public int RetryCount { get; set; }
        }

        private List<WolRetryItem> wolRetryList = new List<WolRetryItem>();
        private readonly object wolRetryLock = new object();

        private System.Windows.Forms.Timer monitoringTimer;
        private bool isSimulatingSequence = false;

        private bool isDragging = false;
        private Point dragCursorPoint;
        private Point FormDragPoint;

        // System Tray Components
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private bool isExiting = false;

        // UDP Heartbeat 리스너 필드
        private UdpClient heartbeatUdpListener;
        private bool isHeartbeatListenerRunning = true;
        private int heartbeatReceivePort = 9998;

        public bool IsRunningSimulation { get { return isSimulatingSequence; } }
        public List<DeviceItem> CurrentDevices { get { return devices; } }
        public List<string> CurrentSpaces { get { return spaces; } }
        public ScheduleSettings CurrentScheduleSettings { get { return scheduleSettings; } }
        public bool IsRealNetworkControlMode { get; set; }

        public PowerControllerForm()
        {
            this.Text = "시연실 통합 전원 제어반 (v1.0.0)";
            this.Size = new Size(1200, 950);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            // 자체 내장 아이콘 추출하여 폼 아이콘으로 이식
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch { }

            LoadThemeSettings();
            LoadDevicesConfig();
            InitializeComponent();
            ThemeManager.ApplyThemeTo(this, isDarkMode);
            InitializeTrayIcon();
            PopulateSpaces();
            PopulateVirtualDays();
            RefreshVisualDashboard();
            UpdateScheduleText(); 

            this.FormClosing += PowerControllerForm_FormClosing;

            monitoringTimer = new System.Windows.Forms.Timer();
            monitoringTimer.Interval = 5000;
            monitoringTimer.Tick += (s, e) => RunMonitoringTick();
            monitoringTimer.Start();

            StartHeartbeatListener();

            AppendLog("시연실 통합 전원 제어 프로그램(v1.0.0 정식 버전)이 기동되었습니다.");
            AppendLog("실제 원격 전원 네트워크 제어(WOL 및 TCP)가 상시 작동 중입니다.");
        }

        private void StartHeartbeatListener()
        {
            System.Threading.ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    heartbeatUdpListener = new UdpClient(heartbeatReceivePort);
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                    while (isHeartbeatListenerRunning)
                    {
                        byte[] data = heartbeatUdpListener.Receive(ref remoteEP);
                        if (data != null && data.Length > 0)
                        {
                            string message = Encoding.UTF8.GetString(data).Trim();
                            if (message.StartsWith("HEARTBEAT:"))
                            {
                                string targetId = message.Substring(10).Trim();
                                
                                var dev = devices.Find(d => d.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));
                                if (dev != null)
                                {
                                    dev.LastActiveTime = DateTime.Now;
                                    
                                    bool isRecentlyShutdown = (DateTime.Now - dev.LastShutdownTime).TotalSeconds < 5;
                                    if ((dev.RuntimeStatus == "FREEZE" || dev.RuntimeStatus == "OFFLINE") && !isRecentlyShutdown)
                                    {
                                        dev.RuntimeStatus = "ONLINE";
                                        AppendLog(string.Format("💚 [네트워크 복구] '{0}' 기기로부터 Heartbeat 정상 수신 재개 ➡️ 상태: ONLINE", dev.Name));
                                        RefreshVisualDashboard();
                                    }
                                }
                            }
                            else if (message.StartsWith("EXIT:"))
                            {
                                string targetId = message.Substring(5).Trim();
                                var dev = devices.Find(d => d.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));
                                if (dev != null)
                                {
                                    if (dev.RuntimeStatus != "OFFLINE")
                                    {
                                        dev.RuntimeStatus = "OFFLINE";
                                        AppendLog(string.Format("💤 [정상 종료] '{0}' 기기로부터 정상 종료 신호(EXIT) 수신 ➡️ 상태: OFFLINE", dev.Name));
                                        RefreshVisualDashboard();
                                    }
                                }
                            }
                            else if (message.StartsWith("PING_TEST:"))
                            {
                                // 형식: PING_TEST:<DeviceId>:<ResponsePort>
                                string[] parts = message.Split(':');
                                if (parts.Length >= 3)
                                {
                                    string testId = parts[1].Trim();
                                    int responsePort = 9997;
                                    int.TryParse(parts[2], out responsePort);

                                    // PONG 패킷 송출
                                    UdpClient pongClient = null;
                                    try
                                    {
                                        pongClient = new UdpClient();
                                        byte[] pongBytes = Encoding.UTF8.GetBytes("PONG_TEST:" + testId);
                                        pongClient.Send(pongBytes, pongBytes.Length, remoteEP.Address.ToString(), responsePort);
                                        AppendLog(string.Format("📡 [네트워크 테스트] '{0}' 기기(IP: {1})로부터 1회성 PING 수신 ➡️ PONG 응답 전송 완료 (포트 {2})", testId, remoteEP.Address.ToString(), responsePort));
                                    }
                                    catch (Exception ex)
                                    {
                                        AppendLog(string.Format("⚠️ [네트워크 테스트] PONG 전송 실패: {0}", ex.Message));
                                    }
                                    finally
                                    {
                                        if (pongClient != null) pongClient.Close();
                                    }

                                    string senderIp = remoteEP.Address.ToString();
                                    bool isRegistered = devices.Exists(d => d.Id.Equals(testId, StringComparison.OrdinalIgnoreCase) || d.IpAddress == senderIp);

                                    this.BeginInvoke((MethodInvoker)delegate
                                    {
                                        try
                                        {
                                            Color toastColor = isRegistered ? Color.CornflowerBlue : Color.DarkOrange;
                                            string toastMsg = isRegistered
                                                ? string.Format("📡 기기 [{0}] (IP: {1}) 연결 테스트 요청 수신", testId, senderIp)
                                                : string.Format("⚠️ 미등록 기기 [{0}] (IP: {1}) 연결 시도 중", testId, senderIp);

                                            ToastForm toast = new ToastForm(toastMsg, toastColor, testId, senderIp, isRegistered, (newId, newIp) =>
                                            {
                                                OpenDeviceAddFormWithPreset(newId, newIp);
                                            });
                                            toast.Show();
                                        }
                                        catch { }
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        private void InitializeTrayIcon()
        {
            try
            {
                trayMenu = new ContextMenu();
                
                MenuItem menuOpen = new MenuItem("대시보드 열기", (s, e) => {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.Activate();
                });
                menuOpen.DefaultItem = true;
                
                MenuItem menuExit = new MenuItem("프로그램 종료", (s, e) => {
                    isExiting = true;
                    Application.Exit();
                });

                trayMenu.MenuItems.Add(menuOpen);
                trayMenu.MenuItems.Add("-"); // 구분선
                trayMenu.MenuItems.Add(menuExit);

                trayIcon = new NotifyIcon();
                trayIcon.Text = "Showroom Power Controller";
                
                try
                {
                    trayIcon.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
                catch
                {
                    trayIcon.Icon = SystemIcons.Application;
                }

                trayIcon.ContextMenu = trayMenu;
                trayIcon.Visible = true;

                trayIcon.DoubleClick += (s, e) => {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.Activate();
                };
            }
            catch (Exception ex)
            {
                AppendLog("트레이 아이콘 초기화 실패: " + ex.Message);
            }
        }

        private void PowerControllerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isExiting)
            {
                e.Cancel = true;
                this.Hide();
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(3000, "Power Controller", "전원제어기가 시스템 트레이(백그라운드)에서 계속 스케줄을 처리 중입니다.", ToolTipIcon.Info);
                    }
                }
                catch { }
            }
            else
            {
                try
                {
                    isHeartbeatListenerRunning = false;
                    if (heartbeatUdpListener != null) heartbeatUdpListener.Close();
                }
                catch { }

                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.Q))
            {
                isExiting = true;
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void LoadDevicesConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            if (!File.Exists(configPath))
            {
                string dummyJson = @"{
  ""Spaces"": [
    ""테스트 전시실"",
    ""세미나실""
  ],
  ""Devices"": [
    { ""Id"": ""PC_01"", ""Name"": ""원격 대상 PC A"", ""Type"": ""PC"", ""IpAddress"": ""192.168.0.11"", ""MacAddress"": ""00-11-22-33-44-55"", ""Port"": 9999, ""Space"": ""테스트 전시실"", ""AssociatedDeviceId"": ""Proj_01"", ""BootOrder"": 2, ""BootDelaySeconds"": 5 },
    { ""Id"": ""Proj_01"", ""Name"": ""원격 프로젝터 A"", ""Type"": ""Projector"", ""IpAddress"": ""192.168.0.12"", ""MacAddress"": """", ""Port"": 4352, ""Space"": ""테스트 전시실"", ""AssociatedDeviceId"": """", ""BootOrder"": 1, ""BootDelaySeconds"": 10 },
    { ""Id"": ""PC_02"", ""Name"": ""원격 대상 PC B"", ""Type"": ""PC"", ""IpAddress"": ""192.168.0.13"", ""MacAddress"": ""00-11-22-33-44-66"", ""Port"": 9999, ""Space"": ""테스트 전시실"", ""AssociatedDeviceId"": """", ""BootOrder"": 1, ""BootDelaySeconds"": 0 }
  ],
  ""Schedules"": {
    ""WeekdayStart"": ""08:50"",
    ""WeekdayEnd"": ""18:10"",
    ""SaturdayStart"": ""09:50"",
    ""SaturdayEnd"": ""15:10"",
    ""IgnoreDays"": [""월요일"", ""일요일""]
  }
}";
                try { File.WriteAllText(configPath, dummyJson, Encoding.UTF8); } catch { }
            }

            try
            {
                string jsonText = File.ReadAllText(configPath, Encoding.UTF8);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                DeviceConfig config = serializer.Deserialize<DeviceConfig>(jsonText);
                if (config != null)
                {
                    if (config.Devices != null) devices = config.Devices;
                    
                    if (config.Spaces != null)
                    {
                        spaces = config.Spaces;
                    }
                    else
                    {
                        spaces = new List<string>();
                        foreach (var d in devices)
                        {
                            if (!spaces.Contains(d.Space)) spaces.Add(d.Space);
                        }
                    }

                    if (config.Schedules != null)
                    {
                        scheduleSettings = config.Schedules;
                    }
                    else
                    {
                        scheduleSettings = new ScheduleSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("설정 파일을 로드하는 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadThemeSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath, Encoding.UTF8);
                    var serializer = new JavaScriptSerializer();
                    var dict = serializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null && dict.ContainsKey("themeMode"))
                    {
                        isDarkMode = dict["themeMode"].ToLower() == "dark";
                    }
                }
            }
            catch { }
        }

        private void SaveThemeSettings()
        {
            try
            {
                var dict = new Dictionary<string, string>();
                dict["themeMode"] = isDarkMode ? "dark" : "light";
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(dict);
                File.WriteAllText(settingsFilePath, json, Encoding.UTF8);
            }
            catch { }
        }

        private void InitializeComponent()
        {
            Color launcherBg = Color.FromArgb(18, 19, 28);
            Color titleBarBg = Color.FromArgb(24, 25, 38);
            Color cardContainerBg = Color.FromArgb(18, 19, 28);
            Color buttonBaseBg = Color.FromArgb(35, 37, 54);
            Color textGray = Color.FromArgb(156, 163, 175);

            this.BackColor = launcherBg;

            // 1. Custom Title Panel
            titlePanel = new Panel();
            titlePanel.Name = "titlePanel";
            titlePanel.Location = new Point(0, 0);
            titlePanel.Size = new Size(1200, 60);
            titlePanel.BackColor = titleBarBg;
            titlePanel.MouseDown += TitlePanel_MouseDown;
            titlePanel.MouseMove += TitlePanel_MouseMove;
            titlePanel.MouseUp += TitlePanel_MouseUp;

            titleLabel = new Label();
            titleLabel.Name = "titleLabel";
            titleLabel.Text = "⚡   시연실 통합 전원 제어반   |   [정식 운영 빌드 v1.0.0]";
            titleLabel.ForeColor = ColorTranslator.FromHtml("#f54e00");
            titleLabel.Font = FontHelper.GetFont(12f, FontStyle.Bold);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(20, 18);
            titleLabel.MouseDown += TitlePanel_MouseDown;
            titleLabel.MouseMove += TitlePanel_MouseMove;
            titleLabel.MouseUp += TitlePanel_MouseUp;

            statusSummaryLabel = new Label();
            statusSummaryLabel.Text = "가동 상황: 0대 실행 중 / 총 0대";
            statusSummaryLabel.ForeColor = textGray;
            statusSummaryLabel.Font = FontHelper.GetFont(9.5f, FontStyle.Bold);
            statusSummaryLabel.AutoSize = true;
            statusSummaryLabel.Location = new Point(580, 22);

            btnClose = new Button();
            btnClose.Text = "✕";
            btnClose.Font = FontHelper.GetFont(11f, FontStyle.Bold);
            btnClose.ForeColor = textGray;
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#cf2d56");
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(185, 28, 28);
            btnClose.Size = new Size(45, 60);
            btnClose.Location = new Point(1155, 0);
            btnClose.Click += (s, e) => this.Close();

            btnMinimize = new Button();
            btnMinimize.Text = "—";
            btnMinimize.Font = FontHelper.GetFont(10f, FontStyle.Bold);
            btnMinimize.ForeColor = textGray;
            btnMinimize.BackColor = Color.Transparent;
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 255, 255, 10);
            btnMinimize.Size = new Size(45, 60);
            btnMinimize.Location = new Point(1110, 0);
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            btnGlobalSettings = new Button();
            btnGlobalSettings.Text = "⚙️";
            btnGlobalSettings.Font = new Font("Segoe UI Emoji", 12f, FontStyle.Bold);
            btnGlobalSettings.ForeColor = textGray;
            btnGlobalSettings.BackColor = Color.Transparent;
            btnGlobalSettings.FlatStyle = FlatStyle.Flat;
            btnGlobalSettings.FlatAppearance.BorderSize = 0;
            btnGlobalSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 255, 255, 10);
            btnGlobalSettings.Size = new Size(45, 60);
            btnGlobalSettings.Location = new Point(1065, 0);
            btnGlobalSettings.Cursor = Cursors.Hand;
            btnGlobalSettings.Click += (s, e) => OpenGlobalSettingsDialog();

            titlePanel.Controls.Add(titleLabel);
            titlePanel.Controls.Add(statusSummaryLabel);
            titlePanel.Controls.Add(btnClose);
            titlePanel.Controls.Add(btnMinimize);
            titlePanel.Controls.Add(btnGlobalSettings);

            // 2. Scheduler Panel
            schedulePanel = new DoubleBufferedPanel();
            schedulePanel.Size = new Size(1152, 75);
            schedulePanel.Location = new Point(24, 78);
            schedulePanel.BackColor = Color.Transparent;
            schedulePanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 8;
                    path.AddArc(0, 0, radius, radius, 180, 90);
                    path.AddArc(schedulePanel.Width - radius - 1, 0, radius, radius, 270, 90);
                    path.AddArc(schedulePanel.Width - radius - 1, schedulePanel.Height - radius - 1, radius, radius, 0, 90);
                    path.AddArc(0, schedulePanel.Height - radius - 1, radius, radius, 90, 90);
                    path.CloseAllFigures();

                    using (SolidBrush fillBrush = new SolidBrush(ThemeManager.CardBgColor))
                    {
                        e.Graphics.FillPath(fillBrush, path);
                    }
                    using (Pen borderPen = new Pen(ThemeManager.BorderColorSoft, 1f))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }
            };

            autoScheduleCheckBox = new CheckBox();
            autoScheduleCheckBox.Text = "스케줄 자동 제어 활성화 (Auto Scheduler)";
            autoScheduleCheckBox.ForeColor = Color.White;
            autoScheduleCheckBox.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            autoScheduleCheckBox.Checked = true;
            autoScheduleCheckBox.AutoSize = true;
            autoScheduleCheckBox.Location = new Point(20, 13);
            autoScheduleCheckBox.BackColor = Color.Transparent;
            autoScheduleCheckBox.CheckedChanged += AutoScheduleCheckBox_CheckedChanged;

            nextScheduleLabel = new Label();
            nextScheduleLabel.ForeColor = ColorTranslator.FromHtml("#f54e00");
            nextScheduleLabel.Font = FontHelper.GetFont(8.5f, FontStyle.Regular);
            nextScheduleLabel.AutoSize = true;
            nextScheduleLabel.BackColor = Color.Transparent;
            nextScheduleLabel.Location = new Point(20, 45);

            virtualDayLabel = new Label();
            virtualDayLabel.Text = "가상 요일:";
            virtualDayLabel.ForeColor = Color.White;
            virtualDayLabel.Font = FontHelper.GetFont(9f, FontStyle.Bold);
            virtualDayLabel.AutoSize = true;
            virtualDayLabel.BackColor = Color.Transparent;
            virtualDayLabel.Location = new Point(780, 17);

            virtualDayComboBox = new ComboBox();
            virtualDayComboBox.Size = new Size(130, 25);
            virtualDayComboBox.Location = new Point(855, 13);
            virtualDayComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            virtualDayComboBox.BackColor = Color.FromArgb(28, 29, 43);
            virtualDayComboBox.ForeColor = Color.White;
            virtualDayComboBox.FlatStyle = FlatStyle.Flat;
            virtualDayComboBox.Font = FontHelper.GetFont(9f);

            triggerScheduleTestBtn = new Button();
            triggerScheduleTestBtn.Text = "⚡ 가상 예약 트리거";
            triggerScheduleTestBtn.Size = new Size(145, 30);
            triggerScheduleTestBtn.Location = new Point(995, 10);
            triggerScheduleTestBtn.BackColor = Color.FromArgb(35, 37, 54);
            triggerScheduleTestBtn.ForeColor = Color.White;
            triggerScheduleTestBtn.FlatStyle = FlatStyle.Flat;
            triggerScheduleTestBtn.FlatAppearance.BorderSize = 1;
            triggerScheduleTestBtn.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 70);
            triggerScheduleTestBtn.Font = FontHelper.GetFont(9f, FontStyle.Bold);
            triggerScheduleTestBtn.Cursor = Cursors.Hand;
            triggerScheduleTestBtn.Click += TriggerScheduleTestBtn_Click;

            schedulePanel.Controls.Add(autoScheduleCheckBox);
            schedulePanel.Controls.Add(nextScheduleLabel);
            schedulePanel.Controls.Add(virtualDayLabel);
            schedulePanel.Controls.Add(virtualDayComboBox);
            schedulePanel.Controls.Add(triggerScheduleTestBtn);

            // 3. 공간별 통합 제어반 (DoubleBufferedPanel을 활용한 꽉 찬 박스 디자인)
            zoneControlGroup = new DoubleBufferedPanel();
            zoneControlGroup.Size = new Size(1152, 120);
            zoneControlGroup.Location = new Point(24, 168);
            zoneControlGroup.Paint += (s, e) =>
            {
                using (SolidBrush bgBrush = new SolidBrush(ThemeManager.CardBgColor))
                using (Pen borderPen = new Pen(ThemeManager.BorderColorSoft, 1.5f))
                {
                    e.Graphics.FillRectangle(bgBrush, 0, 0, zoneControlGroup.Width, zoneControlGroup.Height);
                    e.Graphics.DrawRectangle(borderPen, 0, 0, zoneControlGroup.Width - 1, zoneControlGroup.Height - 1);
                }
            };

            Label zoneTitleLabel = new Label();
            zoneTitleLabel.Text = "공간별 통합 제어반 (Tab Zone Control)";
            zoneTitleLabel.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            zoneTitleLabel.ForeColor = ThemeManager.TextColor;
            zoneTitleLabel.BackColor = Color.Transparent;
            zoneTitleLabel.AutoSize = true;
            zoneTitleLabel.Location = new Point(20, 15);
            zoneControlGroup.Controls.Add(zoneTitleLabel);

            // 공간 추가/삭제 버튼을 타이틀 옆에 다이렉트 배치
            addSpaceBtn = new Button();
            addSpaceBtn.Text = "＋";
            addSpaceBtn.Size = new Size(26, 22);
            addSpaceBtn.Location = new Point(290, 13);
            addSpaceBtn.FlatStyle = FlatStyle.Flat;
            addSpaceBtn.FlatAppearance.BorderSize = 1;
            addSpaceBtn.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#f54e00");
            addSpaceBtn.BackColor = Color.FromArgb(24, 25, 38);
            addSpaceBtn.ForeColor = ColorTranslator.FromHtml("#f54e00");
            addSpaceBtn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            addSpaceBtn.Cursor = Cursors.Hand;
            addSpaceBtn.Click += AddSpaceTabBtn_Click;
            zoneControlGroup.Controls.Add(addSpaceBtn);

            deleteSpaceBtn = new Button();
            deleteSpaceBtn.Text = "－";
            deleteSpaceBtn.Size = new Size(26, 22);
            deleteSpaceBtn.Location = new Point(322, 13);
            deleteSpaceBtn.FlatStyle = FlatStyle.Flat;
            deleteSpaceBtn.FlatAppearance.BorderSize = 1;
            deleteSpaceBtn.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#cf2d56");
            deleteSpaceBtn.BackColor = Color.FromArgb(24, 25, 38);
            deleteSpaceBtn.ForeColor = ColorTranslator.FromHtml("#cf2d56");
            deleteSpaceBtn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            deleteSpaceBtn.Cursor = Cursors.Hand;
            deleteSpaceBtn.Click += DeleteSpaceTabBtn_Click;
            zoneControlGroup.Controls.Add(deleteSpaceBtn);

            tabFlowPanel = new FlowLayoutPanel();
            tabFlowPanel.Size = new Size(580, 60);
            tabFlowPanel.Location = new Point(20, 48);
            tabFlowPanel.BackColor = Color.Transparent;
            tabFlowPanel.FlowDirection = FlowDirection.LeftToRight;
            tabFlowPanel.WrapContents = true;

            zoneOnBtn = new Button();
            zoneOnBtn.Text = "◀  전체 켜기";
            zoneOnBtn.Size = new Size(240, 52);
            zoneOnBtn.Location = new Point(620, 40);
            zoneOnBtn.BackColor = ColorTranslator.FromHtml("#f54e00");
            zoneOnBtn.ForeColor = Color.White;
            zoneOnBtn.FlatStyle = FlatStyle.Flat;
            zoneOnBtn.FlatAppearance.BorderSize = 0;
            zoneOnBtn.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            zoneOnBtn.Cursor = Cursors.Hand;
            zoneOnBtn.Click += ZoneOnBtn_Click;

            zoneOffBtn = new Button();
            zoneOffBtn.Text = "⏹️  전체 끄기";
            zoneOffBtn.Size = new Size(240, 52);
            zoneOffBtn.Location = new Point(880, 40);
            zoneOffBtn.BackColor = ColorTranslator.FromHtml("#cf2d56");
            zoneOffBtn.ForeColor = Color.White;
            zoneOffBtn.FlatStyle = FlatStyle.Flat;
            zoneOffBtn.FlatAppearance.BorderSize = 0;
            zoneOffBtn.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            zoneOffBtn.Cursor = Cursors.Hand;
            zoneOffBtn.Click += ZoneOffBtn_Click;

            zoneControlGroup.Controls.Add(tabFlowPanel);
            zoneControlGroup.Controls.Add(zoneOnBtn);
            zoneControlGroup.Controls.Add(zoneOffBtn);

            // 4. Dashboard Group (DoubleBufferedPanel을 활용한 꽉 찬 박스 디자인)
            dashboardGroup = new DoubleBufferedPanel();
            dashboardGroup.Size = new Size(1152, 440);
            dashboardGroup.Location = new Point(24, 305);
            dashboardGroup.Paint += (s, e) =>
            {
                using (SolidBrush bgBrush = new SolidBrush(ThemeManager.CardBgColor))
                using (Pen borderPen = new Pen(ThemeManager.BorderColorSoft, 1.5f))
                {
                    e.Graphics.FillRectangle(bgBrush, 0, 0, dashboardGroup.Width, dashboardGroup.Height);
                    e.Graphics.DrawRectangle(borderPen, 0, 0, dashboardGroup.Width - 1, dashboardGroup.Height - 1);
                }
            };

            Label dashTitleLabel = new Label();
            dashTitleLabel.Text = "3. 장비 현황 모니터 (비주얼 카드 뷰)";
            dashTitleLabel.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            dashTitleLabel.ForeColor = ThemeManager.TextColor;
            dashTitleLabel.BackColor = Color.Transparent;
            dashTitleLabel.AutoSize = true;
            dashTitleLabel.Location = new Point(20, 15);
            dashboardGroup.Controls.Add(dashTitleLabel);

            cardContainerPanel = new FlowLayoutPanel();
            cardContainerPanel.Size = new Size(1112, 380);
            cardContainerPanel.Location = new Point(20, 48);
            cardContainerPanel.AutoScroll = true;
            cardContainerPanel.BackColor = Color.FromArgb(18, 19, 28);
            cardContainerPanel.FlowDirection = FlowDirection.LeftToRight;
            cardContainerPanel.WrapContents = true;
            cardContainerPanel.Padding = new Padding(10);

            dashboardGroup.Controls.Add(cardContainerPanel);

            // 5. Simulation Log Group
            logGroup = new DoubleBufferedPanel();
            logGroup.Paint += (s, e) =>
            {
                using (SolidBrush bgBrush = new SolidBrush(ThemeManager.CardBgColor))
                using (Pen borderPen = new Pen(ThemeManager.BorderColorSoft, 1.5f))
                {
                    e.Graphics.FillRectangle(bgBrush, 0, 0, logGroup.Width, logGroup.Height);
                    e.Graphics.DrawRectangle(borderPen, 0, 0, logGroup.Width - 1, logGroup.Height - 1);
                }
            };

            Label logTitleLabel = new Label();
            logTitleLabel.Text = "실시간 로그";
            logTitleLabel.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            logTitleLabel.ForeColor = ThemeManager.TextColor;
            logTitleLabel.BackColor = Color.Transparent;
            logTitleLabel.AutoSize = true;
            logTitleLabel.Location = new Point(20, 15);
            logGroup.Controls.Add(logTitleLabel);
            logGroup.Size = new Size(1152, 165);
            logGroup.Location = new Point(24, 760);


            logRichTextBox = new RichTextBox();
            logRichTextBox.Text = "";
            logRichTextBox.Multiline = true;
            logRichTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            logRichTextBox.Size = new Size(1112, 110);
            logRichTextBox.Location = new Point(20, 42);
            logRichTextBox.BackColor = Color.FromArgb(18, 19, 28);
            logRichTextBox.ForeColor = Color.FromArgb(243, 244, 246);
            logRichTextBox.BorderStyle = BorderStyle.None;
            logRichTextBox.Font = new Font("Consolas", 9f);
            logRichTextBox.ReadOnly = true;

            logGroup.Controls.Add(logRichTextBox);

            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(ThemeManager.BorderColor, 1.5f))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            this.Controls.Add(titlePanel);
            this.Controls.Add(schedulePanel);
            this.Controls.Add(zoneControlGroup);
            this.Controls.Add(dashboardGroup);
            this.Controls.Add(logGroup);
        }

        // ==========================================
        // 🚨 실장비 네트워크 통신 핵심 로직 구현부
        // ==========================================

        public void SendWOLMagicPacket(string macAddress)
        {
            try
            {
                string cleanMac = macAddress.Replace("-", "").Replace(":", "").Replace(" ", "");
                if (cleanMac.Length != 12)
                {
                    AppendLog("   [WOL 에러] MAC 주소 형식이 올바르지 않습니다: " + macAddress);
                    return;
                }

                byte[] macBytes = new byte[6];
                for (int i = 0; i < 6; i++)
                {
                    macBytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
                }

                byte[] packet = new byte[102];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;
                for (int i = 1; i <= 16; i++)
                {
                    Array.Copy(macBytes, 0, packet, i * 6, 6);
                }

                using (UdpClient client = new UdpClient())
                {
                    client.Connect(IPAddress.Broadcast, 9);
                    client.Send(packet, packet.Length);
                }
                AppendLog(string.Format("   📡 [UDP WOL 전송 성공] 브로드캐스트로 Magic Packet 발송 완료 -> MAC: {0}", macAddress));
            }
            catch (Exception ex)
            {
                AppendLog("   ❌ [WOL 전송 실패] " + ex.Message);
            }
        }

        public void SendTcpShutdownCommandAsync(string ip, int port)
        {
            Task.Run(async () => {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        var connectTask = client.ConnectAsync(ip, port);
                        if (await Task.WhenAny(connectTask, Task.Delay(2500)) == connectTask)
                        {
                            await connectTask; 
                            using (var stream = client.GetStream())
                            {
                                byte[] cmd = Encoding.UTF8.GetBytes("SHUTDOWN\n");
                                await stream.WriteAsync(cmd, 0, cmd.Length);
                            }
                            AppendLog(string.Format("   📡 [TCP Shutdown 성공] PC 종료 신호 송출 완료 -> IP: {0}:{1}", ip, port));
                        }
                        else
                        {
                            throw new TimeoutException("연결 타임아웃 (에이전트가 꺼져있거나 포트가 닫혀있음)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog(string.Format("   ❌ [TCP Shutdown 실패] {0}:{1} -> {2}", ip, port, ex.Message));
                }
            });
        }

        public void SendPJLinkCommandAsync(string ip, int port, string command)
        {
            Task.Run(async () => {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        var connectTask = client.ConnectAsync(ip, port);
                        if (await Task.WhenAny(connectTask, Task.Delay(2500)) == connectTask)
                        {
                            await connectTask;
                            using (var stream = client.GetStream())
                            {
                                // PJLink 웰컴 헤더 수신 대기 (PJLINK 0 / PJLINK 1 등)
                                byte[] welcomeBuffer = new byte[64];
                                int welcomed = await stream.ReadAsync(welcomeBuffer, 0, welcomeBuffer.Length);

                                byte[] cmdBytes = Encoding.UTF8.GetBytes(command + "\r");
                                await stream.WriteAsync(cmdBytes, 0, cmdBytes.Length);
                            }
                            AppendLog(string.Format("   📡 [PJLink 전송 성공] 명령 패킷 발송 완료 -> {0} ({1}:{2})", command, ip, port));
                        }
                        else
                        {
                            throw new TimeoutException("연결 타임아웃 (프로젝터가 꺼져있거나 네트워크 단선)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog(string.Format("   ❌ [PJLink 전송 실패] {0}:{1} -> {2}", ip, port, ex.Message));
                }
            });
        }

        private void ScheduleConfigBtn_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence) return;

            using (ScheduleConfigForm schForm = new ScheduleConfigForm(scheduleSettings, devices, spaces, this))
            {
                schForm.ShowDialog();
            }
        }

        public void ApplyScheduleSettings(ScheduleSettings newSettings)
        {
            this.scheduleSettings = newSettings;
            UpdateScheduleText();
            AppendLog("⏰ 자동 스케줄 예약 테이블 변경 완료: 설정 시각이 실시간 동기화되었습니다.");
        }

        private void UpdateScheduleText()
        {
            if (autoScheduleCheckBox.Checked)
            {
                string ignoreStr = scheduleSettings.IgnoreDays.Count > 0 ? string.Join(", ", scheduleSettings.IgnoreDays) : "없음";
                nextScheduleLabel.Text = string.Format("⏰ 스케줄 상세: 차단요인({0}) | 평일({1} ~ {2}) | 토요일({3} ~ {4})",
                    ignoreStr, scheduleSettings.WeekdayStart, scheduleSettings.WeekdayEnd, scheduleSettings.SaturdayStart, scheduleSettings.SaturdayEnd);
                nextScheduleLabel.ForeColor = ColorTranslator.FromHtml("#f54e00");
            }
            else
            {
                nextScheduleLabel.Text = "⏰ 자동 예약 대기 중지 (연장 점검/운영 모드)";
                nextScheduleLabel.ForeColor = ColorTranslator.FromHtml("#cf2d56");
            }
        }

        private void ConfigBtn_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence) return;
            
            using (DeviceConfigForm configForm = new DeviceConfigForm(devices, spaces, selectedSpaceTab, this))
            {
                configForm.ShowDialog();
            }
        }

        public void OpenCompositeEditForm(string parentDeviceId)
        {
            if (isSimulatingSequence) return;

            using (DeviceCompositeEditForm compForm = new DeviceCompositeEditForm(parentDeviceId, devices, this))
            {
                compForm.ShowDialog();
            }
        }

        public void OpenConfigFormForDevice(string targetDeviceId)
        {
            if (isSimulatingSequence) return;

            var dev = devices.Find(d => d.Id == targetDeviceId);
            if (dev != null)
            {
                selectedSpaceTab = dev.Space;
                UpdateTabButtonSelectionVisuals();
                UpdateZoneControlButtonsText();
                cardContainerPanel.Controls.Clear();
                RefreshVisualDashboard();
            }

            using (DeviceConfigForm configForm = new DeviceConfigForm(devices, spaces, selectedSpaceTab, this))
            {
                configForm.SetTargetDeviceFocus(targetDeviceId);
                configForm.ShowDialog();
            }
        }

        public void ApplyUpdatedDevices(List<DeviceItem> newDevices, List<string> newSpaces)
        {
            this.devices = newDevices;
            this.spaces = newSpaces;
            
            PopulateSpaces();
            
            cardContainerPanel.Controls.Clear();
            RefreshVisualDashboard();
            
            AppendLog("⚙️ 장치 및 공간 리스트 변경 감지: 데이터가 갱신되어 탭 및 대시보드에 즉시 반영되었습니다.");
        }

        public DeviceItem FindDeviceById(string id)
        {
            return devices.Find(d => d.Id == id);
        }

        public void SetSimulationMode(bool isRunning)
        {
            this.isSimulatingSequence = isRunning;
            LockButtons(!isRunning);
        }

        public void LogMessage(string message)
        {
            AppendLog(message);
        }

        public void UpdateVisualDashboard()
        {
            RefreshVisualDashboard();
        }

        private void RefreshVisualDashboard()
        {
            if (cardContainerPanel.InvokeRequired)
            {
                cardContainerPanel.Invoke(new Action(RefreshVisualDashboard));
                return;
            }

            List<DeviceItem> filteredDevices = new List<DeviceItem>();
            if (selectedSpaceTab == "ALL")
            {
                filteredDevices = new List<DeviceItem>(devices);
            }
            else
            {
                filteredDevices = devices.FindAll(d => d.Space == selectedSpaceTab);
            }

            filteredDevices.Sort((x, y) => {
                int spaceComp = string.Compare(x.Space, y.Space);
                if (spaceComp != 0) return spaceComp;
                return x.BootOrder.CompareTo(y.BootOrder);
            });

            HashSet<string> handledIds = new HashSet<string>();
            List<Control> newCards = new List<Control>();

            foreach (var dev in filteredDevices)
            {
                if (dev.Type != "PC" || handledIds.Contains(dev.Id)) continue;

                List<DeviceItem> partners = filteredDevices.FindAll(d => d.Type == "Projector" && d.AssociatedDeviceId == dev.Id);
                
                if (!string.IsNullOrEmpty(dev.AssociatedDeviceId))
                {
                    var standardPartner = filteredDevices.Find(d => d.Id == dev.AssociatedDeviceId && d.Type == "Projector");
                    if (standardPartner != null && !partners.Contains(standardPartner))
                    {
                        partners.Add(standardPartner);
                    }
                }

                DeviceCardControl card = new DeviceCardControl(dev, partners, this);
                newCards.Add(card);

                handledIds.Add(dev.Id);
                foreach (var p in partners) handledIds.Add(p.Id);
            }

            foreach (var dev in filteredDevices)
            {
                if (handledIds.Contains(dev.Id)) continue;

                DeviceCardControl card = new DeviceCardControl(dev, null, this);
                newCards.Add(card);
                handledIds.Add(dev.Id);
            }

            // GDI Handles & Timer Memory Leaks 해결을 위해 기존 카드를 명시적으로 루프 돌며 파괴
            foreach (Control ctrl in cardContainerPanel.Controls)
            {
                try
                {
                    ctrl.Dispose();
                }
                catch { }
            }
            cardContainerPanel.Controls.Clear();

            foreach (var card in newCards)
            {
                cardContainerPanel.Controls.Add(card);
            }

            int onlineCount = 0;
            foreach (var dev in devices)
            {
                if (dev.RuntimeStatus == "ONLINE") onlineCount++;
            }
            statusSummaryLabel.Text = string.Format("가동 상황: {0}대 실행 중 / 총 {1}대", onlineCount, devices.Count);
        }

        private LogType DetermineLogType(string message)
        {
            if (message.Contains("❌") || message.Contains("⚠️") || message.Contains("실패") || message.Contains("오류") || message.Contains("에러") || message.Contains("차단"))
            {
                return LogType.Error;
            }
            else if (message.Contains("📡") || message.Contains("⚡") || message.Contains("🟢") || message.Contains("💚") || message.Contains("성공") || message.Contains("ONLINE") || message.Contains("복구") || message.Contains("완료"))
            {
                return LogType.Success;
            }
            return LogType.Normal;
        }

        private Color GetLogColor(LogType type, bool isDark)
        {
            if (isDark)
            {
                switch (type)
                {
                    case LogType.Success:
                        return ColorTranslator.FromHtml("#34d399"); // Bright emerald green
                    case LogType.Error:
                        return ColorTranslator.FromHtml("#f87171"); // Bright coral red
                    default:
                        return ColorTranslator.FromHtml("#f8fafc"); // White
                }
            }
            else
            {
                switch (type)
                {
                    case LogType.Success:
                        return ColorTranslator.FromHtml("#047857"); // Dark forest green
                    case LogType.Error:
                        return ColorTranslator.FromHtml("#b91c1c"); // Dark brick red
                    default:
                        return ColorTranslator.FromHtml("#26251e"); // Dark ink
                }
            }
        }

        private void AppendLog(string message)
        {
            if (logRichTextBox.InvokeRequired)
            {
                logRichTextBox.Invoke(new Action<string>(AppendLog), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogType type = DetermineLogType(message);
            LogHistoryItem item = new LogHistoryItem { Timestamp = timestamp, Message = message, Type = type };
            logHistory.Add(item);
            if (logHistory.Count > 500) logHistory.RemoveAt(0);

            Color textColor = GetLogColor(type, isDarkMode);
            logRichTextBox.SelectionStart = logRichTextBox.TextLength;
            logRichTextBox.SelectionLength = 0;
            logRichTextBox.SelectionColor = textColor;
            logRichTextBox.AppendText(string.Format("[{0}] {1}\r\n", timestamp, message));
            logRichTextBox.ScrollToCaret();
        }

        private void ReColorLogHistory()
        {
            if (logRichTextBox.InvokeRequired)
            {
                logRichTextBox.Invoke(new MethodInvoker(ReColorLogHistory));
                return;
            }

            logRichTextBox.SuspendLayout();
            logRichTextBox.Clear();
            foreach (var item in logHistory)
            {
                Color textColor = GetLogColor(item.Type, isDarkMode);
                logRichTextBox.SelectionStart = logRichTextBox.TextLength;
                logRichTextBox.SelectionLength = 0;
                logRichTextBox.SelectionColor = textColor;
                logRichTextBox.AppendText(string.Format("[{0}] {1}\r\n", item.Timestamp, item.Message));
            }
            logRichTextBox.ScrollToCaret();
            logRichTextBox.ResumeLayout();
        }

        public void AddWolRetryQueue(string deviceId)
        {
            var dev = devices.Find(d => d.Id == deviceId && d.Type == "PC");
            if (dev == null) return;

            lock (wolRetryLock)
            {
                var exist = wolRetryList.Find(x => x.DeviceId == deviceId);
                if (exist != null)
                {
                    exist.FirstSentTime = DateTime.Now;
                    exist.LastSentTime = DateTime.Now;
                    exist.RetryCount = 0;
                }
                else
                {
                    wolRetryList.Add(new WolRetryItem
                    {
                        DeviceId = deviceId,
                        FirstSentTime = DateTime.Now,
                        LastSentTime = DateTime.Now,
                        RetryCount = 0
                    });
                }
            }
        }

        public void RemoveWolRetryQueue(string deviceId)
        {
            lock (wolRetryLock)
            {
                wolRetryList.RemoveAll(x => x.DeviceId == deviceId);
            }
        }

        private void RunMonitoringTick()
        {
            if (isSimulatingSequence) return;
            
            bool isDashboardUpdated = false;
            DateTime threshold = DateTime.Now.AddSeconds(-15);

            // 1. 하트비트 타임아웃 감시 (FREEZE 전환)
            foreach (var dev in devices)
            {
                if (dev.Type == "PC" && dev.RuntimeStatus == "ONLINE")
                {
                    if (dev.LastActiveTime < threshold)
                    {
                        dev.RuntimeStatus = "FREEZE";
                        AppendLog(string.Format("🚨 [장애 감지] '{0}' 기기의 콘텐츠 생존 신호(Heartbeat) 수신 차단! (15초 감시 타임아웃)", dev.Name));
                        isDashboardUpdated = true;

                        if (trayIcon != null)
                        {
                            trayIcon.ShowBalloonTip(
                                5000, 
                                "🚨 장비 연결 끊김 감지 (FREEZE)", 
                                string.Format("'{0}' 기기의 Heartbeat 신호가 15초간 수신되지 않았습니다.", dev.Name), 
                                ToolTipIcon.Error
                            );
                        }
                    }
                }
            }

            // 2. WOL 재시도 감시
            lock (wolRetryLock)
            {
                // 성공한 기기들(ONLINE) 혹은 최근 5초 이내 하트비트 신호 수신 기기는 큐에서 즉시 제거
                wolRetryList.RemoveAll(item => {
                    var dev = devices.Find(d => d.Id == item.DeviceId);
                    return dev != null && (dev.RuntimeStatus == "ONLINE" || (DateTime.Now - dev.LastActiveTime).TotalSeconds < 5);
                });

                List<WolRetryItem> toRemove = new List<WolRetryItem>();
                foreach (var item in wolRetryList)
                {
                    var dev = devices.Find(d => d.Id == item.DeviceId);
                    if (dev == null)
                    {
                        toRemove.Add(item);
                        continue;
                    }

                    // 120초(2분) 경과 감시
                    if ((DateTime.Now - item.LastSentTime).TotalSeconds >= 120)
                    {
                        if (item.RetryCount < 3)
                        {
                            item.RetryCount++;
                            item.LastSentTime = DateTime.Now;
                            
                            AppendLog(string.Format("🔁 [WOL 자동 재시도] '{0}' 기기가 2분간 응답이 없어 WOL 패킷을 재송출합니다. (시도: {1}/3)", dev.Name, item.RetryCount));
                            SendWOLMagicPacket(dev.MacAddress);
                        }
                        else
                        {
                            AppendLog(string.Format("❌ [WOL 재시도 실패] '{0}' 기기에 WOL을 3회 재송출했으나 켜지지 않아 예외 모니터링을 종료합니다. (하드웨어 점검 필요)", dev.Name));
                            toRemove.Add(item);
                        }
                    }
                }

                foreach (var r in toRemove)
                {
                    wolRetryList.Remove(r);
                }
            }

            if (isDashboardUpdated)
            {
                RefreshVisualDashboard();
            }
        }

        private void AutoScheduleCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateScheduleText();
            AppendLog(string.Format("스케줄러: 자동 전원 제어 모드가 [{0}] 되었습니다.", autoScheduleCheckBox.Checked ? "활성화" : "비활성화"));
        }

        private async void TriggerScheduleTestBtn_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence || virtualDayComboBox.SelectedItem == null) return;

            string selectedDay = virtualDayComboBox.SelectedItem.ToString();
            AppendLog(string.Format("[스케줄러 예약 감지 시뮬레이션 트리거됨] 가상 요일: {0}", selectedDay));

            if (!autoScheduleCheckBox.Checked)
            {
                AppendLog("스케줄러: 스케줄 예약 시각이 되었으나, 자동 제어가 비활성화[OFF] 상태이므로 전원 종료를 스킵합니다.");
                MessageBox.Show("자동 전원 제어 토글이 OFF 상태이므로 스케줄 기동이 무시되었습니다.", "스케줄 차단 작동", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool isBlockedDay = false;
            foreach (var block in scheduleSettings.IgnoreDays)
            {
                if (selectedDay.Contains(block)) isBlockedDay = true;
            }

            if (isBlockedDay)
            {
                AppendLog("스케줄러: 예약 시각이 도달했으나, 오늘 요일은 지정된 '무조건 차단 요일(IgnoreDaysOfWeek)'에 해당하여 자동 제어 명령을 무시합니다.");
                MessageBox.Show("지정된 요일은 자동 제어 차단 요일로 등록되어 기동이 강제 차단되었습니다.", "차단 요일 작동", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetTime = "";
            if (selectedDay.Contains("화요일"))
            {
                targetTime = string.Format("{0} (평일 종료)", scheduleSettings.WeekdayEnd);
            }
            else if (selectedDay.Contains("토요일"))
            {
                targetTime = string.Format("{0} (토요일 단축 종료)", scheduleSettings.SaturdayEnd);
            }

            var result = MessageBox.Show(
                string.Format("잠시 후 {0}에 시연실 전체 자동 종료 시퀀스가 시작됩니다.\n연장 운영을 원하시면 [취소(Cancel)] 버튼을 누르십시오.", targetTime), 
                "자동 전원 제어 알림 (1분 전 경고)", 
                MessageBoxButtons.OKCancel, 
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
            {
                AppendLog("스케줄러: 관리자에 의해 금일 자동 종료 시퀀스가 취소(Skip) 되었습니다.");
                return;
            }

            isSimulatingSequence = true;
            LockButtons(false);

            AppendLog(string.Format("스케줄러: 설정된 요일별 종료 예약 시각({0})에 도달하였습니다. 자동 종료 시퀀스를 트리거합니다.", targetTime));
            AppendLog("====== [스케줄 자동 종료 시퀀스 시작] ======");
            await RunPowerOffSequenceAsync(devices);
            AppendLog("====== [스케줄 자동 종료 시퀀스 완료] ======");

            isSimulatingSequence = false;
            LockButtons(true);
            RefreshVisualDashboard();
        }

        private void TitlePanel_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            dragCursorPoint = Cursor.Position;
            FormDragPoint = this.Location;
        }

        private void TitlePanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(FormDragPoint, new Size(dif));
            }
        }

        private void TitlePanel_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void PopulateSpaces()
        {
            if (tabFlowPanel.InvokeRequired)
            {
                tabFlowPanel.Invoke(new Action(PopulateSpaces));
                return;
            }

            tabFlowPanel.Controls.Clear();

            Button allTabBtn = CreateTabButton("전체보기", "ALL");
            tabFlowPanel.Controls.Add(allTabBtn);

            foreach (var sp in spaces)
            {
                Button spaceTabBtn = CreateTabButton(sp, sp);
                tabFlowPanel.Controls.Add(spaceTabBtn);
            }

            UpdateTabButtonSelectionVisuals();
            UpdateZoneControlButtonsText();
        }

        private void DeleteSpaceTabBtn_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence) return;

            if (selectedSpaceTab == "ALL")
            {
                MessageBox.Show("전체보기 탭은 삭제할 수 없습니다.\n삭제할 특정 공간 탭을 선택한 뒤 다시 눌러주세요.", "공간 삭제 불가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult res = MessageBox.Show(
                string.Format("선택한 공간 '{0}'을 정말로 삭제하시겠습니까?\n\n※ 삭제 시 해당 공간에 속해있던 장비들은 안전하게 다른 기본 공간으로 대피 이전 처리됩니다.", selectedSpaceTab),
                "공간 삭제 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (res == DialogResult.Yes)
            {
                string targetSpace = selectedSpaceTab;
                spaces.Remove(targetSpace);

                // 대피 처리 (남은 공간 중 첫 번째 공간으로, 없으면 기본공간 생성)
                string fallbackSpace = spaces.Count > 0 ? spaces[0] : "기본공간";
                if (spaces.Count == 0)
                {
                    spaces.Add(fallbackSpace);
                }

                int movedCount = 0;
                foreach (var d in devices)
                {
                    if (d.Space == targetSpace)
                    {
                        d.Space = fallbackSpace;
                        movedCount++;
                    }
                }

                SaveConfigToFile();
                
                selectedSpaceTab = "ALL";
                PopulateSpaces();
                RefreshVisualDashboard();

                if (movedCount > 0)
                {
                    AppendLog(string.Format("⚙️ 공간 '{0}'이 삭제되었습니다. 소속 장비 {1}대가 '{2}' 공간으로 대피 이전되었습니다.", targetSpace, movedCount, fallbackSpace));
                }
                else
                {
                    AppendLog(string.Format("⚙️ 공간 '{0}'이 삭제되었습니다.", targetSpace));
                }
            }
        }

        private Button CreateTabButton(string text, string spaceValue)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Tag = spaceValue;
            btn.Size = new Size(100, 28);
            btn.Margin = new Padding(0, 3, 8, 3);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(50, 52, 74);
            btn.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Click += TabButton_Click;
            return btn;
        }

        private void TabButton_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence) return;
            Button clickedBtn = sender as Button;
            if (clickedBtn == null) return;

            string spaceVal = clickedBtn.Tag.ToString();
            selectedSpaceTab = spaceVal;

            UpdateTabButtonSelectionVisuals();
            UpdateZoneControlButtonsText();
            
            cardContainerPanel.Controls.Clear();
            RefreshVisualDashboard();
        }

        private void UpdateTabButtonSelectionVisuals()
        {
            foreach (Control ctrl in tabFlowPanel.Controls)
            {
                Button btn = ctrl as Button;
                if (btn == null) continue;
                if (btn.Tag == null) continue;

                string val = btn.Tag.ToString();
                if (val == selectedSpaceTab)
                {
                    btn.BackColor = ColorTranslator.FromHtml("#f54e00");
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#f54e00");
                }
                else
                {
                    btn.BackColor = ThemeManager.CardBgColor;
                    btn.ForeColor = ThemeManager.TextColor;
                    btn.FlatAppearance.BorderColor = ThemeManager.BorderColorSoft;
                }
            }
        }

        private void UpdateZoneControlButtonsText()
        {
            if (selectedSpaceTab == "ALL")
            {
                zoneOnBtn.Text = "◀  전체 켜기";
                zoneOffBtn.Text = "⏹️  전체 끄기";
            }
            else
            {
                zoneOnBtn.Text = "◀  구역 켜기";
                zoneOffBtn.Text = "⏹️  구역 끄기";
            }
        }

        private void AddSpaceTabBtn_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence) return;
            
            using (PromptForm prompt = new PromptForm("추가할 공간(Space) 이름을 입력하십시오:", "공간 추가"))
            {
                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    string newSpace = prompt.InputText;
                    if (string.IsNullOrEmpty(newSpace))
                    {
                        MessageBox.Show("공간 이름은 비워둘 수 없습니다.", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (spaces.Contains(newSpace))
                    {
                        MessageBox.Show("이미 존재하는 공간 이름입니다.", "중복 경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    spaces.Add(newSpace);
                    SaveConfigToFile();
                    PopulateSpaces();

                    AppendLog(string.Format("⚙️ 신규 공간 '{0}'이 메인 화면 탭에서 즉시 생성 및 저장되었습니다.", newSpace));
                    
                    foreach (Control ctrl in tabFlowPanel.Controls)
                    {
                        Button btn = ctrl as Button;
                        if (btn != null && btn.Tag != null && btn.Tag.ToString() == newSpace)
                        {
                            TabButton_Click(btn, EventArgs.Empty);
                            break;
                        }
                    }
                }
            }
        }

        private void SaveConfigToFile()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            try
            {
                DeviceConfig wrap = new DeviceConfig() 
                { 
                    Spaces = this.spaces, 
                    Devices = this.devices,
                    Schedules = this.scheduleSettings
                };
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string jsonText = serializer.Serialize(wrap);
                File.WriteAllText(configPath, jsonText, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("설정 파일 저장 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenDeviceAddFormWithPreset(string presetDeviceId, string presetIp)
        {
            try
            {
                using (DeviceAddForm addForm = new DeviceAddForm(this.devices, this.spaces, presetDeviceId, presetIp))
                {
                    if (addForm.ShowDialog(this) == DialogResult.OK && addForm.AddedDevice != null)
                    {
                        this.devices.Add(addForm.AddedDevice);
                        SaveConfigToFile();
                        RefreshVisualDashboard();
                        AppendLog(string.Format("➕ [기기 등록] 신규 기기 '{0}'(ID: {1}, IP: {2}) 등록 완료 ➡️ devices.json 저장",
                            addForm.AddedDevice.Name, addForm.AddedDevice.Id, addForm.AddedDevice.IpAddress));
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("⚠️ [기기 등록 오류] " + ex.Message);
            }
        }

        public async Task TestLauncherConnectionAsync(string ip, int port, string deviceName)
        {
            AppendLog(string.Format("📡 [연결 진단 시작] '{0}'(IP: {1}, 포트: {2}) TCP 소켓 연결 진단 중...", deviceName, ip, port));
            
            bool isSuccess = false;
            string errorReason = "";
            long elapsedMs = 0;

            await Task.Run(() =>
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                TcpClient client = null;
                try
                {
                    client = new TcpClient();
                    IAsyncResult ar = client.BeginConnect(ip, port, null, null);
                    bool connected = ar.AsyncWaitHandle.WaitOne(2500, false);
                    if (!connected)
                    {
                        errorReason = "연결 시간 초과 (PC가 꺼져 있거나 런처가 실행되지 않음)";
                        return;
                    }
                    client.EndConnect(ar);

                    client.ReceiveTimeout = 2500;
                    client.SendTimeout = 2500;

                    NetworkStream stream = client.GetStream();
                    byte[] testCmd = Encoding.UTF8.GetBytes("CONN_TEST\n");
                    stream.Write(testCmd, 0, testCmd.Length);
                    stream.Flush();

                    byte[] buffer = new byte[256];
                    int readBytes = stream.Read(buffer, 0, buffer.Length);
                    sw.Stop();
                    elapsedMs = sw.ElapsedMilliseconds;

                    if (readBytes > 0)
                    {
                        string response = Encoding.UTF8.GetString(buffer, 0, readBytes).Trim();
                        if (response.StartsWith("CONN_OK"))
                        {
                            isSuccess = true;
                        }
                        else
                        {
                            errorReason = "비정상 응답 수신: " + response;
                        }
                    }
                    else
                    {
                        errorReason = "상대방으로부터 응답 데이터가 없음";
                    }
                }
                catch (SocketException ex)
                {
                    errorReason = "네트워크 소켓 오류: " + ex.Message;
                }
                catch (Exception ex)
                {
                    errorReason = "통신 오류: " + ex.Message;
                }
                finally
                {
                    if (client != null)
                    {
                        try { client.Close(); } catch { }
                    }
                }
            });

            if (isSuccess)
            {
                AppendLog(string.Format("🟢 [연결 진단 성공] '{0}'(IP: {1}) 접속 성공 및 CONN_OK 확인 ({2}ms)", deviceName, ip, elapsedMs));
                MessageBox.Show(
                    string.Format("🟢 연결 진단 성공!\n\n장치: '{0}' (IP: {1})\n상태: 런처 프로그램이 정상 작동 중이며 통신이 원활합니다.\n응답 속도: {2}ms", deviceName, ip, elapsedMs),
                    "원격 통신 진단 성공",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                AppendLog(string.Format("❌ [연결 진단 실패] '{0}'(IP: {1}) -> {2}", deviceName, ip, errorReason));
                MessageBox.Show(
                    string.Format("🔴 연결 진단 실패!\n\n장치: '{0}' (IP: {1})\n사유: {2}\n\n[점검 사항]\n1. 해당 PC 전원이 켜져 있는지 확인\n2. 런처 프로그램(Launcher.exe)이 실행 중인지 확인\n3. LAN 케이블 연결 및 IP({1})가 맞는지 확인", deviceName, ip, errorReason),
                    "원격 통신 진단 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void PopulateVirtualDays()
        {
            virtualDayComboBox.Items.Clear();
            virtualDayComboBox.Items.Add("화요일 (평일 정상)");
            virtualDayComboBox.Items.Add("토요일 (사용자 단축)");
            virtualDayComboBox.Items.Add("월요일 (무조건 차단)");
            virtualDayComboBox.SelectedIndex = 0;
        }

        private async void ZoneOnBtn_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence) return;

            List<DeviceItem> targetDevices;
            string spaceLogName;

            if (selectedSpaceTab == "ALL")
            {
                targetDevices = new List<DeviceItem>(devices);
                spaceLogName = "시연실 전체";
            }
            else
            {
                targetDevices = devices.FindAll(d => d.Space == selectedSpaceTab);
                spaceLogName = string.Format("'{0}' 구역", selectedSpaceTab);
            }

            if (targetDevices.Count == 0)
            {
                MessageBox.Show("해당 구역에 등록된 장비가 존재하지 않습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isSimulatingSequence = true;
            LockButtons(false);

            AppendLog(string.Format("====== [{0} 켜기 시퀀스 시작 (대상: {1}대)] ======", spaceLogName, targetDevices.Count));
            await RunPowerOnSequenceAsync(targetDevices);
            AppendLog(string.Format("====== [{0} 켜기 시퀀스 완료] ======", spaceLogName));

            isSimulatingSequence = false;
            LockButtons(true);
            RefreshVisualDashboard();
        }

        private async void ZoneOffBtn_Click(object sender, EventArgs e)
        {
            if (isSimulatingSequence) return;

            List<DeviceItem> targetDevices;
            string spaceLogName;

            if (selectedSpaceTab == "ALL")
            {
                var confirm = MessageBox.Show("시연실의 전체 전원을 강제 종료하시겠습니까?\n프로젝터는 쿨링 작업이 순차 진행됩니다.", "전체 종료 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                targetDevices = new List<DeviceItem>(devices);
                spaceLogName = "시연실 전체";
            }
            else
            {
                targetDevices = devices.FindAll(d => d.Space == selectedSpaceTab);
                spaceLogName = string.Format("'{0}' 구역", selectedSpaceTab);
            }

            if (targetDevices.Count == 0)
            {
                MessageBox.Show("해당 구역에 등록된 장비가 존재하지 않습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isSimulatingSequence = true;
            LockButtons(false);

            AppendLog(string.Format("====== [{0} 끄기 시퀀스 시작 (대상: {1}대)] ======", spaceLogName, targetDevices.Count));
            await RunPowerOffSequenceAsync(targetDevices);
            AppendLog(string.Format("====== [{0} 끄기 시퀀스 완료] ======", spaceLogName));

            isSimulatingSequence = false;
            LockButtons(true);
            RefreshVisualDashboard();
        }

        private async Task RunPowerOnSequenceAsync(List<DeviceItem> targetList)
        {
            List<DeviceItem> sorted = new List<DeviceItem>(targetList);
            sorted.Sort((x, y) => x.BootOrder.CompareTo(y.BootOrder));

            int currentOrder = -1;
            List<DeviceItem> currentGroup = new List<DeviceItem>();

            for (int i = 0; i < sorted.Count; i++)
            {
                var dev = sorted[i];
                if (currentOrder == -1)
                {
                    currentOrder = dev.BootOrder;
                }

                if (dev.BootOrder == currentOrder)
                {
                    currentGroup.Add(dev);
                }

                if (i == sorted.Count - 1 || sorted[i + 1].BootOrder != currentOrder)
                {
                    AppendLog(string.Format("[우선순위 {0}순위 기동 실행] (그룹 크기: {1}대)", currentOrder, currentGroup.Count));
                    
                    int targetIndex = 0;
                    foreach (var target in currentGroup)
                    {
                        if (target.RuntimeStatus == "ONLINE") continue;

                        if (targetIndex > 0)
                        {
                            await Task.Delay(500); // 동일 우선순위 내 기동 분산 딜레이
                        }
                        targetIndex++;

                        List<DeviceItem> linkedProjs = devices.FindAll(d => d.Type == "Projector" && d.AssociatedDeviceId == target.Id);
                        if (!string.IsNullOrEmpty(target.AssociatedDeviceId))
                        {
                            var standardLinked = devices.Find(d => d.Id == target.AssociatedDeviceId && d.Type == "Projector");
                            if (standardLinked != null && !linkedProjs.Contains(standardLinked)) linkedProjs.Add(standardLinked);
                        }

                        bool isProjFirst = (target.Type == "PC" && target.PowerOnSequenceMode == "PROJ_FIRST");

                        if (isProjFirst && linkedProjs.Count > 0)
                        {
                            // [모드 A] 프로젝터 우선 (프로젝터 예열 ➡️ PC 켬)
                            AppendLog(string.Format(">> (연동 프로젝터 예열) '{0}'의 자식 프로젝터 기동 및 예열 시작", target.Name));
                            int maxProjDelay = 0;
                            foreach (var proj in linkedProjs)
                            {
                                proj.RuntimeStatus = "BOOTING";
                                int dly = proj.BootDelaySeconds > 0 ? proj.BootDelaySeconds : 10;
                                proj.RemainingSeconds = dly;
                                if (dly > maxProjDelay) maxProjDelay = dly;

                                AppendLog(string.Format("   └🔗 {0} 예열 ON 패킷 송신 (대기: {1}초)", proj.Name, dly));
                                #pragma warning disable 4014
                                SendPJLinkCommandAsync(proj.IpAddress, proj.Port, "%1POWR 1");
                                #pragma warning restore 4014
                            }
                            RefreshVisualDashboard();

                            for (int d = maxProjDelay; d > 0; d--)
                            {
                                foreach (var proj in linkedProjs)
                                {
                                    if (proj.RemainingSeconds > 0) proj.RemainingSeconds--;
                                }
                                RefreshVisualDashboard();
                                await Task.Delay(1000);
                            }

                            foreach (var proj in linkedProjs)
                            {
                                proj.RuntimeStatus = "ONLINE";
                                proj.RemainingSeconds = 0;
                            }
                            RefreshVisualDashboard();

                            // 부모 PC WOL 기동
                            target.RuntimeStatus = "BOOTING";
                            AddWolRetryQueue(target.Id);
                            AppendLog(string.Format(">> (UDP WOL) 매직 패킷 송신 시도 ➡️ MAC: {0} [{1}]", target.MacAddress, target.Name));
                            SendWOLMagicPacket(target.MacAddress);

                            int pcDelay = target.BootDelaySeconds > 0 ? target.BootDelaySeconds : 5;
                            for (int d = pcDelay; d >= 0; d--)
                            {
                                target.RemainingSeconds = d;
                                RefreshVisualDashboard();
                                if (d > 0) await Task.Delay(1000);
                            }
                            target.RuntimeStatus = "ONLINE";
                            target.RemainingSeconds = 0;
                            RefreshVisualDashboard();
                        }
                        else
                        {
                            // [모드 B] PC 우선 (PC ➡️ 프로젝터) 또는 단독 기기 기동
                            if (target.Type == "PC")
                            {
                                target.RuntimeStatus = "BOOTING";
                                AddWolRetryQueue(target.Id);
                                AppendLog(string.Format(">> (UDP WOL) 매직 패킷 송신 시도 ➡️ MAC: {0} [{1}]", target.MacAddress, target.Name));
                                SendWOLMagicPacket(target.MacAddress);

                                int pcDelay = target.BootDelaySeconds > 0 ? target.BootDelaySeconds : 5;
                                for (int d = pcDelay; d >= 0; d--)
                                {
                                    target.RemainingSeconds = d;
                                    RefreshVisualDashboard();
                                    if (d > 0) await Task.Delay(1000);
                                }
                                target.RuntimeStatus = "ONLINE";
                                target.RemainingSeconds = 0;
                                RefreshVisualDashboard();

                                // 프로젝터 기동
                                if (linkedProjs.Count > 0)
                                {
                                    AppendLog(string.Format(">> (연동 프로젝터 기동) '{0}'의 자식 프로젝터 기동 시작", target.Name));
                                    int maxProjDelay = 0;
                                    foreach (var proj in linkedProjs)
                                    {
                                        proj.RuntimeStatus = "BOOTING";
                                        int dly = proj.BootDelaySeconds > 0 ? proj.BootDelaySeconds : 10;
                                        proj.RemainingSeconds = dly;
                                        if (dly > maxProjDelay) maxProjDelay = dly;

                                        AppendLog(string.Format("   └🔗 {0} ON 패킷 송신 (대기: {1}초)", proj.Name, dly));
                                        #pragma warning disable 4014
                                        SendPJLinkCommandAsync(proj.IpAddress, proj.Port, "%1POWR 1");
                                        #pragma warning restore 4014
                                    }
                                    RefreshVisualDashboard();

                                    for (int d = maxProjDelay; d > 0; d--)
                                    {
                                        foreach (var proj in linkedProjs)
                                        {
                                            if (proj.RemainingSeconds > 0) proj.RemainingSeconds--;
                                        }
                                        RefreshVisualDashboard();
                                        await Task.Delay(1000);
                                    }

                                    foreach (var proj in linkedProjs)
                                    {
                                        proj.RuntimeStatus = "ONLINE";
                                        proj.RemainingSeconds = 0;
                                    }
                                    RefreshVisualDashboard();
                                }
                            }
                            else if (target.Type == "Projector")
                            {
                                // 연동되지 않은 단독 프로젝터만 여기서 기동
                                bool isLinked = !string.IsNullOrEmpty(target.AssociatedDeviceId) || devices.Exists(d => d.Type == "PC" && d.AssociatedDeviceId == target.Id);
                                if (!isLinked)
                                {
                                    target.RuntimeStatus = "BOOTING";
                                    AppendLog(string.Format(">> (TCP PJLink ON) 켜짐 명령어 전송 시도 ➡️ IP: {0} [{1}]", target.IpAddress, target.Name));
                                    #pragma warning disable 4014
                                    SendPJLinkCommandAsync(target.IpAddress, target.Port, "%1POWR 1");
                                    #pragma warning restore 4014

                                    int projDelay = target.BootDelaySeconds > 0 ? target.BootDelaySeconds : 10;
                                    for (int d = projDelay; d >= 0; d--)
                                    {
                                        target.RemainingSeconds = d;
                                        RefreshVisualDashboard();
                                        if (d > 0) await Task.Delay(1000);
                                    }
                                    target.RuntimeStatus = "ONLINE";
                                    target.RemainingSeconds = 0;
                                    RefreshVisualDashboard();
                                }
                            }
                        }
                    }

                    if (i < sorted.Count - 1)
                    {
                        currentOrder = sorted[i + 1].BootOrder;
                        currentGroup.Clear();
                    }
                }
            }
        }

        private async Task RunPowerOffSequenceAsync(List<DeviceItem> targetList)
        {
            AppendLog("[1단계: PC 기기 강제 종료 신호 송신]");
            List<DeviceItem> pcs = targetList.FindAll(d => d.Type == "PC");
            foreach (var pc in pcs)
            {
                RemoveWolRetryQueue(pc.Id);
                pc.LastShutdownTime = DateTime.Now;
                pc.RuntimeStatus = "OFFLINE";
                RefreshVisualDashboard();
                AppendLog(string.Format(">> (TCP Agent Command) SHUTDOWN 신호 송신 시도 ➡️ IP: {0} [{1}]", pc.IpAddress, pc.Name));
                
                #pragma warning disable 4014
                SendTcpShutdownCommandAsync(pc.IpAddress, pc.Port);
                #pragma warning restore 4014
                await Task.Delay(500);
            }

            AppendLog("[완충 대기] 안정적인 영상 분리를 위해 3초 대기합니다...");
            for (int i = 3; i > 0; i--)
            {
                AppendLog(string.Format("... {0}초", i));
                await Task.Delay(1000);
            }

            AppendLog("[2단계: 프로젝터 기기 PJLink OFF 신호 송신 및 쿨링 모니터링]");
            List<DeviceItem> projectors = targetList.FindAll(d => d.Type == "Projector");
            
            foreach (var pc in pcs)
            {
                List<DeviceItem> linkedProjs = devices.FindAll(d => d.Type == "Projector" && d.AssociatedDeviceId == pc.Id);
                if (!string.IsNullOrEmpty(pc.AssociatedDeviceId))
                {
                    var standardLinked = devices.Find(d => d.Id == pc.AssociatedDeviceId && d.Type == "Projector");
                    if (standardLinked != null && !linkedProjs.Contains(standardLinked)) linkedProjs.Add(standardLinked);
                }

                foreach (var proj in linkedProjs)
                {
                    if (!projectors.Contains(proj)) projectors.Add(proj);
                }
            }

            foreach (var proj in projectors)
            {
                proj.RuntimeStatus = "COOLING";
                proj.RemainingSeconds = 5;
                RefreshVisualDashboard();
                AppendLog(string.Format(">> (TCP PJLink OFF) 종료 및 쿨링 개시 명령어 전송 시도 ➡️ IP: {0} [{1}]", proj.IpAddress, proj.Name));

                #pragma warning disable 4014
                SendPJLinkCommandAsync(proj.IpAddress, proj.Port, "%1POWR 0");
                #pragma warning restore 4014
            }

            for (int s = 5; s >= 0; s--)
            {
                foreach (var proj in projectors)
                {
                    proj.RemainingSeconds = s;
                }
                RefreshVisualDashboard();
                await Task.Delay(1000);
            }

            foreach (var proj in projectors)
            {
                proj.RuntimeStatus = "OFFLINE";
                AppendLog(string.Format(">> {0} 쿨링 완료. 기기 대기 모드 진입 확인.", proj.Name));
            }
            RefreshVisualDashboard();
        }

        private void LockButtons(bool enable)
        {
            ThemeManager.SetControlEnabledState(zoneOnBtn, enable, isDarkMode);
            ThemeManager.SetControlEnabledState(zoneOffBtn, enable, isDarkMode);
            ThemeManager.SetControlEnabledState(autoScheduleCheckBox, enable, isDarkMode);
            ThemeManager.SetControlEnabledState(triggerScheduleTestBtn, enable, isDarkMode);
            ThemeManager.SetControlEnabledState(virtualDayComboBox, enable, isDarkMode);
            
            foreach (Control ctrl in tabFlowPanel.Controls)
            {
                Button btn = ctrl as Button;
                if (btn != null)
                {
                    ThemeManager.SetControlEnabledState(btn, enable, isDarkMode);
                }
            }
        }

        public bool IsAutoScheduleActive 
        { 
            get { return autoScheduleCheckBox != null && autoScheduleCheckBox.Checked; } 
            set { if (autoScheduleCheckBox != null) autoScheduleCheckBox.Checked = value; } 
        }
        public bool IsConfirmRequired { get; set; }

        public void SetDarkMode(bool dark)
        {
            isDarkMode = dark;
            ThemeManager.ApplyThemeTo(this, isDarkMode);
            ReColorLogHistory();
            SaveThemeSettings();
            PopulateSpaces();
            RefreshVisualDashboard();
            this.Invalidate(true);
        }

        public string GetLogText()
        {
            return logRichTextBox != null ? logRichTextBox.Text : "";
        }

        public void ClearLogHistory()
        {
            logHistory.Clear();
            if (logRichTextBox != null) logRichTextBox.Clear();
        }

        public void OpenGlobalSettingsDialog()
        {
            using (GlobalSettingsForm gsForm = new GlobalSettingsForm(this))
            {
                gsForm.ShowDialog(this);
            }
        }

    }
}
