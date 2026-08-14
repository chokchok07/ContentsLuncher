using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Web.Script.Serialization;

namespace ShowroomLauncher
{

    public class MainWindow : Form
    {
        private static readonly string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private List<ContentItem> contentList = new List<ContentItem>();
        private readonly Dictionary<string, RunningGroup> activeProcesses = new Dictionary<string, RunningGroup>();

        // Controls
        private Panel titlePanel;
        private Panel actionPanel;
        private Panel footerPanel;
        private Label titleLabel;
        private Button btnClose;
        private Button btnMinimize;
        private Button btnAdd;
        private Button btnKillAll;
        private Button btnExit;
        private Button btnScheduler;
        private FlowLayoutPanel gridPanel;
        private Label lblTotal;
        private Label lblRunning;
        private Timer statusTimer;

        private TextBox txtDeviceId;
        private Button btnApplyDeviceId;
        private Button btnPingTest;

        private CheckBox chkSoloMode;
        private bool isSoloMode = false;
        private CheckBox chkPowerController;
        private Panel pnlSolo;
        private Panel pnlDeviceId;
        private Label lblSoloDesc;
        private Label lblDeviceId;
        private bool isPowerControllerEnabled = true;
        private bool schedulerEnabled = false;
        private string autoStartContentId = "";
        private string autoShutdownTime = "";
        private bool isPcShutdown = false;
        private string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        // Theme and Autostart Controls
        private Button btnThemeToggle;
        private Panel pnlStatusBadge;
        private CheckBox chkAutostart;
        private Button btnViewLog;
        private Button btnViewOpLog;
        private bool isAutostartEnabled = false;
        private bool isDarkMode = true;

        // Theme Color Fields
        private Color colorCanvas;
        private Color colorCanvasSoft;
        private Color colorCard;
        private Color colorHairline;
        private Color colorHairlineSoft;
        private Color colorInk;
        private Color colorMuted;
        private Color colorPrimary;
        private Color colorPrimaryActive;
        private Color colorSuccess;
        private Color colorWarning;
        private Color colorError;

        // System Tray Components
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private bool isExiting = false;
        private IntPtr myDynamicIconHandle = IntPtr.Zero;
        private Icon myDynamicIcon = null;

        // Custom Titlebar drag states
        private bool isDragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        // TCP Shutdown Listener fields
        private TcpListener shutdownListener;
        private bool isShutdownListenerRunning = true;
        private int shutdownPort = 9999;

        // UDP Heartbeat fields
        private string controllerIp = "127.0.0.1";
        private string deviceId = "PC_01";
        private int heartbeatPort = 9998;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            LoadSettings();
            ApplyTheme();
            chkSoloMode.Checked = isSoloMode;
            chkPowerController.Checked = isPowerControllerEnabled;
            chkAutostart.Checked = StartupManager.IsStartupEnabled();
            txtDeviceId.Text = deviceId;
            ThemeManager.SetControlEnabledState(txtDeviceId, isPowerControllerEnabled, isDarkMode);
            ThemeManager.SetControlEnabledState(btnApplyDeviceId, isPowerControllerEnabled, isDarkMode);
            ThemeManager.SetControlEnabledState(btnPingTest, isPowerControllerEnabled, isDarkMode);
            LoadConfig();
            RenderCards();
            UpdateSchedulerButtonVisual();
            StartTimer();
            StartShutdownListener();
            StartHeartbeatSender();
        }

        private void StartHeartbeatSender()
        {
            System.Threading.ThreadPool.QueueUserWorkItem((state) =>
            {
                while (true)
                {
                    try
                    {
                        if (isPowerControllerEnabled && !string.IsNullOrEmpty(controllerIp))
                        {
                            using (UdpClient udpClient = new UdpClient())
                            {
                                byte[] data = Encoding.UTF8.GetBytes(string.Format("HEARTBEAT:{0}", deviceId));
                                udpClient.Send(data, data.Length, controllerIp, heartbeatPort);
                            }
                        }
                    }
                    catch { }
                    System.Threading.Thread.Sleep(5000);
                }
            });
        }

        private void StartShutdownListener()
        {
            if (!isPowerControllerEnabled) return;
            if (shutdownListener != null) return;

            isShutdownListenerRunning = true;
            System.Threading.ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    shutdownListener = new TcpListener(IPAddress.Any, shutdownPort);
                    shutdownListener.Start();

                    while (isShutdownListenerRunning)
                    {
                        using (TcpClient client = shutdownListener.AcceptTcpClient())
                        using (NetworkStream stream = client.GetStream())
                        {
                            byte[] buffer = new byte[256];
                            int read = stream.Read(buffer, 0, buffer.Length);
                            if (read > 0)
                            {
                                string command = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                                if (command == "CONN_TEST" || command.StartsWith("CONN_TEST"))
                                {
                                    byte[] resp = Encoding.UTF8.GetBytes("CONN_OK\n");
                                    stream.Write(resp, 0, resp.Length);
                                    stream.Flush();
                                }
                                else if (command == "SHUTDOWN")
                                {
                                    Program.LogOperation(string.Format("⚡ [네트워크 원격 제어] {0} 포트로부터 원격 종료(SHUTDOWN) 명령이 수신되었습니다. 20초 종료 카운트다운을 시작합니다.", shutdownPort));
                                    
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        using (ShutdownWarningForm warnForm = new ShutdownWarningForm(isDarkMode))
                                        {
                                            if (warnForm.ShowDialog() == DialogResult.OK)
                                            {
                                                Program.LogOperation("⚡ [네트워크 원격 제어] 사용자가 즉시종료를 클릭했거나 카운트가 완료되어 시스템을 종료합니다.");
                                                Process.Start("shutdown", "/s /f /t 0");
                                                isShutdownListenerRunning = false;
                                                Application.Exit();
                                            }
                                            else
                                            {
                                                Program.LogOperation("⚡ [네트워크 원격 제어] 사용자가 시스템 종료 취소(대피)를 클릭했습니다.");
                                            }
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 소켓이 강제 정지되었거나 닫혔을 때 조용히 루프 종료
                }
            });
        }

        private void StopShutdownListener()
        {
            isShutdownListenerRunning = false;
            if (shutdownListener != null)
            {
                try
                {
                    shutdownListener.Stop();
                }
                catch { }
                shutdownListener = null;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            AutoStartContentIfNeeded();
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(1280, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 19, 28); // Premium Dark Background
            this.Text = "Showroom Launcher";

            // Font Initialization
            Font fontPrimary = FontHelper.GetFont(9.75f, FontStyle.Regular);
            Font fontHeader = FontHelper.GetFont(12f, FontStyle.Bold);

            // Title Panel (Custom Title Bar)
            titlePanel = new Panel();
            titlePanel.Location = new Point(0, 0);
            titlePanel.Size = new Size(1280, 60);
            titlePanel.BackColor = Color.FromArgb(24, 25, 38);
            titlePanel.MouseDown += TitlePanel_MouseDown;
            titlePanel.MouseMove += TitlePanel_MouseMove;
            titlePanel.MouseUp += TitlePanel_MouseUp;
            titlePanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titlePanel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(colorHairlineSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, titlePanel.Height - 1, titlePanel.Width, titlePanel.Height - 1);
                }
            };

            // Logo Icon & Label
            titleLabel = new Label();
            titleLabel.Text = "🚀  Showroom Launcher";
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = fontHeader;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(20, 18);
            titleLabel.MouseDown += TitlePanel_MouseDown;
            titleLabel.MouseMove += TitlePanel_MouseMove;
            titleLabel.MouseUp += TitlePanel_MouseUp;
            titlePanel.Controls.Add(titleLabel);

            // Status Badge Panel
            pnlStatusBadge = new Panel();
            pnlStatusBadge.Location = new Point(230, 17);
            pnlStatusBadge.Size = new Size(145, 26);
            pnlStatusBadge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Color bg = isPowerControllerEnabled ? colorPrimary : (isDarkMode ? Color.FromArgb(55, 57, 84) : Color.FromArgb(230, 229, 224));
                Color fg = isPowerControllerEnabled ? Color.White : (isDarkMode ? Color.White : colorInk);

                using (SolidBrush brush = new SolidBrush(bg))
                {
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        int radius = 8;
                        path.AddArc(0, 0, radius, radius, 180, 90);
                        path.AddArc(pnlStatusBadge.Width - radius - 1, 0, radius, radius, 270, 90);
                        path.AddArc(pnlStatusBadge.Width - radius - 1, pnlStatusBadge.Height - radius - 1, radius, radius, 0, 90);
                        path.AddArc(0, pnlStatusBadge.Height - radius - 1, radius, radius, 90, 90);
                        path.CloseAllFigures();
                        e.Graphics.FillPath(brush, path);
                    }
                }

                string text = isPowerControllerEnabled ? "\uD83D\uDCE1 \uC81C\uC5B4\uAE30 \uC5F0\uB3D9 \uC911" : "\uD83D\uDCBB PC \uB2E8\uB3C5 \uC2E4\uD589 \uC911";
                using (Font font = FontHelper.GetFont(8.5f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(fg))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(text, font, textBrush, new RectangleF(0, 0, pnlStatusBadge.Width, pnlStatusBadge.Height), sf);
                }
            };
            titlePanel.Controls.Add(pnlStatusBadge);

            // Close button (Titlebar)
            btnClose = new Button();
            btnClose.Text = "✕";
            btnClose.Font = FontHelper.GetFont(11f, FontStyle.Bold);
            btnClose.ForeColor = Color.FromArgb(156, 163, 175);
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = colorError;
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(185, 28, 28);
            btnClose.Size = new Size(45, 60);
            btnClose.Location = new Point(this.Width - 45, 0);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click += (s, e) => this.Close();
            titlePanel.Controls.Add(btnClose);

            // Minimize button (Titlebar)
            btnMinimize = new Button();
            btnMinimize.Text = "—";
            btnMinimize.Font = FontHelper.GetFont(10f, FontStyle.Bold);
            btnMinimize.ForeColor = Color.FromArgb(156, 163, 175);
            btnMinimize.BackColor = Color.Transparent;
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 255, 255, 10);
            btnMinimize.Size = new Size(45, 60);
            btnMinimize.Location = new Point(this.Width - 90, 0);
            btnMinimize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            titlePanel.Controls.Add(btnMinimize);

            // Theme Toggle button (Titlebar)
            btnThemeToggle = new Button();
            btnThemeToggle.Text = isDarkMode ? "☀️" : "🌙";
            btnThemeToggle.Font = new Font("Segoe UI Emoji", 10f, FontStyle.Bold);
            btnThemeToggle.ForeColor = Color.FromArgb(156, 163, 175);
            btnThemeToggle.BackColor = Color.Transparent;
            btnThemeToggle.FlatStyle = FlatStyle.Flat;
            btnThemeToggle.FlatAppearance.BorderSize = 0;
            btnThemeToggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 255, 255, 10);
            btnThemeToggle.Size = new Size(45, 60);
            btnThemeToggle.Location = new Point(this.Width - 135, 0);
            btnThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnThemeToggle.Cursor = Cursors.Hand;
            btnThemeToggle.Click += (s, e) =>
            {
                isDarkMode = !isDarkMode;
                SaveSettings();
                ApplyTheme();
            };
            titlePanel.Controls.Add(btnThemeToggle);

            this.Controls.Add(titlePanel);

            // Global Actions Panel (Below title panel)
            actionPanel = new Panel();
            actionPanel.Location = new Point(0, 60);
            actionPanel.Size = new Size(1280, 80);
            actionPanel.BackColor = Color.FromArgb(18, 19, 28);
            actionPanel.Padding = new Padding(20, 20, 20, 20);
            actionPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            actionPanel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(colorHairlineSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, actionPanel.Height - 1, actionPanel.Width, actionPanel.Height - 1);
                }
            };

            // Add Content Button
            btnAdd = new Button();
            btnAdd.Text = "➕  콘텐츠 추가";
            btnAdd.Font = FontHelper.GetFont(10f, FontStyle.Bold);
            btnAdd.BackColor = Color.FromArgb(35, 37, 54);
            btnAdd.ForeColor = Color.FromArgb(243, 244, 246);
            btnAdd.FlatStyle = FlatStyle.Flat;
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Size = new Size(150, 40);
            btnAdd.Location = new Point(20, 20);
            btnAdd.Cursor = Cursors.Hand;
            btnAdd.Click += BtnAdd_Click;
            actionPanel.Controls.Add(btnAdd);

            // Kill All Button
            btnKillAll = new Button();
            btnKillAll.Text = "⏹️  전체 종료 (Kill All)";
            btnKillAll.Font = FontHelper.GetFont(10f, FontStyle.Bold);
            btnKillAll.BackColor = colorError;
            btnKillAll.ForeColor = Color.White;
            btnKillAll.FlatStyle = FlatStyle.Flat;
            btnKillAll.FlatAppearance.BorderSize = 0;
            btnKillAll.Size = new Size(200, 40);
            btnKillAll.Location = new Point(185, 20);
            btnKillAll.Cursor = Cursors.Hand;
            btnKillAll.Click += BtnKillAll_Click;
            actionPanel.Controls.Add(btnKillAll);

            // Exit Launcher Button
            btnExit = new Button();
            btnExit.Text = "❌  런처 종료 (Exit)";
            btnExit.Font = FontHelper.GetFont(10f, FontStyle.Bold);
            btnExit.BackColor = Color.FromArgb(35, 37, 54);
            btnExit.ForeColor = Color.FromArgb(243, 244, 246);
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.FlatAppearance.BorderSize = 1;
            btnExit.FlatAppearance.BorderColor = colorError;
            btnExit.Size = new Size(160, 40);
            btnExit.Location = new Point(395, 20);
            btnExit.Cursor = Cursors.Hand;
            btnExit.Click += (s, e) => {
                isExiting = true;
                this.Close();
            };
            actionPanel.Controls.Add(btnExit);

            // ----------------------------------------------------
            // GLOBAL CONFIG OPTIONS (Premium Rounded Mini Card Boxes)
            // ----------------------------------------------------
            Color textLabelColor = Color.FromArgb(156, 163, 175);
            Font fontOption = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontOptionDesc = FontHelper.GetFont(7.5f, FontStyle.Regular);

            // 1) Solo Mode Card Panel (Sized 265x60)
            pnlSolo = new DoubleBufferedPanel();
            pnlSolo.Size = new Size(265, 60);
            pnlSolo.Location = new Point(990, 10);
            pnlSolo.BackColor = Color.FromArgb(24, 25, 38);
            pnlSolo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlSolo.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 8;
                    path.AddArc(0, 0, radius, radius, 180, 90);
                    path.AddArc(pnlSolo.Width - radius - 1, 0, radius, radius, 270, 90);
                    path.AddArc(pnlSolo.Width - radius - 1, pnlSolo.Height - radius - 1, radius, radius, 0, 90);
                    path.AddArc(0, pnlSolo.Height - radius - 1, radius, radius, 90, 90);
                    path.CloseAllFigures();

                    using (Pen borderPen = new Pen(colorHairline, 1f))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }
            };

            chkSoloMode = new CheckBox();
            chkSoloMode.Text = "🛡️ 단독 실행 모드";
            chkSoloMode.ForeColor = Color.White;
            chkSoloMode.Font = fontOption;
            chkSoloMode.Location = new Point(10, 8);
            chkSoloMode.Size = new Size(160, 22);
            chkSoloMode.Cursor = Cursors.Hand;
            chkSoloMode.CheckedChanged += (s, e) => {
                isSoloMode = chkSoloMode.Checked;
                SaveSettings();
            };
            pnlSolo.Controls.Add(chkSoloMode);

            lblSoloDesc = new Label();
            lblSoloDesc.Text = "새 빌드 실행 시, 기존 실행 중인 시연 빌드 자동 종료";
            lblSoloDesc.ForeColor = textLabelColor;
            lblSoloDesc.Font = fontOptionDesc;
            lblSoloDesc.Location = new Point(10, 36);
            lblSoloDesc.AutoSize = true;
            pnlSolo.Controls.Add(lblSoloDesc);

            // 2) Device ID Settings Card Panel (Sized 365x60)
            pnlDeviceId = new DoubleBufferedPanel();
            pnlDeviceId.Size = new Size(365, 60);
            pnlDeviceId.Location = new Point(605, 10);
            pnlDeviceId.BackColor = Color.FromArgb(24, 25, 38);
            pnlDeviceId.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlDeviceId.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 8;
                    path.AddArc(0, 0, radius, radius, 180, 90);
                    path.AddArc(pnlDeviceId.Width - radius - 1, 0, radius, radius, 270, 90);
                    path.AddArc(pnlDeviceId.Width - radius - 1, pnlDeviceId.Height - radius - 1, radius, radius, 0, 90);
                    path.AddArc(0, pnlDeviceId.Height - radius - 1, radius, radius, 90, 90);
                    path.CloseAllFigures();

                    using (Pen borderPen = new Pen(colorHairline, 1f))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }
            };

            chkPowerController = new CheckBox();
            chkPowerController.Text = "📡 제어기 연동";
            chkPowerController.ForeColor = Color.White;
            chkPowerController.Font = fontOption;
            chkPowerController.Location = new Point(10, 6);
            chkPowerController.Size = new Size(115, 20);
            chkPowerController.Cursor = Cursors.Hand;
            chkPowerController.CheckedChanged += (s, e) => {
                isPowerControllerEnabled = chkPowerController.Checked;
                ThemeManager.SetControlEnabledState(txtDeviceId, isPowerControllerEnabled, isDarkMode);
                ThemeManager.SetControlEnabledState(btnApplyDeviceId, isPowerControllerEnabled, isDarkMode);
                ThemeManager.SetControlEnabledState(btnPingTest, isPowerControllerEnabled, isDarkMode);
                if (pnlStatusBadge != null)
                {
                    pnlStatusBadge.Invalidate();
                }
                SaveSettings();
                
                if (isPowerControllerEnabled)
                {
                    StartShutdownListener();
                }
                else
                {
                    StopShutdownListener();
                }
            };
            pnlDeviceId.Controls.Add(chkPowerController);

            lblDeviceId = new Label();
            lblDeviceId.Text = "(기기 ID)";
            lblDeviceId.ForeColor = textLabelColor;
            lblDeviceId.Font = fontOptionDesc;
            lblDeviceId.Location = new Point(130, 9);
            lblDeviceId.AutoSize = true;
            pnlDeviceId.Controls.Add(lblDeviceId);

            txtDeviceId = new TextBox();
            txtDeviceId.Text = deviceId;
            txtDeviceId.Size = new Size(120, 20);
            txtDeviceId.Location = new Point(10, 32);
            txtDeviceId.BackColor = Color.FromArgb(18, 19, 28);
            txtDeviceId.ForeColor = Color.White;
            txtDeviceId.BorderStyle = BorderStyle.None;
            txtDeviceId.Font = fontOption;
            pnlDeviceId.Controls.Add(txtDeviceId);

            btnApplyDeviceId = new Button();
            btnApplyDeviceId.Text = "적용";
            btnApplyDeviceId.Size = new Size(70, 26);
            btnApplyDeviceId.Location = new Point(140, 29);
            btnApplyDeviceId.FlatStyle = FlatStyle.Flat;
            btnApplyDeviceId.FlatAppearance.BorderSize = 1;
            btnApplyDeviceId.FlatAppearance.BorderColor = colorPrimary;
            btnApplyDeviceId.BackColor = Color.FromArgb(24, 25, 38);
            btnApplyDeviceId.ForeColor = colorPrimary;
            btnApplyDeviceId.Font = fontOption;
            btnApplyDeviceId.Cursor = Cursors.Hand;
            btnApplyDeviceId.Click += (s, e) =>
            {
                string newId = txtDeviceId.Text.Trim();
                if (string.IsNullOrEmpty(newId))
                {
                    MessageBox.Show("기기 ID는 빈 칸으로 설정할 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                deviceId = newId;
                SaveSettings();
                MessageBox.Show(string.Format("기기 ID가 '{0}'(으)로 즉시 변경 및 저장되었습니다.\n제어기의 기기 고유 ID 정보와 일치시켜주세요.", newId), "기기 ID 변경 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            pnlDeviceId.Controls.Add(btnApplyDeviceId);

            btnPingTest = new Button();
            btnPingTest.Text = "연결 테스트";
            btnPingTest.Size = new Size(110, 26);
            btnPingTest.Location = new Point(220, 29);
            btnPingTest.FlatStyle = FlatStyle.Flat;
            btnPingTest.FlatAppearance.BorderSize = 1;
            btnPingTest.FlatAppearance.BorderColor = Color.FromArgb(16, 185, 129);
            btnPingTest.BackColor = Color.FromArgb(24, 25, 38);
            btnPingTest.ForeColor = Color.FromArgb(16, 185, 129);
            btnPingTest.Font = fontOption;
            btnPingTest.Cursor = Cursors.Hand;
            btnPingTest.Click += async (s, e) =>
            {
                btnPingTest.Enabled = false;
                btnPingTest.Text = "테스트중..";

                string currentId = txtDeviceId.Text.Trim();
                string targetIp = controllerIp;
                int targetPort = heartbeatPort;
                int localResponsePort = 9997;

                bool success = false;
                string errorMessage = "";

                await System.Threading.Tasks.Task.Run(() =>
                {
                    System.Net.Sockets.UdpClient udpClient = null;
                    System.Net.Sockets.UdpClient receiver = null;
                    try
                    {
                        // 1. 응답 수신용 리스너 생성 (포트 9997)
                        receiver = new System.Net.Sockets.UdpClient(localResponsePort);
                        receiver.Client.ReceiveTimeout = 2000; // 2초 타임아웃

                        // 2. 송신용 클라이언트 생성 및 패킷 전송
                        udpClient = new System.Net.Sockets.UdpClient();
                        byte[] pingBytes = Encoding.UTF8.GetBytes(string.Format("PING_TEST:{0}:{1}", currentId, localResponsePort));
                        udpClient.Send(pingBytes, pingBytes.Length, targetIp, targetPort);

                        // 3. 응답 대기
                        System.Net.IPEndPoint remoteEP = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                        byte[] pongBytes = receiver.Receive(ref remoteEP);
                        string pongMsg = Encoding.UTF8.GetString(pongBytes);

                        if (pongMsg.StartsWith("PONG_TEST:" + currentId))
                        {
                            success = true;
                        }
                        else
                        {
                            errorMessage = "이상한 응답 패킷 수신: " + pongMsg;
                        }
                    }
                    catch (System.Net.Sockets.SocketException ex)
                    {
                        if (ex.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut)
                        {
                            errorMessage = "응답 시간 초과 (제어판이 켜져 있지 않거나 방화벽/IP 설정을 확인하세요)";
                        }
                        else
                        {
                            errorMessage = "소켓 오류: " + ex.Message;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessage = "오류 발생: " + ex.Message;
                    }
                    finally
                    {
                        if (udpClient != null) udpClient.Close();
                        if (receiver != null) receiver.Close();
                    }
                });

                btnPingTest.Enabled = true;
                btnPingTest.Text = "연결 테스트";

                if (success)
                {
                    MessageBox.Show(string.Format("🟢 연결 성공!\n\n제어기(IP: {0})와의 UDP 양방향 핑-퐁 통신 및 방화벽 인아웃바운드가 정상 작동 중입니다.", targetIp), "연결 테스트 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(string.Format("🔴 연결 실패!\n\n사유: {0}\n\n[체크포인트]\n1. 제어기 컴퓨터 IP({1})가 맞는지 확인\n2. 제어판 프로그램이 구동 중인지 확인\n3. 대상 컴퓨터 및 본 PC 방화벽에서 UDP 포트 {2}, {3}이 허용되어 있는지 확인", errorMessage, targetIp, targetPort, localResponsePort), "연결 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            pnlDeviceId.Controls.Add(btnPingTest);

            actionPanel.Controls.Add(pnlSolo);
            actionPanel.Controls.Add(pnlDeviceId);

            this.Controls.Add(actionPanel);

            // Footer Panel
            footerPanel = new Panel();
            footerPanel.Location = new Point(0, 700);
            footerPanel.Size = new Size(1280, 50);
            footerPanel.BackColor = Color.FromArgb(24, 25, 38);
            footerPanel.Padding = new Padding(20, 15, 20, 15);
            footerPanel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(colorHairlineSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
                }
            };

            lblTotal = new Label();
            lblTotal.Text = "전체 콘텐츠: 0개";
            lblTotal.ForeColor = Color.FromArgb(156, 163, 175);
            lblTotal.Font = fontPrimary;
            lblTotal.AutoSize = true;
            lblTotal.Location = new Point(20, 16);
            footerPanel.Controls.Add(lblTotal);

            lblRunning = new Label();
            lblRunning.Text = "실행 중: 0개";
            lblRunning.ForeColor = Color.FromArgb(16, 185, 129);
            lblRunning.Font = FontHelper.GetFont(9.75f, FontStyle.Bold);
            lblRunning.AutoSize = true;
            lblRunning.Location = new Point(150, 16);
            footerPanel.Controls.Add(lblRunning);

            // Error Log Open Button
            btnViewLog = new Button();
            btnViewLog.Text = "\uD83D\uDCCB  에러 로그 열기";
            btnViewLog.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnViewLog.BackColor = Color.FromArgb(35, 37, 54);
            btnViewLog.ForeColor = Color.FromArgb(156, 163, 175);
            btnViewLog.FlatStyle = FlatStyle.Flat;
            btnViewLog.FlatAppearance.BorderSize = 1;
            btnViewLog.FlatAppearance.BorderColor = Color.FromArgb(55, 57, 84);
            btnViewLog.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 52, 74);
            btnViewLog.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 32, 48);
            btnViewLog.Size = new Size(150, 26);
            btnViewLog.Location = new Point(260, 12);
            btnViewLog.Cursor = Cursors.Hand;
            btnViewLog.Click += (s, e) => Program.ShowErrorLogFile();
            footerPanel.Controls.Add(btnViewLog);

            // Operation Log Open Button
            btnViewOpLog = new Button();
            btnViewOpLog.Text = "\uD83D\uDCCB  운용 로그 열기";
            btnViewOpLog.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnViewOpLog.BackColor = Color.FromArgb(35, 37, 54);
            btnViewOpLog.ForeColor = Color.FromArgb(156, 163, 175);
            btnViewOpLog.FlatStyle = FlatStyle.Flat;
            btnViewOpLog.FlatAppearance.BorderSize = 1;
            btnViewOpLog.FlatAppearance.BorderColor = Color.FromArgb(55, 57, 84);
            btnViewOpLog.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 52, 74);
            btnViewOpLog.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 32, 48);
            btnViewOpLog.Size = new Size(150, 26);
            btnViewOpLog.Location = new Point(420, 12);
            btnViewOpLog.Cursor = Cursors.Hand;
            btnViewOpLog.Click += (s, e) => Program.ShowOperationLogFile();
            footerPanel.Controls.Add(btnViewOpLog);

            // Auto Operation Settings Button
            btnScheduler = new Button();
            btnScheduler.Text = "\u23F0  자동 운용 설정";
            btnScheduler.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnScheduler.BackColor = Color.FromArgb(35, 37, 54);
            btnScheduler.ForeColor = colorMuted;
            btnScheduler.FlatStyle = FlatStyle.Flat;
            btnScheduler.FlatAppearance.BorderSize = 1;
            btnScheduler.FlatAppearance.BorderColor = colorHairline;
            btnScheduler.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 52, 74);
            btnScheduler.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 32, 48);
            btnScheduler.Size = new Size(190, 26);
            btnScheduler.Location = new Point(580, 12);
            btnScheduler.Cursor = Cursors.Hand;
            btnScheduler.Click += BtnScheduler_Click;
            footerPanel.Controls.Add(btnScheduler);

            // Autostart CheckBox
            chkAutostart = new CheckBox();
            chkAutostart.Text = "\uD83D\uDE80 PC 시작 시 자동 실행";
            chkAutostart.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            chkAutostart.Location = new Point(785, 14);
            chkAutostart.Size = new Size(180, 22);
            chkAutostart.Cursor = Cursors.Hand;
            chkAutostart.CheckedChanged += (s, e) =>
            {
                isAutostartEnabled = chkAutostart.Checked;
                SaveSettings();
                StartupManager.SetStartup(isAutostartEnabled);
            };
            footerPanel.Controls.Add(chkAutostart);

            Label lblVersion = new Label();
            lblVersion.Text = "v1.2.0 (Dual Theme Edition)";
            lblVersion.ForeColor = Color.FromArgb(107, 114, 128);
            lblVersion.Font = new Font("Consolas", 9f);
            lblVersion.AutoSize = true;
            lblVersion.Location = new Point(this.Width - 210, 16);
            lblVersion.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            footerPanel.Controls.Add(lblVersion);

            footerPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(footerPanel);

            // Main Grid Panel (FlowLayout for cards)
            gridPanel = new FlowLayoutPanel();
            gridPanel.Location = new Point(0, 140);
            gridPanel.Size = new Size(1280, 560);
            gridPanel.AutoScroll = true;
            gridPanel.Padding = new Padding(20, 10, 20, 10);
            gridPanel.BackColor = Color.FromArgb(18, 19, 28);
            gridPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(gridPanel);

            // Border Line (Aesthetic overlay border)
            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(colorHairline, 1.5f))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            // Form closed cleanup
            this.FormClosing += MainWindow_FormClosing;


        }

        // --- Custom dragging on TitlePanel ---
        private void TitlePanel_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void TitlePanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void TitlePanel_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        // --- CONFIG MANAGER ---
        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(configFilePath))
                {
                    File.WriteAllText(configFilePath, "[]", Encoding.UTF8);
                }

                string json;
                using (StreamReader sr = new StreamReader(configFilePath, Encoding.UTF8, true))
                {
                    json = sr.ReadToEnd();
                }
                contentList = ParseJson(json);
            }
            catch (Exception ex)
            {
                Program.LogError("LoadConfig (설정 로드 실패)", ex);
                MessageBox.Show("설정 파일을 불러오는데 실패했습니다. 에러 로그를 확인해 주세요: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                contentList = new List<ContentItem>();
            }
            lblTotal.Text = "전체 콘텐츠: " + contentList.Count + "개";
        }

        private void SaveConfig()
        {
            try
            {
                string json = ToJson(contentList);
                File.WriteAllText(configFilePath, json, Encoding.UTF8);
                lblTotal.Text = "전체 콘텐츠: " + contentList.Count + "개";
            }
            catch (Exception ex)
            {
                Program.LogError("SaveConfig (설정 저장 실패)", ex);
                MessageBox.Show("설정을 저장하는데 실패했습니다. 에러 로그를 확인해 주세요: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- CARD RENDERER ---
        private void RenderCards()
        {
            gridPanel.SuspendLayout();
            gridPanel.Controls.Clear();

            if (contentList.Count == 0)
            {
                Label emptyLabel = new Label();
                emptyLabel.Text = "\n\n📂  등록된 시연 콘텐츠가 없습니다.\n좌측 상단의 '콘텐츠 추가' 버튼을 눌러 등록해 주세요.";
                emptyLabel.ForeColor = Color.FromArgb(156, 163, 175);
                emptyLabel.Font = FontHelper.GetFont(12f, FontStyle.Regular);
                emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
                emptyLabel.Size = new Size(800, 150);
                gridPanel.Controls.Add(emptyLabel);
            }
            else
            {
                foreach (var item in contentList)
                {
                    gridPanel.Controls.Add(CreateCardPanel(item));
                }
            }

            gridPanel.ResumeLayout();
            UpdateStatusDisplay();
        }

        private Panel CreateCardPanel(ContentItem item)
        {
            Panel card = new DoubleBufferedPanel();
            card.Size = new Size(335, 230);
            card.Margin = new Padding(10, 10, 10, 10);
            card.BackColor = Color.Transparent; 
            card.Name = "card_" + item.id;
            card.Tag = false; // IsRunning boolean stored here

            // Custom GDI+ Rounded corner rendering
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                RunningGroup group = null;
                lock (activeProcesses)
                {
                    if (activeProcesses.ContainsKey(item.id))
                    {
                        group = activeProcesses[item.id];
                    }
                }

                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 16;
                    path.AddArc(0, 0, radius, radius, 180, 90);
                    path.AddArc(card.Width - radius - 1, 0, radius, radius, 270, 90);
                    path.AddArc(card.Width - radius - 1, card.Height - radius - 1, radius, radius, 0, 90);
                    path.AddArc(0, card.Height - radius - 1, radius, radius, 90, 90);
                    path.CloseAllFigures();

                    // Fill Card
                    using (SolidBrush fillBrush = new SolidBrush(colorCard))
                    {
                        e.Graphics.FillPath(fillBrush, path);
                    }

                    // Border Glow depending on states
                    Color borderColor = colorHairline;
                    float borderWidth = 1.5f;
                    if (group != null)
                    {
                        if (group.IsStartingDelay)
                        {
                            borderColor = colorWarning; // Gold/Orange for module delay
                            borderWidth = 2.0f;
                        }
                        else
                        {
                            borderColor = colorPrimary; // Purple/Orange for main running
                            borderWidth = 2.0f;
                        }
                    }

                    using (Pen borderPen = new Pen(borderColor, borderWidth))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }

                // Draw Content Title with Ellipsis to prevent overflow
                using (Font fontTitle = FontHelper.GetFont(11f, FontStyle.Bold))
                using (SolidBrush brushTitle = new SolidBrush(colorInk))
                using (StringFormat sfTitle = new StringFormat())
                {
                    sfTitle.Trimming = StringTrimming.EllipsisCharacter;
                    sfTitle.FormatFlags = StringFormatFlags.NoWrap;
                    e.Graphics.DrawString(item.name, fontTitle, brushTitle, new RectangleF(94, 22, 142, 24), sfTitle);
                }

                // Draw Status Light & Text dynamically
                string statusText = "대기 중";
                Color statusColor = colorMuted;

                if (group != null)
                {
                    if (group.IsStartingDelay)
                    {
                        statusText = string.Format("모듈 대기 ({0}초)", group.RemainingDelay);
                        statusColor = colorWarning;
                    }
                    else
                    {
                        statusText = "실행 중";
                        statusColor = colorSuccess;
                    }
                }
                
                using (SolidBrush dotBrush = new SolidBrush(statusColor))
                {
                    e.Graphics.FillEllipse(dotBrush, 243, 29, 7, 7);
                }
                using (Font fontStatus = FontHelper.GetFont(8f, FontStyle.Bold))
                using (SolidBrush brushStatus = new SolidBrush(statusColor))
                {
                    e.Graphics.DrawString(statusText, fontStatus, brushStatus, new PointF(254, 25));
                }

                // Draw Description Text with Multi-line Ellipsis to prevent overflow
                string descText = string.IsNullOrEmpty(item.description) ? "등록된 콘텐츠 설명이 없습니다." : item.description;
                using (Font fontDesc = FontHelper.GetFont(9f))
                using (SolidBrush brushDesc = new SolidBrush(colorMuted))
                using (StringFormat sfDesc = new StringFormat())
                {
                    sfDesc.Trimming = StringTrimming.EllipsisCharacter;
                    e.Graphics.DrawString(descText, fontDesc, brushDesc, new RectangleF(18, 96, 300, 62), sfDesc);
                }
            };

            // Icon box (Image or Emoji support)
            Control iconControl = null;
            bool isIconPath = false;

            if (!string.IsNullOrEmpty(item.icon))
            {
                string lowerIcon = item.icon.ToLower();
                if (lowerIcon.EndsWith(".png") || lowerIcon.EndsWith(".jpg") || lowerIcon.EndsWith(".jpeg") || lowerIcon.EndsWith(".gif") || lowerIcon.EndsWith(".bmp") || item.icon.Contains("\\") || item.icon.Contains("/"))
                {
                    isIconPath = true;
                }
            }

            if (isIconPath)
            {
                PictureBox pbIcon = new PictureBox();
                pbIcon.SizeMode = PictureBoxSizeMode.Zoom;
                pbIcon.BackColor = colorCanvasSoft;
                pbIcon.Size = new Size(64, 64);
                pbIcon.Location = new Point(18, 18);
                
                bool loadSuccess = false;
                if (File.Exists(item.icon))
                {
                    try
                    {
                        using (FileStream fs = new FileStream(item.icon, FileMode.Open, FileAccess.Read))
                        {
                            pbIcon.Image = Image.FromStream(fs);
                        }
                        loadSuccess = true;
                    }
                    catch { }
                }

                if (loadSuccess)
                {
                    iconControl = pbIcon;
                }
                else
                {
                    Label lblIcon = new Label();
                    lblIcon.Text = "\u26A0\uFE0F";
                    lblIcon.Font = FontHelper.GetFont(24f);
                    lblIcon.BackColor = colorCanvasSoft;
                    lblIcon.ForeColor = colorInk;
                    lblIcon.TextAlign = ContentAlignment.MiddleCenter;
                    lblIcon.Size = new Size(64, 64);
                    lblIcon.Location = new Point(18, 18);
                    iconControl = lblIcon;
                }
            }
            else
            {
                Label lblIcon = new Label();
                lblIcon.Text = string.IsNullOrEmpty(item.icon) ? "\uD83D\uDE80" : item.icon;
                lblIcon.Font = FontHelper.GetFont(24f);
                lblIcon.BackColor = colorCanvasSoft;
                lblIcon.ForeColor = colorInk;
                lblIcon.TextAlign = ContentAlignment.MiddleCenter;
                lblIcon.Size = new Size(64, 64);
                lblIcon.Location = new Point(18, 18);
                iconControl = lblIcon;
            }

            // Quick rounded look for icon container via region
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(0, 0, 12, 12, 180, 90);
                path.AddArc(64 - 12, 0, 12, 12, 270, 90);
                path.AddArc(64 - 12, 64 - 12, 12, 12, 0, 90);
                path.AddArc(0, 64 - 12, 12, 12, 90, 90);
                iconControl.Region = new Region(path);
            }
            card.Controls.Add(iconControl);

            // Launch / Kill Action Button
            Button btnLaunch = new Button();
            btnLaunch.Name = "btnLaunch";
            btnLaunch.Font = FontHelper.GetFont(9f, FontStyle.Bold);
            btnLaunch.ForeColor = Color.White;
            btnLaunch.FlatStyle = FlatStyle.Flat;
            btnLaunch.FlatAppearance.BorderSize = 0;
            btnLaunch.Size = new Size(190, 36);
            btnLaunch.Location = new Point(18, 176);
            btnLaunch.Cursor = Cursors.Hand;

            RunningGroup curGroup = null;
            lock (activeProcesses)
            {
                if (activeProcesses.ContainsKey(item.id))
                {
                    curGroup = activeProcesses[item.id];
                }
            }

            if (curGroup != null)
            {
                if (curGroup.IsStartingDelay)
                {
                    btnLaunch.Text = string.Format("\u23F3 \uB300\uAE30 ({0}\uCE18)", curGroup.RemainingDelay);
                    btnLaunch.BackColor = colorWarning;
                }
                else
                {
                    btnLaunch.Text = "\u23F9\uFE0F  \uC985\uB550";
                    btnLaunch.BackColor = colorError;
                }
            }
            else
            {
                btnLaunch.Text = "\u25B6\uFE0F  \uC2E4\uD589";
                btnLaunch.BackColor = colorPrimary;
            }

            btnLaunch.Click += (s, e) => ToggleProcess(item, card, btnLaunch);
            card.Controls.Add(btnLaunch);

            // Edit Button
            Button btnEdit = new Button();
            btnEdit.Text = "\u270F\uFE0F";
            btnEdit.Font = new Font("Segoe UI Emoji", 9f);
            btnEdit.BackColor = colorCard;
            btnEdit.ForeColor = colorMuted;
            btnEdit.FlatStyle = FlatStyle.Flat;
            btnEdit.FlatAppearance.BorderSize = 1;
            btnEdit.FlatAppearance.BorderColor = colorHairline;
            btnEdit.Size = new Size(45, 36);
            btnEdit.Location = new Point(218, 176);
            btnEdit.Cursor = Cursors.Hand;
            btnEdit.Click += (s, e) => EditContent(item);
            card.Controls.Add(btnEdit);

            // Delete Button
            Button btnDel = new Button();
            btnDel.Text = "\uD83D\uDDD1\uFE0F";
            btnDel.Font = new Font("Segoe UI Emoji", 9f);
            btnDel.BackColor = colorCard;
            btnDel.ForeColor = colorMuted;
            btnDel.FlatStyle = FlatStyle.Flat;
            btnDel.FlatAppearance.BorderSize = 1;
            btnDel.FlatAppearance.BorderColor = colorHairline;
            btnDel.Size = new Size(45, 36);
            btnDel.Location = new Point(273, 176);
            btnDel.Cursor = Cursors.Hand;
            btnDel.Click += (s, e) => DeleteContent(item);
            card.Controls.Add(btnDel);

            return card;
        }

        // --- ACTIONS ---
        private void ToggleProcess(ContentItem item, Panel card, Button btnLaunch)
        {
            bool isRunning = false;
            RunningGroup group = null;
            lock (activeProcesses)
            {
                if (activeProcesses.ContainsKey(item.id))
                {
                    group = activeProcesses[item.id];
                    isRunning = true;
                }
            }

            if (isRunning && group != null)
            {
                // Kill processes
                if (group.MainProcess != null)
                {
                    try { if (!group.MainProcess.HasExited) group.MainProcess.Kill(); } catch { }
                }
                if (group.ModuleProcess != null)
                {
                    try { if (!group.ModuleProcess.HasExited) group.ModuleProcess.Kill(); } catch { }
                }
                KillProcessesByName(group.MainPath); // 메인 프로세스 이름 기반 강제 종료
                KillProcessesByName(group.ModulePath); // 선행 모듈 프로세스 이름 기반 강제 종료

                lock (activeProcesses)
                {
                    activeProcesses.Remove(item.id);
                }

                btnLaunch.Text = "▶️  실행";
                btnLaunch.BackColor = colorPrimary;
                card.Tag = false;
                card.Invalidate();
            }
            else
            {
                // 단독 실행 모드 활성화 시 기존 프로세스 일괄 정리
                if (isSoloMode)
                {
                    KillAllActive();
                }

                // Launch process
                if (!File.Exists(item.path))
                {
                    MessageBox.Show("지정된 실행 파일이 존재하지 않습니다:\n" + item.path, "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                RunningGroup newGroup = new RunningGroup();
                newGroup.ModulePath = item.modulePath; // 모듈 경로 백업 저장
                newGroup.MainPath = item.path; // 메인 경로 백업 저장
                bool hasModule = !string.IsNullOrEmpty(item.modulePath) && File.Exists(item.modulePath);

                if (hasModule)
                {
                    try
                    {
                        ProcessStartInfo psiModule = new ProcessStartInfo
                        {
                            FileName = item.modulePath,
                            WorkingDirectory = Path.GetDirectoryName(item.modulePath),
                            UseShellExecute = true
                        };

                        newGroup.ModuleProcess = Process.Start(psiModule);
                        newGroup.IsStartingDelay = true;
                        newGroup.RemainingDelay = item.moduleDelay > 0 ? item.moduleDelay : 5;
                    }
                    catch (Exception ex)
                    {
                        Program.LogError(string.Format("ToggleProcess - '{0}' 선행 모듈({1}) 시작 실패", item.name, item.modulePath), ex);
                        MessageBox.Show("선행 모듈 프로그램 시작에 실패했습니다. 에러 로그를 확인해 주세요: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        ProcessStartInfo psiMain = new ProcessStartInfo
                        {
                            FileName = item.path,
                            WorkingDirectory = Path.GetDirectoryName(item.path),
                            UseShellExecute = true
                        };

                        Process proc = Process.Start(psiMain);
                        if (proc == null)
                        {
                            string mainProcName = Path.GetFileNameWithoutExtension(item.path);
                            Process[] procs = Process.GetProcessesByName(mainProcName);
                            if (procs.Length > 0) proc = procs[0];
                        }
                        newGroup.MainProcess = proc;
                        newGroup.IsStartingDelay = false;
                        newGroup.RemainingDelay = 0;

                        // Minimize launcher on successful direct launch to give focus to the game/app
                        this.BeginInvoke((MethodInvoker)delegate {
                            this.WindowState = FormWindowState.Minimized;
                        });
                        StartFocusMonitorThread(proc, item.path, item.keepFocus);
                    }
                    catch (Exception ex)
                    {
                        Program.LogError(string.Format("ToggleProcess - '{0}' 메인 실행 파일({1}) 시작 실패", item.name, item.path), ex);
                        MessageBox.Show("콘텐츠 실행 중 에러가 발생했습니다. 에러 로그를 확인해 주세요: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                lock (activeProcesses)
                {
                    activeProcesses[item.id] = newGroup;
                }

                if (newGroup.IsStartingDelay)
                {
                    btnLaunch.Text = string.Format("⏳ 대기 ({0}초)", newGroup.RemainingDelay);
                    btnLaunch.BackColor = colorWarning;
                }
                else
                {
                    btnLaunch.Text = "⏹️  종료";
                    btnLaunch.BackColor = colorError;
                }

                card.Tag = true;
                card.Invalidate();
            }

            // Let timer do the direct state switch, but trigger immediate update for UI responsiveness
            UpdateStatusDisplay();
        }

        private void BtnKillAll_Click(object sender, EventArgs e)
        {
            if (activeProcesses.Count == 0)
            {
                MessageBox.Show("현재 실행 중인 시연 빌드가 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("정말로 실행 중인 모든 시연 빌드를 종료하시겠습니까?", "시연 전체 종료", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                KillAllActive();
            }
        }

        private void KillAllActive()
        {
            Program.LogWrite(string.Format("[{0}] [KillAllActive] Called. SoloMode: {1}, ActiveCount: {2}\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), isSoloMode, activeProcesses.Count));

            lock (activeProcesses)
            {
                foreach (var pair in activeProcesses)
                {
                    RunningGroup group = pair.Value;
                    if (group.MainProcess != null)
                    {
                        try { if (!group.MainProcess.HasExited) group.MainProcess.Kill(); } catch { }
                    }
                    if (group.ModuleProcess != null)
                    {
                        try { if (!group.ModuleProcess.HasExited) group.ModuleProcess.Kill(); } catch { }
                    }
                    KillProcessesByName(group.MainPath); // 메인 프로세스 이름 기반 강제 종료
                    KillProcessesByName(group.ModulePath); // 선행 모듈 프로세스 이름 기반 강제 종료
                }
                activeProcesses.Clear();
            }

            // 단독 실행 모드 등의 경우, 이름 기반으로 모든 등록 콘텐츠 추가 전수 정리 (래퍼/잃어버린 핸들 대응)
            foreach (var item in contentList)
            {
                KillProcessesByName(item.path);
                KillProcessesByName(item.modulePath);
            }

            CheckProcessStatuses();
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (EditForm editForm = new EditForm(null, isDarkMode))
            {
                if (editForm.ShowDialog(this) == DialogResult.OK)
                {
                    ContentItem newItem = editForm.ResultItem;
                    newItem.id = "content_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    contentList.Add(newItem);
                    SaveConfig();
                    RenderCards();
                }
            }
        }

        private void EditContent(ContentItem item)
        {
            using (EditForm editForm = new EditForm(item, isDarkMode))
            {
                if (editForm.ShowDialog(this) == DialogResult.OK)
                {
                    ContentItem edited = editForm.ResultItem;
                    item.name = edited.name;
                    item.icon = edited.icon;
                    item.description = edited.description;
                    item.path = edited.path;
                    item.modulePath = edited.modulePath;
                    item.moduleDelay = edited.moduleDelay;
                    item.keepFocus = edited.keepFocus;

                    SaveConfig();
                    RenderCards();
                }
            }
        }

        private void BtnScheduler_Click(object sender, EventArgs e)
        {
            using (SchedulerForm form = new SchedulerForm(contentList, schedulerEnabled, autoStartContentId, autoShutdownTime, isPcShutdown, isPowerControllerEnabled, isDarkMode))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    schedulerEnabled = form.SchedulerEnabled;
                    autoStartContentId = form.AutoStartContentId;
                    autoShutdownTime = form.AutoShutdownTime;
                    isPcShutdown = form.IsPcShutdown;
                    SaveSettings();
                    UpdateSchedulerButtonVisual();
                }
            }
        }

        private void UpdateSchedulerButtonVisual()
        {
            if (btnScheduler == null) return;
            try
            {
                btnScheduler.BackColor = colorCard;
                if (schedulerEnabled)
                {
                    btnScheduler.Text = "\u23F0  자동 운용 설정 (ON)";
                    btnScheduler.FlatAppearance.BorderColor = colorPrimary;
                    btnScheduler.ForeColor = isDarkMode ? Color.White : colorPrimary;
                }
                else
                {
                    btnScheduler.Text = "\u23F0  자동 운용 설정 (OFF)";
                    btnScheduler.FlatAppearance.BorderColor = colorHairline;
                    btnScheduler.ForeColor = colorMuted;
                }
            }
            catch { }
        }

        private void DeleteContent(ContentItem item)
        {
            if (MessageBox.Show(string.Format("정말로 '{0}' 콘텐츠를 실행기 목록에서 삭제하시겠습니까?\n(실제 실행 파일은 삭제되지 않습니다)", item.name), "콘텐츠 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                // Kill if running
                lock (activeProcesses)
                {
                    if (activeProcesses.ContainsKey(item.id))
                    {
                        try
                        {
                            RunningGroup group = activeProcesses[item.id];
                            if (group.MainProcess != null && !group.MainProcess.HasExited) group.MainProcess.Kill();
                            if (group.ModuleProcess != null && !group.ModuleProcess.HasExited) group.ModuleProcess.Kill();
                            KillProcessesByName(group.MainPath); // 메인 프로세스 이름 기반 강제 종료
                            KillProcessesByName(group.ModulePath); // 선행 모듈 프로세스 이름 기반 강제 종료
                        }
                        catch { }
                        activeProcesses.Remove(item.id);
                    }
                }

                contentList.Remove(item);
                SaveConfig();
                RenderCards();
            }
        }

        // --- TIMER & UPDATE REFRESH ---
        private void StartTimer()
        {
            statusTimer = new Timer();
            statusTimer.Interval = 1000; // 1 second
            statusTimer.Tick += (s, e) => CheckProcessStatuses();
            statusTimer.Start();
        }

        private void AutoStartContentIfNeeded()
        {
            if (!schedulerEnabled || string.IsNullOrEmpty(autoStartContentId)) return;



            ContentItem autoItem = contentList.Find(item => item.id == autoStartContentId);
            if (autoItem != null)
            {
                Control[] foundCards = gridPanel.Controls.Find("card_" + autoItem.id, true);
                if (foundCards.Length > 0)
                {
                    Panel card = (Panel)foundCards[0];
                    Control[] launchBtns = card.Controls.Find("btnLaunch", true);
                    if (launchBtns.Length > 0)
                    {
                        Button btnLaunch = (Button)launchBtns[0];
                        
                        try
                        {
                            Program.LogOperation("스케줄러에 의해 자동 시작 콘텐츠 '" + autoStartContentId + "'이(가) 트리거되었습니다.");
                        }
                        catch { }

                        this.BeginInvoke((MethodInvoker)delegate {
                            ToggleProcess(autoItem, card, btnLaunch);
                        });
                    }
                }
            }
        }

        private void CheckProcessStatuses()
        {
            // ⏰ 글로벌 자동 종료 스케줄러 시간 체크 (매초 실행)
            string currentTime = DateTime.Now.ToString("HH:mm");
            bool hasAutoShutdownTriggered = false;

            if (schedulerEnabled && !string.IsNullOrEmpty(autoShutdownTime) && autoShutdownTime == currentTime)
            {
                lock (activeProcesses)
                {
                    if (activeProcesses.Count > 0)
                    {
                        hasAutoShutdownTriggered = true;
                    }
                }
            }

            if (hasAutoShutdownTriggered)
            {
                Program.LogOperation("스케줄러 자동 종료 조건에 의해 모든 콘텐츠가 정상 종료되었습니다. (PC 종료: " + isPcShutdown.ToString() + ")");
                Program.LogWrite(string.Format("[{0}] Auto-shutdown triggered by global scheduler at {1}. Killing all active content.\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), autoShutdownTime));
                
                if (isPcShutdown)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("shutdown", "/s /t 30")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi);
                        Program.LogWrite(string.Format("[{0}] PC Shutdown triggered by global scheduler\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                    }
                    catch (Exception ex)
                    {
                        Program.LogWrite(string.Format("[{0}] Failed to trigger PC Shutdown: {1}\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), ex.Message));
                    }
                }

                KillAllActive();
                return;
            }

            List<string> exitedIds = new List<string>();
            List<ContentItem> toLaunchMains = new List<ContentItem>();

            lock (activeProcesses)
            {
                foreach (var pair in activeProcesses)
                {
                    string id = pair.Key;
                    RunningGroup group = pair.Value;

                    if (group.IsStartingDelay)
                    {

                        group.RemainingDelay--;
                        if (group.RemainingDelay <= 0)
                        {
                            group.IsStartingDelay = false;
                            ContentItem item = contentList.Find(c => c.id == id);
                            if (item != null)
                            {
                                toLaunchMains.Add(item);
                            }
                        }
                    }
                    else
                    {
                        if (group.MainProcess == null)
                        {
                            // 딜레이가 막 끝나서 null 인 찰나이거나 기동 렉으로 핸들을 획득하지 못한 경우, 이름 기반 긴급 복원 시도
                            ContentItem item = contentList.Find(c => c.id == id);
                            if (item != null)
                            {
                                string mainProcName = Path.GetFileNameWithoutExtension(item.path);
                                if (!string.IsNullOrEmpty(mainProcName))
                                {
                                    Process[] procs = Process.GetProcessesByName(mainProcName);
                                    if (procs.Length > 0)
                                    {
                                        group.MainProcess = procs[0]; // 실시간 감시 핸들 연결 성공!
                                    }
                                }
                            }

                            // 3초 유예 시간이 끝날 때까지도 끝내 프로세스를 찾지 못했다면 강제 정리
                            if (group.MainProcess == null)
                            {
                                if (group.GraceTicks <= 0)
                                {
                                    exitedIds.Add(id); // 기동 실패로 규정하고 클린업
                                }
                                else
                                {
                                    group.GraceTicks--; // 다음 틱에 재시도
                                }
                                continue;
                            }
                        }

                        // 기동 초기 3초 동안은 무조건 생사 감시를 유예하고 가동 상태를 강제 고정 보장 (래퍼 빌드 완벽 대응)
                        if (group.GraceTicks > 0)
                        {
                            group.GraceTicks--;
                            continue;
                        }

                        bool hasExited = false;
                        bool needsDoubleCheck = false;
                        try
                        {
                            if (group.MainProcess == null)
                            {
                                hasExited = true;
                                needsDoubleCheck = true;
                            }
                            else
                            {
                                group.MainProcess.Refresh();
                                hasExited = group.MainProcess.HasExited;
                            }
                        }
                        catch
                        {
                            hasExited = true; // 권한 오류/핸들 분실 시 일단 죽은 것으로 가정하고 아래 이름 검색 더블체크로 넘김
                            needsDoubleCheck = true;
                        }

                        // 더블 체크 (Double-Check): 핸들이 끊겼거나 권한 부족으로 에러가 났을 때만 OS 프로세스 리스트에서 이름 기반으로 재검색
                        if (hasExited && needsDoubleCheck)
                        {
                            ContentItem item = contentList.Find(c => c.id == id);
                            if (item != null)
                            {
                                string mainProcName = Path.GetFileNameWithoutExtension(item.path);
                                if (!string.IsNullOrEmpty(mainProcName))
                                {
                                    Process[] procs = Process.GetProcessesByName(mainProcName);
                                    if (procs.Length > 0)
                                    {
                                        group.MainProcess = procs[0]; // 실시간 프로세스로 핸들 갱신
                                        hasExited = false;
                                    }
                                }
                            }
                        }

                        if (hasExited)
                        {
                            exitedIds.Add(id);
                        }
                    }
                }

                foreach (string id in exitedIds)
                {
                    if (activeProcesses.ContainsKey(id))
                    {
                        RunningGroup group = activeProcesses[id];
                        if (group.ModuleProcess != null && !group.ModuleProcess.HasExited)
                        {
                            try { group.ModuleProcess.Kill(); } catch { }
                        }
                        activeProcesses.Remove(id);
                    }
                }
            }

            foreach (ContentItem item in toLaunchMains)
            {
                try
                {
                    ProcessStartInfo psiMain = new ProcessStartInfo
                    {
                        FileName = item.path,
                        WorkingDirectory = Path.GetDirectoryName(item.path),
                        UseShellExecute = true
                    };
                    Process proc = Process.Start(psiMain);
                    if (proc == null)
                    {
                        string mainProcName = Path.GetFileNameWithoutExtension(item.path);
                        Process[] procs = Process.GetProcessesByName(mainProcName);
                        if (procs.Length > 0) proc = procs[0];
                    }

                    lock (activeProcesses)
                    {
                        if (activeProcesses.ContainsKey(item.id))
                        {
                            activeProcesses[item.id].MainProcess = proc;
                        }
                    }

                    // Minimize launcher on successful delayed launch to give focus to the game/app
                    this.BeginInvoke((MethodInvoker)delegate {
                        this.WindowState = FormWindowState.Minimized;
                    });
                    StartFocusMonitorThread(proc, item.path, item.keepFocus);
                }
                catch (Exception ex)
                {
                    Program.LogError(string.Format("CheckProcessStatuses - '{0}' 지연 기동 메인 파일({1}) 실행 실패", item.name, item.path), ex);
                    MessageBox.Show(string.Format("'{0}' 콘텐츠의 메인 시연 빌드 실행에 실패했습니다. 에러 로그를 확인해 주세요: {1}", item.name, ex.Message), "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lock (activeProcesses)
                    {
                        if (activeProcesses.ContainsKey(item.id))
                        {
                            RunningGroup group = activeProcesses[item.id];
                            if (group.ModuleProcess != null && !group.ModuleProcess.HasExited)
                            {
                                try { group.ModuleProcess.Kill(); } catch { }
                            }
                            activeProcesses.Remove(item.id);
                        }
                    }
                }
            }

            bool anyExited = exitedIds.Count > 0;

            this.BeginInvoke((MethodInvoker)delegate {
                gridPanel.Invalidate(true);
                UpdateStatusDisplay();
                if (anyExited)
                {
                    RestoreWindow();
                }
            });
        }

        private void UpdateStatusDisplay()
        {
            int runningCount = 0;

            foreach (var item in contentList)
            {
                Control[] foundCards = gridPanel.Controls.Find("card_" + item.id, true);
                if (foundCards.Length == 0) continue;

                Panel card = (Panel)foundCards[0];
                RunningGroup group = null;
                lock (activeProcesses)
                {
                    if (activeProcesses.ContainsKey(item.id))
                    {
                        group = activeProcesses[item.id];
                    }
                }

                bool isRunning = (group != null);
                if (isRunning)
                {
                    runningCount++;
                }

                bool oldTag = false;
                if (card.Tag != null)
                {
                    oldTag = (bool)card.Tag;
                }

                card.Tag = isRunning;

                Control[] launchBtns = card.Controls.Find("btnLaunch", true);
                string expectedText = "▶️  실행";
                Color expectedColor = colorPrimary;

                if (group != null)
                {
                    if (group.IsStartingDelay)
                    {
                        expectedText = string.Format("⏳ 대기 ({0}초)", group.RemainingDelay);
                        expectedColor = colorWarning;
                    }
                    else
                    {
                        expectedText = "⏹️  종료";
                        expectedColor = colorError;
                    }
                }

                if (launchBtns.Length > 0)
                {
                    Button btn = (Button)launchBtns[0];
                    if (btn.Text != expectedText) btn.Text = expectedText;
                    if (btn.BackColor != expectedColor) btn.BackColor = expectedColor;
                }

                bool stateChanged = (oldTag != isRunning);
                bool isDelayActive = (group != null && group.IsStartingDelay);
                if (stateChanged || isDelayActive)
                {
                    card.Invalidate();
                }
            }

            lblRunning.Text = "실행 중: " + runningCount + "개";
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isExiting)
            {
                e.Cancel = true;
                this.Hide();
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(3000, "Showroom Launcher", "런처가 시스템 트레이(백그라운드)에서 계속 구동 중입니다.", ToolTipIcon.Info);
                    }
                }
                catch { }
            }
            else
            {
                if (isPowerControllerEnabled && !string.IsNullOrEmpty(controllerIp))
                {
                    try
                    {
                        using (UdpClient exitClient = new UdpClient())
                        {
                            byte[] exitBytes = Encoding.UTF8.GetBytes(string.Format("EXIT:{0}", deviceId));
                            exitClient.Send(exitBytes, exitBytes.Length, controllerIp, heartbeatPort);
                        }
                    }
                    catch { }
                }

                try
                {
                    isShutdownListenerRunning = false;
                    if (shutdownListener != null) shutdownListener.Stop();
                }
                catch { }

                if (activeProcesses.Count > 0)
                {
                    KillAllActive();
                }

                if (trayIcon != null)
                {
                    try
                    {
                        trayIcon.Visible = false;
                        trayIcon.Dispose();
                    }
                    catch { }
                }

                // 동적 생성한 아이콘 리소스 및 GDI 핸들 소멸 (메모리 누수 방지)
                if (myDynamicIcon != null)
                {
                    try
                    {
                        myDynamicIcon.Dispose();
                    }
                    catch { }
                }
                if (myDynamicIconHandle != IntPtr.Zero)
                {
                    try
                    {
                        DestroyIcon(myDynamicIconHandle);
                    }
                    catch { }
                }
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                trayMenu = new ContextMenu();
                trayMenu.MenuItems.Add("열기 (Open)", TrayMenu_Open);
                trayMenu.MenuItems.Add("전체 종료 (Kill All)", TrayMenu_KillAll);
                trayMenu.MenuItems.Add("-");
                trayMenu.MenuItems.Add("완전히 종료 (Exit)", TrayMenu_Exit);

                trayIcon = new NotifyIcon();
                trayIcon.Text = "Showroom Launcher";

                // 1) icon.png가 외부에 존재할 경우 우선 적용
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
                bool iconLoaded = false;
                if (File.Exists(iconPath))
                {
                    try
                    {
                        using (Bitmap bitmap = new Bitmap(iconPath))
                        {
                            bitmap.MakeTransparent(Color.White);
                            myDynamicIconHandle = bitmap.GetHicon();
                            myDynamicIcon = Icon.FromHandle(myDynamicIconHandle);
                            trayIcon.Icon = myDynamicIcon;
                            this.Icon = myDynamicIcon;
                            iconLoaded = true;
                        }
                    }
                    catch { }
                }

                // 2) 외부 이미지 파일이 없거나 로드 실패 시, Launcher.exe 자체에 내장된 리소스 아이콘 추출 적용 (단일 파일 배포 완벽 지원)
                if (!iconLoaded)
                {
                    try
                    {
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        Icon exeIcon = Icon.ExtractAssociatedIcon(exePath);
                        if (exeIcon != null)
                        {
                            trayIcon.Icon = exeIcon;
                            this.Icon = exeIcon;
                        }
                        else
                        {
                            trayIcon.Icon = SystemIcons.Application;
                        }
                    }
                    catch
                    {
                        trayIcon.Icon = SystemIcons.Application;
                    }
                }

                trayIcon.ContextMenu = trayMenu;
                trayIcon.Visible = true;
                trayIcon.DoubleClick += TrayIcon_DoubleClick;
            }
            catch (Exception ex)
            {
                Program.LogError("InitializeTrayIcon 실패", ex);
            }
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            RestoreWindow();
        }

        private void TrayMenu_Open(object sender, EventArgs e)
        {
            RestoreWindow();
        }

        private void TrayMenu_KillAll(object sender, EventArgs e)
        {
            BtnKillAll_Click(null, null);
        }

        private void TrayMenu_Exit(object sender, EventArgs e)
        {
            isExiting = true;
            this.Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.Q))
            {
                isExiting = true;
                this.Close();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                this.Hide();
                try
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(3000, "Showroom Launcher", "런처가 시스템 트레이(백그라운드)에서 계속 구동 중입니다.", ToolTipIcon.Info);
                    }
                }
                catch { }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
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

            if (m.Msg == WM_SHOWME)
            {
                RestoreWindow();
            }

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

        private void RestoreWindow()
        {
            try
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
                this.BringToFront();
                SetForegroundWindow(this.Handle);
            }
            catch { }
        }

        private static void KillProcessesByName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try
            {
                string procName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(procName)) return;

                string targetFullPath = Path.GetFullPath(filePath).ToLower();
                Program.LogWrite(string.Format("[KillProcessesByName] Target FilePath: {0} (ProcessName: {1})\n", filePath, procName));

                // Windows default Calculator App exception handling (calc.exe maps to CalculatorApp.exe)
                if (procName.ToLower() == "calc")
                {
                    foreach (var p in Process.GetProcessesByName("CalculatorApp"))
                    {
                        try 
                        { 
                            p.Kill(); 
                            Program.LogWrite(string.Format(" -> Killed CalculatorApp ID: {0}\n", p.Id));
                        } 
                        catch (Exception ex) { Program.LogWrite(string.Format(" -> Fail to kill CalculatorApp ID: {0}, Err: {1}\n", p.Id, ex.Message)); }
                    }
                }

                // 런처 자신은 당연히 종료 배제
                int currentProcId = Process.GetCurrentProcess().Id;

                Process[] targetProcs = Process.GetProcessesByName(procName);
                Program.LogWrite(string.Format(" -> Found {0} processes for name {1}\n", targetProcs.Length, procName));

                foreach (var p in targetProcs)
                {
                    if (p.Id == currentProcId) continue;

                    try 
                    { 
                        bool shouldKill = false;
                        string procFilePath = "";

                        try
                        {
                            procFilePath = p.MainModule.FileName;
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            // 권한 부족 등으로 인해 프로세스의 MainModule 경로 획득 실패 시 빈 값
                            procFilePath = "";
                        }
                        catch (Exception)
                        {
                            procFilePath = "";
                        }

                        if (!string.IsNullOrEmpty(procFilePath))
                        {
                            string fullProcPath = Path.GetFullPath(procFilePath).ToLower();
                            if (fullProcPath == targetFullPath)
                            {
                                shouldKill = true;
                            }
                            else
                            {
                                // 다른 경로의 동일 이름 프로세스는 Skip (예: 원격용 AnyDesk 등 보호)
                                Program.LogWrite(string.Format(" -> Skip ID: {0} (Path mismatch: {1} != {2})\n", p.Id, fullProcPath, targetFullPath));
                                shouldKill = false;
                            }
                        }
                        else
                        {
                            // 경로 획득에 실패한 윈도우 기본 도구는 예외적으로 셧다운 시도, 일반 프로세스는 오인 차단 방지를 위해 Skip
                            if (procName.ToLower() == "calc" || procName.ToLower() == "notepad")
                            {
                                shouldKill = true;
                            }
                            else
                            {
                                Program.LogWrite(string.Format(" -> Skip ID: {0} (Cannot verify file path - Access Denied)\n", p.Id));
                                shouldKill = false;
                            }
                        }

                        if (shouldKill)
                        {
                            p.Kill(); 
                            Program.LogWrite(string.Format(" -> Killed {0} ID: {1} (Path matched or default exception)\n", procName, p.Id));
                        }
                    } 
                    catch (Exception ex) 
                    { 
                        Program.LogWrite(string.Format(" -> Fail to process ID: {0}, Err: {1}\n", p.Id, ex.Message));
                    }
                }
            }
            catch (Exception ex) 
            {
                Program.LogWrite(string.Format("[KillProcessesByName ERROR] {0}\n", ex.Message));
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath, Encoding.UTF8);
                    var serializer = new JavaScriptSerializer();
                    var dict = serializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (dict != null)
                    {
                        if (dict.ContainsKey("isSoloMode")) isSoloMode = dict["isSoloMode"].ToLower() == "true";
                        if (dict.ContainsKey("powerControllerEnabled")) isPowerControllerEnabled = dict["powerControllerEnabled"].ToLower() == "true";
                        if (dict.ContainsKey("isDarkMode")) isDarkMode = dict["isDarkMode"].ToLower() == "true";
                        if (dict.ContainsKey("schedulerEnabled")) schedulerEnabled = dict["schedulerEnabled"].ToLower() == "true";
                        if (dict.ContainsKey("autoStartContentId")) autoStartContentId = dict["autoStartContentId"];
                        if (dict.ContainsKey("autoShutdownTime")) autoShutdownTime = dict["autoShutdownTime"];
                        if (dict.ContainsKey("isPcShutdown")) isPcShutdown = dict["isPcShutdown"].ToLower() == "true";
                        if (dict.ContainsKey("shutdownPort"))
                        {
                            int.TryParse(dict["shutdownPort"], out shutdownPort);
                        }
                        if (shutdownPort <= 0 || shutdownPort > 65535) shutdownPort = 9999;

                        if (dict.ContainsKey("controllerIp")) controllerIp = dict["controllerIp"];
                        if (dict.ContainsKey("deviceId")) deviceId = dict["deviceId"];
                        if (dict.ContainsKey("heartbeatPort"))
                        {
                            int.TryParse(dict["heartbeatPort"], out heartbeatPort);
                        }
                        if (heartbeatPort <= 0 || heartbeatPort > 65535) heartbeatPort = 9998;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError("LoadSettings 실패", ex);
            }
        }

        private void SaveSettings()
        {
            try
            {
                var dict = new Dictionary<string, string>();
                dict["isSoloMode"] = isSoloMode.ToString().ToLower();
                dict["powerControllerEnabled"] = isPowerControllerEnabled.ToString().ToLower();
                dict["isDarkMode"] = isDarkMode.ToString().ToLower();
                dict["schedulerEnabled"] = schedulerEnabled.ToString().ToLower();
                dict["autoStartContentId"] = autoStartContentId;
                dict["autoShutdownTime"] = autoShutdownTime;
                dict["isPcShutdown"] = isPcShutdown.ToString().ToLower();
                dict["shutdownPort"] = shutdownPort.ToString();
                dict["controllerIp"] = controllerIp;
                dict["deviceId"] = deviceId;
                dict["heartbeatPort"] = heartbeatPort.ToString();

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(dict);
                File.WriteAllText(settingsFilePath, json, Encoding.UTF8);

                if (schedulerEnabled)
                {
                    Program.LogWrite(string.Format(
                        "==================================================\n" +
                        "[{0}] [NORMAL INFO] Scheduler Settings Updated & Running\n" +
                        "상황: 사용자 조작으로 스케줄러 정보 변경 및 기동 완료\n" +
                        "자동 시작 타겟 ID: {1}\n" +
                        "자동 종료 지정 시간: {2} (PC 종료 설정: {3})\n" +
                        "==================================================\n\n",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        autoStartContentId,
                        autoShutdownTime,
                        isPcShutdown.ToString()
                    ));
                }
            }
            catch (Exception ex)
            {
                Program.LogError("SaveSettings 실패", ex);
            }
        }

        // --- COMPATIBLE JSON PARSER HELPERS ---
        private static List<ContentItem> ParseJson(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var rawList = serializer.Deserialize<List<Dictionary<string, object>>>(json);
                var list = new List<ContentItem>();
                if (rawList == null) return list;

                foreach (var dict in rawList)
                {
                    var item = new ContentItem();
                    if (dict.ContainsKey("id")) item.id = dict["id"] != null ? dict["id"].ToString() : "";
                    if (dict.ContainsKey("name")) item.name = dict["name"] != null ? dict["name"].ToString() : "";
                    if (dict.ContainsKey("icon")) item.icon = dict["icon"] != null ? dict["icon"].ToString() : "";
                    if (dict.ContainsKey("description")) item.description = dict["description"] != null ? dict["description"].ToString() : "";
                    if (dict.ContainsKey("path")) item.path = dict["path"] != null ? dict["path"].ToString() : "";
                    if (dict.ContainsKey("modulePath")) item.modulePath = dict["modulePath"] != null ? dict["modulePath"].ToString() : "";

                    int delayVal = 5;
                    if (dict.ContainsKey("moduleDelay"))
                    {
                        string delayStr = dict["moduleDelay"] != null ? dict["moduleDelay"].ToString() : "";
                        int.TryParse(delayStr, out delayVal);
                    }
                    item.moduleDelay = delayVal;

                    bool keepFocusVal = true; // 기본값 true로 설정하여 안전하게 포커스 보장
                    if (dict.ContainsKey("keepFocus"))
                    {
                        string keepFocusStr = dict["keepFocus"] != null ? dict["keepFocus"].ToString() : "";
                        if (!string.IsNullOrEmpty(keepFocusStr))
                        {
                            keepFocusVal = keepFocusStr.ToLower() == "true";
                        }
                    }
                    item.keepFocus = keepFocusVal;

                    if (!string.IsNullOrEmpty(item.id))
                    {
                        list.Add(item);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                Program.LogError("ParseJson 실패", ex);
                return new List<ContentItem>();
            }
        }

        private static string ToJson(List<ContentItem> items)
        {
            try
            {
                var rawList = new List<Dictionary<string, object>>();
                foreach (var item in items)
                {
                    var dict = new Dictionary<string, object>();
                    dict["id"] = item.id;
                    dict["name"] = item.name;
                    dict["icon"] = item.icon;
                    dict["description"] = item.description;
                    dict["path"] = item.path;
                    dict["modulePath"] = item.modulePath;
                    dict["moduleDelay"] = item.moduleDelay.ToString();
                    dict["keepFocus"] = item.keepFocus.ToString().ToLower();
                    rawList.Add(dict);
                }
                var serializer = new JavaScriptSerializer();
                return serializer.Serialize(rawList);
            }
            catch (Exception ex)
            {
                Program.LogError("ToJson 실패", ex);
                return "[]";
            }
        }

        // --- WIN32 API FOR FORCE FOCUSING ---
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, ref uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        internal static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME_SHOWROOM_LAUNCHER");

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int SW_SHOWNORMAL = 1;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private static void ActivateWindow(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero) return;

                if (IsIconic(hwnd))
                {
                    ShowWindowAsync(hwnd, SW_RESTORE);
                }
                else
                {
                    ShowWindowAsync(hwnd, SW_SHOW);
                }

                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);

                IntPtr foregroundHwnd = GetForegroundWindow();
                uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, IntPtr.Zero);
                uint currentThreadId = GetCurrentThreadId();
                uint targetThreadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);

                if (foregroundThreadId != targetThreadId)
                {
                    bool attached1 = false;
                    bool attached2 = false;
                    try
                    {
                        attached1 = AttachThreadInput(foregroundThreadId, targetThreadId, true);
                        attached2 = AttachThreadInput(currentThreadId, targetThreadId, true);
                        SetForegroundWindow(hwnd);
                    }
                    finally
                    {
                        if (attached1) AttachThreadInput(foregroundThreadId, targetThreadId, false);
                        if (attached2) AttachThreadInput(currentThreadId, targetThreadId, false);
                    }
                }
                else
                {
                    SetForegroundWindow(hwnd);
                }

                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
            }
            catch { }
        }

        private void StartFocusMonitorThread(Process proc, string targetPath, bool keepFocus)
        {
            if (proc == null && string.IsNullOrEmpty(targetPath)) return;

            System.Threading.Thread monitorThread = new System.Threading.Thread(() =>
            {
                try
                {
                    int timeoutMs = 10000;
                    int elapsedMs = 0;
                    int intervalMs = 200;
                    string procName = !string.IsNullOrEmpty(targetPath)
                        ? Path.GetFileNameWithoutExtension(targetPath) : null;

                    IntPtr hwnd = IntPtr.Zero;
                    while (elapsedMs < timeoutMs)
                    {
                        if (proc != null)
                        {
                            try
                            {
                                proc.Refresh();
                                if (!proc.HasExited)
                                    hwnd = proc.MainWindowHandle;
                            }
                            catch { }
                        }

                        if (hwnd == IntPtr.Zero && !string.IsNullOrEmpty(procName))
                        {
                            try
                            {
                                foreach (var p in Process.GetProcessesByName(procName))
                                {
                                    if (p.MainWindowHandle != IntPtr.Zero)
                                    {
                                        hwnd = p.MainWindowHandle;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (hwnd != IntPtr.Zero)
                        {
                            break;
                        }

                        System.Threading.Thread.Sleep(intervalMs);
                        elapsedMs += intervalMs;
                    }

                    if (hwnd == IntPtr.Zero) return;

                    // 최초 1회 포커스 부여
                    ActivateWindow(hwnd);

                    // 포커스 유지가 켜져 있으면, 타겟 프로세스가 살아있는 동안 주기적으로 최상단 포커스 유지
                    while (keepFocus)
                    {
                        if (proc != null)
                        {
                            try
                            {
                                proc.Refresh();
                                if (proc.HasExited) break;
                            }
                            catch { break; }
                        }
                        else if (!string.IsNullOrEmpty(procName))
                        {
                            try
                            {
                                Process[] procs = Process.GetProcessesByName(procName);
                                if (procs.Length == 0) break;
                            }
                            catch { break; }
                        }

                        System.Threading.Thread.Sleep(1000);

                        try
                        {
                            IntPtr fg = GetForegroundWindow();
                            uint fgPid = 0;
                            if (fg != IntPtr.Zero)
                            {
                                GetWindowThreadProcessId(fg, ref fgPid);
                            }
                            uint launcherPid = (uint)Process.GetCurrentProcess().Id;

                            if (fgPid == launcherPid)
                            {
                                // 1. 현재 조작 중인 창이 런처일 때는 포커스 유지상태를 풀어서 런처가 전면에 보이고 조작될 수 있도록 조치
                                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                            }
                            else
                            {
                                // 2. 그게 아닌 다른 일반 창이나 콘텐츠를 다룰 때는 항상 위에 떠 있는(HWND_TOPMOST) 포커스 유지 상태를 진행
                                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

                                // 포그라운드 창이 타겟 콘텐츠 창(hwnd)도 아닐 때는 포커스 강제 회수
                                if (fg != hwnd)
                                {
                                    ActivateWindow(hwnd);
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });

            monitorThread.IsBackground = true;
            monitorThread.Start();
        }



        private void ApplyTheme()
        {
            if (isDarkMode)
            {
                // Dark Mode (Sleek Slate Theme)
                colorCanvas = Color.FromArgb(18, 19, 28);
                colorCanvasSoft = Color.FromArgb(24, 25, 38);
                colorCard = Color.FromArgb(28, 29, 43);
                colorHairline = Color.FromArgb(37, 39, 54);
                colorHairlineSoft = Color.FromArgb(45, 47, 65);
                colorInk = Color.White;
                colorMuted = Color.FromArgb(156, 163, 175);
                colorPrimary = Color.FromArgb(123, 97, 255); // Purple
                colorPrimaryActive = Color.FromArgb(145, 125, 255);
                colorSuccess = Color.FromArgb(16, 185, 129);
                colorWarning = Color.FromArgb(245, 158, 11);
                colorError = Color.FromArgb(239, 68, 68);
            }
            else
            {
                // Light Mode (Cursor Warm Cream Theme)
                colorCanvas = Color.FromArgb(247, 247, 244);
                colorCanvasSoft = Color.FromArgb(250, 250, 247);
                colorCard = Color.FromArgb(255, 255, 255);
                colorHairline = Color.FromArgb(230, 229, 224);
                colorHairlineSoft = Color.FromArgb(239, 238, 232);
                colorInk = Color.FromArgb(38, 37, 30);
                colorMuted = Color.FromArgb(128, 125, 114);
                colorPrimary = Color.FromArgb(245, 78, 0); // Cursor Orange
                colorPrimaryActive = Color.FromArgb(208, 66, 0);
                colorSuccess = Color.FromArgb(31, 138, 101);
                colorWarning = Color.FromArgb(192, 133, 50);
                colorError = Color.FromArgb(207, 45, 86);
            }

            // Update Form and Panel backgrounds
            this.BackColor = colorCanvas;
            titlePanel.BackColor = colorCanvas;
            actionPanel.BackColor = colorCanvasSoft;
            footerPanel.BackColor = colorCanvas;
            gridPanel.BackColor = colorCanvas;

            // Update text colors
            titleLabel.ForeColor = colorInk;
            lblTotal.ForeColor = colorMuted;
            lblRunning.ForeColor = colorSuccess;
            chkSoloMode.ForeColor = colorInk;
            chkPowerController.ForeColor = colorInk;
            chkAutostart.ForeColor = colorInk;

            // Update text inputs and buttons via ThemeManager to handle enabled/disabled styles
            ThemeManager.SetControlEnabledState(txtDeviceId, isPowerControllerEnabled, isDarkMode);
            ThemeManager.SetControlEnabledState(btnApplyDeviceId, isPowerControllerEnabled, isDarkMode);
            ThemeManager.SetControlEnabledState(btnPingTest, isPowerControllerEnabled, isDarkMode);

            // Update settings cards backgrounds
            if (pnlSolo != null)
            {
                pnlSolo.BackColor = colorCard;
                pnlSolo.Invalidate();
            }
            if (pnlDeviceId != null)
            {
                pnlDeviceId.BackColor = colorCard;
                pnlDeviceId.Invalidate();
            }
            if (lblSoloDesc != null) lblSoloDesc.ForeColor = colorMuted;
            if (lblDeviceId != null) lblDeviceId.ForeColor = colorMuted;

            // Update buttons
            btnAdd.BackColor = colorPrimary;
            btnAdd.ForeColor = Color.White;

            btnKillAll.BackColor = colorError;
            btnKillAll.ForeColor = Color.White;

            btnExit.BackColor = colorCard;
            btnExit.ForeColor = colorInk;
            btnExit.FlatAppearance.BorderColor = colorHairline;

            btnViewLog.BackColor = colorCard;
            btnViewLog.ForeColor = colorMuted;
            btnViewLog.FlatAppearance.BorderColor = colorHairline;

            btnViewOpLog.BackColor = colorCard;
            btnViewOpLog.ForeColor = colorMuted;
            btnViewOpLog.FlatAppearance.BorderColor = colorHairline;

            // Header status badge repaint
            if (pnlStatusBadge != null)
            {
                pnlStatusBadge.Invalidate();
            }

            // Theme toggle button update
            btnThemeToggle.Text = isDarkMode ? "\u2600" : "\uD83C\uDF19";
            btnThemeToggle.ForeColor = colorMuted;

            UpdateSchedulerButtonVisual();
            RenderCards();
        }

    }
}
