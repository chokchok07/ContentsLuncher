using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Web.Script.Serialization;

[assembly: AssemblyTitle("Showroom Launcher")]
[assembly: AssemblyDescription("시연실 콘텐츠 통합 실행기")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Showroom Inc.")]
[assembly: AssemblyProduct("Showroom Launcher")]
[assembly: AssemblyCopyright("Copyright © 2026 Showroom Inc. All Rights Reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("83b53030-43d8-4686-a5ff-6062595ae1e4")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ShowroomLauncher
{
    public class ContentItem
    {
        public string id;
        public string name;
        public string icon;
        public string description;
        public string path;
        public string modulePath;
        public int moduleDelay;
        public bool keepFocus;
    }

    public class RunningGroup
    {
        public Process MainProcess;
        public Process ModuleProcess;
        public string ModulePath;
        public string MainPath;
        public bool IsStartingDelay;
        public int RemainingDelay;
        public int GraceTicks = 3; // 기동 후 3초간 생사 체크 유예
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
        }
    }

    // --- ShutdownWarningForm (종료 유예 카운트다운 알림 모달 창) ---
    public class ShutdownWarningForm : Form
    {
        private Label lblMessage;
        private Label lblTimer;
        private Button btnShutdown;
        private Button btnCancel;
        private Timer countdownTimer;
        private int remainingSeconds = 20;

        public ShutdownWarningForm()
        {
            this.Text = "⚠️ 시스템 자동 종료 예고";
            this.Size = new Size(450, 260);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(24, 25, 38);

            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(239, 68, 68), 2f))
                {
                    e.Graphics.DrawRectangle(borderPen, 1, 1, this.Width - 2, this.Height - 2);
                }
            };

            Font fontTitle = new Font("Malgun Gothic", 14f, FontStyle.Bold);
            Font fontText = new Font("Malgun Gothic", 10f);
            Font fontTimer = new Font("Malgun Gothic", 28f, FontStyle.Bold);
            Font fontButton = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);

            Label lblTitle = new Label()
            {
                Text = "⚠️  시스템 자동 종료 예고",
                Location = new Point(20, 20),
                Size = new Size(410, 30),
                ForeColor = Color.FromArgb(239, 68, 68),
                Font = fontTitle,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblMessage = new Label()
            {
                Text = "본 컴퓨터가 잠시 후 자동으로 종료될 예정입니다.\n저장하지 않은 모든 데이터는 유실될 수 있습니다.",
                Location = new Point(20, 60),
                Size = new Size(410, 45),
                ForeColor = Color.White,
                Font = fontText,
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblTimer = new Label()
            {
                Text = "20",
                Location = new Point(20, 110),
                Size = new Size(410, 50),
                ForeColor = Color.FromArgb(245, 158, 11),
                Font = fontTimer,
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnShutdown = new Button()
            {
                Text = "즉시 종료",
                Location = new Point(60, 185),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = fontButton
            };
            btnShutdown.FlatAppearance.BorderSize = 0;
            btnShutdown.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button()
            {
                Text = "종료 취소 (대피)",
                Location = new Point(250, 185),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(55, 57, 84),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = fontButton
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblMessage);
            this.Controls.Add(lblTimer);
            this.Controls.Add(btnShutdown);
            this.Controls.Add(btnCancel);

            countdownTimer = new Timer();
            countdownTimer.Interval = 1000;
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            remainingSeconds--;
            if (remainingSeconds <= 0)
            {
                countdownTimer.Stop();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                lblTimer.Text = remainingSeconds.ToString();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (countdownTimer != null)
                {
                    countdownTimer.Stop();
                    countdownTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            bool isNewInstance;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Global\\ShowroomLauncher_Unique_Mutex_Name", out isNewInstance))
            {
                if (!isNewInstance)
                {
                    try
                    {
                        // 1) FindWindow를 사용하여 숨겨진 창("Showroom Launcher")의 핸들을 우선 탐색
                        IntPtr hwnd = MainWindow.FindWindow(null, "Showroom Launcher");

                        // 2) 만약 못 찾았을 경우 대비하여 기존 process 기반 폴백
                        if (hwnd == IntPtr.Zero)
                        {
                            Process current = Process.GetCurrentProcess();
                            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                            {
                                if (process.Id != current.Id)
                                {
                                    hwnd = process.MainWindowHandle;
                                    if (hwnd != IntPtr.Zero) break;
                                }
                            }
                        }

                        // 3) 핸들을 성공적으로 찾았다면 메시지 전송 및 최상단 강제 활성화 시도
                        if (hwnd != IntPtr.Zero)
                        {
                            MainWindow.PostMessage(hwnd, MainWindow.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);

                            if (MainWindow.IsIconic(hwnd))
                            {
                                MainWindow.ShowWindowAsync(hwnd, 9); // SW_RESTORE
                            }
                            else
                            {
                                MainWindow.ShowWindowAsync(hwnd, 5); // SW_SHOW
                            }
                            MainWindow.SetForegroundWindow(hwnd);
                            MainWindow.SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
                            MainWindow.SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002);
                        }
                    }
                    catch { }


                    return;
                }

                // DPI Awareness 설정 (고해상도 모니터 배율 대응)
                try
                {
                    if (Environment.OSVersion.Version.Major >= 6)
                    {
                        SetProcessDPIAware();
                    }
                }
                catch { }

                // 전역 스레드 및 도메인 예외 처리기 등록 (미처리 예외 자동 로깅 및 메모장 호출)
                System.Windows.Forms.Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
                System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainWindow());
            }
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            LogError("UI 스레드 미처리 예외 (ThreadException)", e.Exception);
            ShowErrorLogFile();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                LogError("도메인 미처리 예외 (UnhandledException)", ex);
            }
            ShowErrorLogFile();
        }

        public static void LogWrite(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                if (File.Exists(logPath))
                {
                    FileInfo fi = new FileInfo(logPath);
                    if (fi.Length > 2 * 1024 * 1024) // 2MB 제한
                    {
                        File.WriteAllText(logPath, "--- 에러 로그 파일 초기화 (2MB 초과) ---\n\n", Encoding.UTF8);
                    }
                }
                File.AppendAllText(logPath, message, Encoding.UTF8);
            }
            catch { }
        }

        public static void LogError(string context, Exception ex)
        {
            try
            {
                string logMsg = string.Format(
                    "==================================================\n" +
                    "[{0}] {1}\n" +
                    "상황: {2}\n" +
                    "오류 메시지: {3}\n" +
                    "호출 스택 (Stack Trace):\n{4}\n" +
                    "==================================================\n\n",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ex.GetType().FullName,
                    context,
                    ex.Message,
                    ex.StackTrace
                );
                LogWrite(logMsg);
            }
            catch { }
        }

        public static void ShowErrorLogFile()
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                if (!File.Exists(logPath))
                {
                    File.WriteAllText(logPath, "--- Showroom Launcher 에러 로그 기록 ---\n\n", Encoding.UTF8);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = string.Format("\"{0}\"", logPath),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public static void LogOperation(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "operation_log.txt");
                if (File.Exists(logPath))
                {
                    FileInfo fi = new FileInfo(logPath);
                    if (fi.Length > 2 * 1024 * 1024) // 2MB 제한
                    {
                        File.WriteAllText(logPath, "--- 운용 로그 파일 초기화 (2MB 초과) ---\n\n", Encoding.UTF8);
                    }
                }
                string formatMsg = string.Format("[{0}] {1}\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), message);
                File.AppendAllText(logPath, formatMsg, Encoding.UTF8);
            }
            catch { }
        }

        public static void ShowOperationLogFile()
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "operation_log.txt");
                if (!File.Exists(logPath))
                {
                    File.WriteAllText(logPath, "--- Showroom Launcher 정상 운용 로그 기록 ---\n\n", Encoding.UTF8);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = string.Format("\"{0}\"", logPath),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }

    // --- MAIN WINDOW CLASS ---
    public class MainWindow : Form
    {
        private static readonly string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private List<ContentItem> contentList = new List<ContentItem>();
        private readonly Dictionary<string, RunningGroup> activeProcesses = new Dictionary<string, RunningGroup>();

        // Controls
        private Panel titlePanel;
        private Label titleLabel;
        private Button btnClose;
        private Button btnMinimize;
        private Button btnAdd;
        private Button btnKillAll;
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
        private bool isPowerControllerEnabled = true;
        private bool schedulerEnabled = false;
        private string autoStartContentId = "";
        private string autoShutdownTime = "";
        private bool isPcShutdown = false;
        private string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        
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
            chkSoloMode.Checked = isSoloMode;
            chkPowerController.Checked = isPowerControllerEnabled;
            txtDeviceId.Enabled = isPowerControllerEnabled;
            btnApplyDeviceId.Enabled = isPowerControllerEnabled;
            btnPingTest.Enabled = isPowerControllerEnabled;
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
                                if (command == "SHUTDOWN")
                                {
                                    Program.LogOperation(string.Format("⚡ [네트워크 원격 제어] {0} 포트로부터 원격 종료(SHUTDOWN) 명령이 수신되었습니다. 20초 종료 카운트다운을 시작합니다.", shutdownPort));
                                    
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        using (ShutdownWarningForm warnForm = new ShutdownWarningForm())
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
            Font fontPrimary = new Font("Malgun Gothic", 9.75f, FontStyle.Regular);
            Font fontHeader = new Font("Malgun Gothic", 12f, FontStyle.Bold);

            // Title Panel (Custom Title Bar)
            titlePanel = new Panel();
            titlePanel.Location = new Point(0, 0);
            titlePanel.Size = new Size(1280, 60);
            titlePanel.BackColor = Color.FromArgb(24, 25, 38);
            titlePanel.MouseDown += TitlePanel_MouseDown;
            titlePanel.MouseMove += TitlePanel_MouseMove;
            titlePanel.MouseUp += TitlePanel_MouseUp;
            titlePanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // Logo Icon & Label
            titleLabel = new Label();
            titleLabel.Text = "🚀   Showroom Launcher   |   시연실 콘텐츠 실행기";
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = fontHeader;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(20, 18);
            titleLabel.MouseDown += TitlePanel_MouseDown;
            titleLabel.MouseMove += TitlePanel_MouseMove;
            titleLabel.MouseUp += TitlePanel_MouseUp;
            titlePanel.Controls.Add(titleLabel);

            // Close button (Titlebar)
            btnClose = new Button();
            btnClose.Text = "✕";
            btnClose.Font = new Font("Malgun Gothic", 11f, FontStyle.Bold);
            btnClose.ForeColor = Color.FromArgb(156, 163, 175);
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 68, 68);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(185, 28, 28);
            btnClose.Size = new Size(45, 60);
            btnClose.Location = new Point(this.Width - 45, 0);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click += (s, e) => this.Close();
            titlePanel.Controls.Add(btnClose);

            // Minimize button (Titlebar)
            btnMinimize = new Button();
            btnMinimize.Text = "—";
            btnMinimize.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
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

            this.Controls.Add(titlePanel);

            // Global Actions Panel (Below title panel)
            Panel actionPanel = new Panel();
            actionPanel.Location = new Point(0, 60);
            actionPanel.Size = new Size(1280, 70);
            actionPanel.BackColor = Color.FromArgb(18, 19, 28);
            actionPanel.Padding = new Padding(20, 15, 20, 15);
            actionPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // Add Content Button
            btnAdd = new Button();
            btnAdd.Text = "➕  콘텐츠 추가";
            btnAdd.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
            btnAdd.BackColor = Color.FromArgb(35, 37, 54);
            btnAdd.ForeColor = Color.FromArgb(243, 244, 246);
            btnAdd.FlatStyle = FlatStyle.Flat;
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Size = new Size(150, 40);
            btnAdd.Location = new Point(20, 15);
            btnAdd.Cursor = Cursors.Hand;
            btnAdd.Click += BtnAdd_Click;
            actionPanel.Controls.Add(btnAdd);

            // Kill All Button
            btnKillAll = new Button();
            btnKillAll.Text = "⏹️  전체 종료 (Kill All)";
            btnKillAll.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
            btnKillAll.BackColor = Color.FromArgb(239, 68, 68);
            btnKillAll.ForeColor = Color.White;
            btnKillAll.FlatStyle = FlatStyle.Flat;
            btnKillAll.FlatAppearance.BorderSize = 0;
            btnKillAll.Size = new Size(200, 40);
            btnKillAll.Location = new Point(185, 15);
            btnKillAll.Cursor = Cursors.Hand;
            btnKillAll.Click += BtnKillAll_Click;
            actionPanel.Controls.Add(btnKillAll);

            // Exit Launcher Button
            Button btnExit = new Button();
            btnExit.Text = "❌  런처 종료 (Exit)";
            btnExit.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
            btnExit.BackColor = Color.FromArgb(35, 37, 54);
            btnExit.ForeColor = Color.FromArgb(243, 244, 246);
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.FlatAppearance.BorderSize = 1;
            btnExit.FlatAppearance.BorderColor = Color.FromArgb(239, 68, 68);
            btnExit.Size = new Size(160, 40);
            btnExit.Location = new Point(395, 15);
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
            Font fontOption = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Font fontOptionDesc = new Font("Malgun Gothic", 7.5f, FontStyle.Regular);

            // 1) Solo Mode Card Panel (Sized 265x52)
            Panel pnlSolo = new DoubleBufferedPanel();
            pnlSolo.Size = new Size(265, 52);
            pnlSolo.Location = new Point(990, 9);
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

                    using (Pen borderPen = new Pen(Color.FromArgb(50, 50, 70), 1f))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }
            };

            chkSoloMode = new CheckBox();
            chkSoloMode.Text = "🛡️ 단독 실행 모드";
            chkSoloMode.ForeColor = Color.White;
            chkSoloMode.Font = fontOption;
            chkSoloMode.Location = new Point(10, 6);
            chkSoloMode.Size = new Size(160, 22);
            chkSoloMode.Cursor = Cursors.Hand;
            chkSoloMode.CheckedChanged += (s, e) => {
                isSoloMode = chkSoloMode.Checked;
                SaveSettings();
            };
            pnlSolo.Controls.Add(chkSoloMode);

            Label lblSoloDesc = new Label();
            lblSoloDesc.Text = "새 빌드 실행 시, 기존 실행 중인 시연 빌드 자동 종료";
            lblSoloDesc.ForeColor = textLabelColor;
            lblSoloDesc.Font = fontOptionDesc;
            lblSoloDesc.Location = new Point(10, 30);
            lblSoloDesc.AutoSize = true;
            pnlSolo.Controls.Add(lblSoloDesc);

            // 2) Device ID Settings Card Panel (Sized 410x52)
            Panel pnlDeviceId = new DoubleBufferedPanel();
            pnlDeviceId.Size = new Size(410, 52);
            pnlDeviceId.Location = new Point(560, 9);
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

                    using (Pen borderPen = new Pen(Color.FromArgb(50, 50, 70), 1f))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }
            };

            chkPowerController = new CheckBox();
            chkPowerController.Text = "📡 제어기 연동";
            chkPowerController.ForeColor = Color.White;
            chkPowerController.Font = fontOption;
            chkPowerController.Location = new Point(10, 4);
            chkPowerController.Size = new Size(115, 20);
            chkPowerController.Cursor = Cursors.Hand;
            chkPowerController.CheckedChanged += (s, e) => {
                isPowerControllerEnabled = chkPowerController.Checked;
                txtDeviceId.Enabled = isPowerControllerEnabled;
                btnApplyDeviceId.Enabled = isPowerControllerEnabled;
                btnPingTest.Enabled = isPowerControllerEnabled;
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

            Label lblDeviceId = new Label();
            lblDeviceId.Text = "(기기 ID)";
            lblDeviceId.ForeColor = textLabelColor;
            lblDeviceId.Font = fontOptionDesc;
            lblDeviceId.Location = new Point(130, 7);
            lblDeviceId.AutoSize = true;
            pnlDeviceId.Controls.Add(lblDeviceId);

            txtDeviceId = new TextBox();
            txtDeviceId.Text = deviceId;
            txtDeviceId.Size = new Size(185, 20);
            txtDeviceId.Location = new Point(10, 24);
            txtDeviceId.BackColor = Color.FromArgb(18, 19, 28);
            txtDeviceId.ForeColor = Color.White;
            txtDeviceId.BorderStyle = BorderStyle.None;
            txtDeviceId.Font = fontOption;
            pnlDeviceId.Controls.Add(txtDeviceId);

            btnApplyDeviceId = new Button();
            btnApplyDeviceId.Text = "적용";
            btnApplyDeviceId.Size = new Size(70, 22);
            btnApplyDeviceId.Location = new Point(210, 22);
            btnApplyDeviceId.FlatStyle = FlatStyle.Flat;
            btnApplyDeviceId.FlatAppearance.BorderSize = 1;
            btnApplyDeviceId.FlatAppearance.BorderColor = Color.FromArgb(123, 97, 255);
            btnApplyDeviceId.BackColor = Color.FromArgb(24, 25, 38);
            btnApplyDeviceId.ForeColor = Color.FromArgb(123, 97, 255);
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
            btnPingTest.Size = new Size(95, 22);
            btnPingTest.Location = new Point(295, 22);
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
            Panel footerPanel = new Panel();
            footerPanel.Location = new Point(0, 700);
            footerPanel.Size = new Size(1280, 50);
            footerPanel.BackColor = Color.FromArgb(24, 25, 38);
            footerPanel.Padding = new Padding(20, 15, 20, 15);

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
            lblRunning.Font = new Font("Malgun Gothic", 9.75f, FontStyle.Bold);
            lblRunning.AutoSize = true;
            lblRunning.Location = new Point(150, 16);
            footerPanel.Controls.Add(lblRunning);

            // 📋 Error Log Open Button
            Button btnViewLog = new Button();
            btnViewLog.Text = "📋  에러 로그 열기";
            btnViewLog.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
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

            // 📋 Operation Log Open Button
            Button btnViewOpLog = new Button();
            btnViewOpLog.Text = "📋  운용 로그 열기";
            btnViewOpLog.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
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

            // ⏰ Auto Operation Settings Button
            btnScheduler = new Button();
            btnScheduler.Text = "⏰  자동 운용 설정";
            btnScheduler.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
            btnScheduler.BackColor = Color.FromArgb(35, 37, 54);
            btnScheduler.ForeColor = Color.FromArgb(156, 163, 175);
            btnScheduler.FlatStyle = FlatStyle.Flat;
            btnScheduler.FlatAppearance.BorderSize = 1;
            btnScheduler.FlatAppearance.BorderColor = Color.FromArgb(55, 57, 84);
            btnScheduler.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 52, 74);
            btnScheduler.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 32, 48);
            btnScheduler.Size = new Size(190, 26);
            btnScheduler.Location = new Point(580, 12);
            btnScheduler.Cursor = Cursors.Hand;
            btnScheduler.Click += BtnScheduler_Click;
            footerPanel.Controls.Add(btnScheduler);

            Label lblVersion = new Label();
            lblVersion.Text = "v1.0.0 (Native WinForms)";
            lblVersion.ForeColor = Color.FromArgb(107, 114, 128);
            lblVersion.Font = new Font("Consolas", 9f);
            lblVersion.AutoSize = true;
            lblVersion.Location = new Point(this.Width - 180, 16);
            lblVersion.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            footerPanel.Controls.Add(lblVersion);

            footerPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(footerPanel);

            // Main Grid Panel (FlowLayout for cards)
            gridPanel = new FlowLayoutPanel();
            gridPanel.Location = new Point(0, 130);
            gridPanel.Size = new Size(1280, 570);
            gridPanel.AutoScroll = true;
            gridPanel.Padding = new Padding(20, 10, 20, 10);
            gridPanel.BackColor = Color.FromArgb(18, 19, 28);
            gridPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(gridPanel); // DockStyle.Fill이 Top/Bottom Docking 패널들에 가려지지 않도록 Z-Order 맨 뒤로 이동

            // Border Line (Aesthetic overlay border)
            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(35, 37, 54), 1.5f))
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
                emptyLabel.Font = new Font("Malgun Gothic", 12f, FontStyle.Regular);
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
                    using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(28, 29, 43)))
                    {
                        e.Graphics.FillPath(fillBrush, path);
                    }

                    // Border Glow depending on states
                    Color borderColor = Color.FromArgb(37, 39, 54);
                    float borderWidth = 1.5f;
                    if (group != null)
                    {
                        if (group.IsStartingDelay)
                        {
                            borderColor = Color.FromArgb(245, 158, 11); // Gold/Orange for module delay
                            borderWidth = 2.0f;
                        }
                        else
                        {
                            borderColor = Color.FromArgb(123, 97, 255); // Purple for main running
                            borderWidth = 2.0f;
                        }
                    }

                    using (Pen borderPen = new Pen(borderColor, borderWidth))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }

                // Draw Content Title with Ellipsis to prevent overflow
                using (Font fontTitle = new Font("Malgun Gothic", 11f, FontStyle.Bold))
                using (SolidBrush brushTitle = new SolidBrush(Color.White))
                using (StringFormat sfTitle = new StringFormat())
                {
                    sfTitle.Trimming = StringTrimming.EllipsisCharacter;
                    sfTitle.FormatFlags = StringFormatFlags.NoWrap;
                    e.Graphics.DrawString(item.name, fontTitle, brushTitle, new RectangleF(94, 22, 142, 24), sfTitle);
                }

                // Draw Status Light & Text dynamically
                string statusText = "대기 중";
                Color statusColor = Color.FromArgb(156, 163, 175); // Grey

                if (group != null)
                {
                    if (group.IsStartingDelay)
                    {
                        statusText = string.Format("모듈 대기 ({0}초)", group.RemainingDelay);
                        statusColor = Color.FromArgb(245, 158, 11); // Gold
                    }
                    else
                    {
                        statusText = "실행 중";
                        statusColor = Color.FromArgb(16, 185, 129); // Green
                    }
                }
                
                using (SolidBrush dotBrush = new SolidBrush(statusColor))
                {
                    e.Graphics.FillEllipse(dotBrush, 243, 29, 7, 7);
                }
                using (Font fontStatus = new Font("Malgun Gothic", 8f, FontStyle.Bold))
                using (SolidBrush brushStatus = new SolidBrush(statusColor))
                {
                    e.Graphics.DrawString(statusText, fontStatus, brushStatus, new PointF(254, 25));
                }

                // Draw Description Text with Multi-line Ellipsis to prevent overflow
                string descText = string.IsNullOrEmpty(item.description) ? "등록된 콘텐츠 설명이 없습니다." : item.description;
                using (Font fontDesc = new Font("Malgun Gothic", 9f))
                using (SolidBrush brushDesc = new SolidBrush(Color.FromArgb(156, 163, 175)))
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
                pbIcon.BackColor = Color.FromArgb(35, 37, 54);
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
                    lblIcon.Text = "⚠️";
                    lblIcon.Font = new Font("Malgun Gothic", 24f);
                    lblIcon.BackColor = Color.FromArgb(35, 37, 54);
                    lblIcon.ForeColor = Color.White;
                    lblIcon.TextAlign = ContentAlignment.MiddleCenter;
                    lblIcon.Size = new Size(64, 64);
                    lblIcon.Location = new Point(18, 18);
                    iconControl = lblIcon;
                }
            }
            else
            {
                Label lblIcon = new Label();
                lblIcon.Text = string.IsNullOrEmpty(item.icon) ? "🚀" : item.icon;
                lblIcon.Font = new Font("Malgun Gothic", 24f);
                lblIcon.BackColor = Color.FromArgb(35, 37, 54);
                lblIcon.ForeColor = Color.White;
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
            btnLaunch.Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
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
                    btnLaunch.Text = string.Format("⏳ 대기 ({0}초)", curGroup.RemainingDelay);
                    btnLaunch.BackColor = Color.FromArgb(245, 158, 11);
                }
                else
                {
                    btnLaunch.Text = "⏹️  종료";
                    btnLaunch.BackColor = Color.FromArgb(239, 68, 68);
                }
            }
            else
            {
                btnLaunch.Text = "▶️  실행";
                btnLaunch.BackColor = Color.FromArgb(123, 97, 255);
            }

            btnLaunch.Click += (s, e) => ToggleProcess(item, card, btnLaunch);
            card.Controls.Add(btnLaunch);

            // Edit Button
            Button btnEdit = new Button();
            btnEdit.Text = "✏️";
            btnEdit.Font = new Font("Segoe UI Emoji", 9f);
            btnEdit.BackColor = Color.FromArgb(35, 37, 54);
            btnEdit.ForeColor = Color.FromArgb(156, 163, 175);
            btnEdit.FlatStyle = FlatStyle.Flat;
            btnEdit.FlatAppearance.BorderSize = 0;
            btnEdit.Size = new Size(45, 36);
            btnEdit.Location = new Point(218, 176);
            btnEdit.Cursor = Cursors.Hand;
            btnEdit.Click += (s, e) => EditContent(item);
            card.Controls.Add(btnEdit);

            // Delete Button
            Button btnDel = new Button();
            btnDel.Text = "🗑️";
            btnDel.Font = new Font("Segoe UI Emoji", 9f);
            btnDel.BackColor = Color.FromArgb(35, 37, 54);
            btnDel.ForeColor = Color.FromArgb(156, 163, 175);
            btnDel.FlatStyle = FlatStyle.Flat;
            btnDel.FlatAppearance.BorderSize = 0;
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
                btnLaunch.BackColor = Color.FromArgb(123, 97, 255);
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
                    btnLaunch.BackColor = Color.FromArgb(245, 158, 11);
                }
                else
                {
                    btnLaunch.Text = "⏹️  종료";
                    btnLaunch.BackColor = Color.FromArgb(239, 68, 68);
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
            using (EditForm editForm = new EditForm(null))
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
            using (EditForm editForm = new EditForm(item))
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
            using (SchedulerForm form = new SchedulerForm(contentList, schedulerEnabled, autoStartContentId, autoShutdownTime, isPcShutdown))
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
                if (schedulerEnabled)
                {
                    btnScheduler.Text = "⏰  자동 운용 설정 (ON)";
                    btnScheduler.FlatAppearance.BorderColor = Color.FromArgb(123, 97, 255); // 눈에 띄는 활성 보라색
                    btnScheduler.ForeColor = Color.White;
                }
                else
                {
                    btnScheduler.Text = "⏰  자동 운용 설정 (OFF)";
                    btnScheduler.FlatAppearance.BorderColor = Color.FromArgb(55, 57, 84); // 기본 어두운 회색
                    btnScheduler.ForeColor = Color.FromArgb(156, 163, 175);
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

            this.BeginInvoke((MethodInvoker)delegate {
                gridPanel.Invalidate(true);
                UpdateStatusDisplay();
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
                Color expectedColor = Color.FromArgb(123, 97, 255);

                if (group != null)
                {
                    if (group.IsStartingDelay)
                    {
                        expectedText = string.Format("⏳ 대기 ({0}초)", group.RemainingDelay);
                        expectedColor = Color.FromArgb(245, 158, 11);
                    }
                    else
                    {
                        expectedText = "⏹️  종료";
                        expectedColor = Color.FromArgb(239, 68, 68);
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

    }


    // --- DIALOG / EDIT FORM CLASS ---
    public class EditForm : Form
    {
        public ContentItem ResultItem { get; private set; }

        private TextBox txtName;
        private TextBox txtIcon;
        private TextBox txtDesc;
        private TextBox txtPath;
        private TextBox txtModulePath;
        private TextBox txtModuleDelay;
        private Button btnBrowse;
        private Button btnBrowseModule;
        private Button btnModuleOn;
        private Button btnModuleOff;
        private bool isModuleEnabled = false;
        private Button btnKeepFocusOn;
        private Button btnKeepFocusOff;
        private bool isKeepFocusEnabled = false;
        private Button btnSave;
        private Button btnCancel;

        private Panel pnlIconPreview;
        private Label lblEmojiPreview;
        private PictureBox pbImagePreview;

        public EditForm(ContentItem item)
        {
            InitializeComponent(item != null);

            if (item != null)
            {
                txtName.Text = item.name;
                txtIcon.Text = item.icon;
                txtDesc.Text = item.description;
                txtPath.Text = item.path;
                txtModulePath.Text = item.modulePath;
                txtModuleDelay.Text = item.moduleDelay.ToString();

                isModuleEnabled = !string.IsNullOrEmpty(item.modulePath);
                isKeepFocusEnabled = item.keepFocus;
            }
            ToggleModuleControls(isModuleEnabled);
            ToggleKeepFocus(isKeepFocusEnabled);

            txtIcon.TextChanged += (s, e) => UpdateIconPreview();
            UpdateIconPreview();
        }

        private void InitializeComponent(bool isEdit)
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(550, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 25, 38); // Premium Modal Background

            Font fontLabel = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Font fontInput = new Font("Malgun Gothic", 9.5f);
            Color textLabelColor = Color.FromArgb(156, 163, 175);
            Color inputBgColor = Color.FromArgb(15, 16, 25);
            Color inputForeColor = Color.White;

            // Custom Title Banner
            Panel modalHeader = new Panel();
            modalHeader.Dock = DockStyle.Top;
            modalHeader.Height = 50;
            modalHeader.BackColor = Color.FromArgb(18, 19, 28);

            Label lblTitle = new Label();
            lblTitle.Text = isEdit ? "✏️  시연 콘텐츠 수정" : "➕  새 시연 콘텐츠 추가";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
            lblTitle.Location = new Point(20, 15);
            lblTitle.AutoSize = true;
            modalHeader.Controls.Add(lblTitle);
            this.Controls.Add(modalHeader);

            int startY = 70;
            int gap = 62;

            // Name
            CreateLabel("콘텐츠 이름 *", 20, startY, textLabelColor, fontLabel);
            txtName = CreateTextBox(20, startY + 20, 260, inputBgColor, inputForeColor, fontInput);

            // Icon (Emoji or Image Path)
            CreateLabel("아이콘 (이모지 / 이미지 경로)", 295, startY, textLabelColor, fontLabel);
            txtIcon = CreateTextBox(295, startY + 20, 120, inputBgColor, inputForeColor, fontInput);
            txtIcon.Text = "🚀";

            Button btnBrowseIcon = new Button();
            btnBrowseIcon.Text = "🖼️";
            btnBrowseIcon.Font = new Font("Segoe UI Emoji", 8.5f, FontStyle.Bold);
            btnBrowseIcon.BackColor = Color.FromArgb(35, 37, 54);
            btnBrowseIcon.ForeColor = Color.White;
            btnBrowseIcon.FlatStyle = FlatStyle.Flat;
            btnBrowseIcon.FlatAppearance.BorderSize = 0;
            btnBrowseIcon.Location = new Point(425, startY + 20);
            btnBrowseIcon.Size = new Size(40, 26);
            btnBrowseIcon.Cursor = Cursors.Hand;
            btnBrowseIcon.Click += BtnBrowseIcon_Click;
            this.Controls.Add(btnBrowseIcon);

            // Icon Preview
            CreateLabel("미리보기", 475, startY - 2, textLabelColor, new Font("Malgun Gothic", 7.5f, FontStyle.Bold));
            pnlIconPreview = new Panel();
            pnlIconPreview.Location = new Point(475, startY + 13);
            pnlIconPreview.Size = new Size(40, 40);
            pnlIconPreview.BackColor = Color.FromArgb(15, 16, 25);
            
            // Round corners for premium preview panel
            pnlIconPreview.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int radius = 8;
                    path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
                    path.AddArc(pnlIconPreview.Width - radius * 2 - 1, 0, radius * 2, radius * 2, 270, 90);
                    path.AddArc(pnlIconPreview.Width - radius * 2 - 1, pnlIconPreview.Height - radius * 2 - 1, radius * 2, radius * 2, 0, 90);
                    path.AddArc(0, pnlIconPreview.Height - radius * 2 - 1, radius * 2, radius * 2, 90, 90);
                    path.CloseAllFigures();
                    pnlIconPreview.Region = new Region(path);
                    
                    using (Pen pen = new Pen(Color.FromArgb(45, 47, 64), 1))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            };

            lblEmojiPreview = new Label();
            lblEmojiPreview.Dock = DockStyle.Fill;
            lblEmojiPreview.TextAlign = ContentAlignment.MiddleCenter;
            lblEmojiPreview.Font = new Font("Segoe UI Emoji", 14f);
            lblEmojiPreview.ForeColor = Color.White;

            pbImagePreview = new PictureBox();
            pbImagePreview.Dock = DockStyle.Fill;
            pbImagePreview.SizeMode = PictureBoxSizeMode.Zoom;
            pbImagePreview.Visible = false;

            pnlIconPreview.Controls.Add(lblEmojiPreview);
            pnlIconPreview.Controls.Add(pbImagePreview);
            this.Controls.Add(pnlIconPreview);

            // Description
            CreateLabel("콘텐츠 설명", 20, startY + gap, textLabelColor, fontLabel);
            txtDesc = CreateTextBox(20, startY + gap + 20, 510, inputBgColor, inputForeColor, fontInput);
            txtDesc.Multiline = true;
            txtDesc.Height = 60;

            // Path (.exe)
            CreateLabel("실행 파일 절대 경로 (.exe) *", 20, startY + gap * 2 + 25, textLabelColor, fontLabel);
            txtPath = CreateTextBox(20, startY + gap * 2 + 45, 410, inputBgColor, inputForeColor, fontInput);
            
            btnBrowse = new Button();
            btnBrowse.Text = "📂  찾기";
            btnBrowse.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
            btnBrowse.BackColor = Color.FromArgb(35, 37, 54);
            btnBrowse.ForeColor = Color.White;
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Location = new Point(440, startY + gap * 2 + 45);
            btnBrowse.Size = new Size(90, 26);
            btnBrowse.Cursor = Cursors.Hand;
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);

            // Startup Module (.exe) & Delay (Seconds) with 2-Button Toggle UI
            CreateLabel("선행 구동 모듈 사용 여부", 20, 300, textLabelColor, fontLabel);

            btnModuleOn = new Button();
            btnModuleOn.Text = "ON";
            btnModuleOn.Font = new Font("Malgun Gothic", 8f, FontStyle.Bold);
            btnModuleOn.FlatStyle = FlatStyle.Flat;
            btnModuleOn.FlatAppearance.BorderSize = 0;
            btnModuleOn.Size = new Size(50, 22);
            btnModuleOn.Location = new Point(220, 296);
            btnModuleOn.Cursor = Cursors.Hand;
            btnModuleOn.Click += (s, e) => ToggleModuleControls(true);
            this.Controls.Add(btnModuleOn);

            btnModuleOff = new Button();
            btnModuleOff.Text = "OFF";
            btnModuleOff.Font = new Font("Malgun Gothic", 8f, FontStyle.Bold);
            btnModuleOff.FlatStyle = FlatStyle.Flat;
            btnModuleOff.FlatAppearance.BorderSize = 0;
            btnModuleOff.Size = new Size(50, 22);
            btnModuleOff.Location = new Point(275, 296);
            btnModuleOff.Cursor = Cursors.Hand;
            btnModuleOff.Click += (s, e) => ToggleModuleControls(false);
            this.Controls.Add(btnModuleOff);

            txtModulePath = CreateTextBox(20, 320, 300, inputBgColor, inputForeColor, fontInput);

            btnBrowseModule = new Button();
            btnBrowseModule.Text = "📂  찾기";
            btnBrowseModule.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
            btnBrowseModule.BackColor = Color.FromArgb(35, 37, 54);
            btnBrowseModule.ForeColor = Color.White;
            btnBrowseModule.FlatStyle = FlatStyle.Flat;
            btnBrowseModule.FlatAppearance.BorderSize = 0;
            btnBrowseModule.Location = new Point(330, 320);
            btnBrowseModule.Size = new Size(60, 26);
            btnBrowseModule.Cursor = Cursors.Hand;
            btnBrowseModule.Click += BtnBrowseModule_Click;
            this.Controls.Add(btnBrowseModule);
            CreateLabel("대기 시간 (초)", 400, 300, textLabelColor, fontLabel);
            txtModuleDelay = CreateTextBox(400, 320, 130, inputBgColor, inputForeColor, fontInput);
            txtModuleDelay.Text = "10";
            // Tiny explanations
            CreateLabel("(센서 구동기, 트래커 제어 등 메인 콘텐츠 기동 전에 필수 구동할 파일)", 20, 350, Color.FromArgb(120, 120, 140), new Font("Malgun Gothic", 7.5f));
            CreateLabel("(모듈 켜진 후 대기할 시간)", 400, 350, Color.FromArgb(120, 120, 140), new Font("Malgun Gothic", 7.5f));

            // Action buttons
            btnSave = new Button();
            btnSave.Text = "저장하기";
            btnSave.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            btnSave.BackColor = Color.FromArgb(123, 97, 255);
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Size = new Size(100, 36);
            btnSave.Location = new Point(320, this.Height - 55);
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button();
            btnCancel.Text = "취소";
            btnCancel.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            btnCancel.BackColor = Color.Transparent;
            btnCancel.ForeColor = Color.FromArgb(156, 163, 175);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 70);
            btnCancel.Size = new Size(90, 36);
            btnCancel.Location = new Point(440, this.Height - 55);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            // Focus Holding Toggle UI [NEW]
            CreateLabel("포커스 유지 여부", 20, 385, textLabelColor, fontLabel);

            btnKeepFocusOn = new Button();
            btnKeepFocusOn.Text = "ON";
            btnKeepFocusOn.Font = new Font("Malgun Gothic", 8f, FontStyle.Bold);
            btnKeepFocusOn.FlatStyle = FlatStyle.Flat;
            btnKeepFocusOn.FlatAppearance.BorderSize = 0;
            btnKeepFocusOn.Size = new Size(50, 22);
            btnKeepFocusOn.Location = new Point(180, 381);
            btnKeepFocusOn.Cursor = Cursors.Hand;
            btnKeepFocusOn.Click += (s, e) => ToggleKeepFocus(true);
            this.Controls.Add(btnKeepFocusOn);

            btnKeepFocusOff = new Button();
            btnKeepFocusOff.Text = "OFF";
            btnKeepFocusOff.Font = new Font("Malgun Gothic", 8f, FontStyle.Bold);
            btnKeepFocusOff.FlatStyle = FlatStyle.Flat;
            btnKeepFocusOff.FlatAppearance.BorderSize = 0;
            btnKeepFocusOff.Size = new Size(50, 22);
            btnKeepFocusOff.Location = new Point(235, 381);
            btnKeepFocusOff.Cursor = Cursors.Hand;
            btnKeepFocusOff.Click += (s, e) => ToggleKeepFocus(false);
            this.Controls.Add(btnKeepFocusOff);

            CreateLabel("(단일 빌드는 ON, 래퍼/컨테이너 빌드는 OFF 권장)", 295, 385, Color.FromArgb(120, 120, 140), new Font("Malgun Gothic", 7.5f));

            // Modal border line
            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(50, 50, 70), 1.5f))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };


        }

        private void ToggleModuleControls(bool enabled)
        {
            this.isModuleEnabled = enabled;
            txtModulePath.Enabled = enabled;
            btnBrowseModule.Enabled = enabled;
            txtModuleDelay.Enabled = enabled;

            if (enabled)
            {
                btnModuleOn.BackColor = Color.FromArgb(123, 97, 255);
                btnModuleOn.ForeColor = Color.White;
                btnModuleOff.BackColor = Color.FromArgb(35, 37, 54);
                btnModuleOff.ForeColor = Color.FromArgb(156, 163, 175);
                
                txtModulePath.BackColor = Color.FromArgb(15, 16, 25);
                txtModuleDelay.BackColor = Color.FromArgb(15, 16, 25);
            }
            else
            {
                btnModuleOn.BackColor = Color.FromArgb(35, 37, 54);
                btnModuleOn.ForeColor = Color.FromArgb(156, 163, 175);
                btnModuleOff.BackColor = Color.FromArgb(80, 80, 100);
                btnModuleOff.ForeColor = Color.White;
                
                txtModulePath.BackColor = Color.FromArgb(30, 30, 40);
                txtModuleDelay.BackColor = Color.FromArgb(30, 30, 40);
            }
        }

        private void ToggleKeepFocus(bool enabled)
        {
            this.isKeepFocusEnabled = enabled;
            if (enabled)
            {
                btnKeepFocusOn.BackColor = Color.FromArgb(123, 97, 255);
                btnKeepFocusOn.ForeColor = Color.White;
                btnKeepFocusOff.BackColor = Color.FromArgb(35, 37, 54);
                btnKeepFocusOff.ForeColor = Color.FromArgb(156, 163, 175);
            }
            else
            {
                btnKeepFocusOn.BackColor = Color.FromArgb(35, 37, 54);
                btnKeepFocusOn.ForeColor = Color.FromArgb(156, 163, 175);
                btnKeepFocusOff.BackColor = Color.FromArgb(80, 80, 100);
                btnKeepFocusOff.ForeColor = Color.White;
            }
        }

        private void UpdateIconPreview()
        {
            if (pnlIconPreview == null || lblEmojiPreview == null || pbImagePreview == null) return;

            string iconText = txtIcon.Text.Trim();
            
            if (string.IsNullOrEmpty(iconText))
            {
                lblEmojiPreview.Text = "❓";
                lblEmojiPreview.Visible = true;
                pbImagePreview.Visible = false;
                return;
            }

            bool isFile = false;
            try
            {
                if (System.IO.File.Exists(iconText))
                {
                    isFile = true;
                }
            }
            catch { }

            if (isFile)
            {
                try
                {
                    if (pbImagePreview.Image != null)
                    {
                        pbImagePreview.Image.Dispose();
                        pbImagePreview.Image = null;
                    }
                    pbImagePreview.Image = Image.FromFile(iconText);
                    pbImagePreview.Visible = true;
                    lblEmojiPreview.Visible = false;
                }
                catch
                {
                    lblEmojiPreview.Text = iconText;
                    lblEmojiPreview.Visible = true;
                    pbImagePreview.Visible = false;
                }
            }
            else
            {
                lblEmojiPreview.Text = iconText;
                lblEmojiPreview.Visible = true;
                pbImagePreview.Visible = false;
            }
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

        private TextBox CreateTextBox(int x, int y, int w, Color bg, Color fg, Font font)
        {
            TextBox txt = new TextBox();
            txt.Location = new Point(x, y);
            txt.Width = w;
            txt.BackColor = bg;
            txt.ForeColor = fg;
            txt.Font = font;
            txt.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(txt);
            return txt;
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
                ofd.Title = "시연할 실행 파일을 선택해 주세요";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = ofd.FileName;
                    if (string.IsNullOrEmpty(txtName.Text))
                    {
                        txtName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            }
        }

        private void BtnBrowseIcon_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|모든 파일 (*.*)|*.*";
                ofd.Title = "아이콘으로 사용할 이미지 파일을 선택해 주세요";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtIcon.Text = ofd.FileName;
                }
            }
        }

        private void BtnBrowseModule_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
                ofd.Title = "선행 구동할 모듈 실행 파일을 선택해 주세요";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtModulePath.Text = ofd.FileName;
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string path = txtPath.Text.Trim();
            string modulePath = isModuleEnabled ? txtModulePath.Text.Trim() : "";
            string delayStr = txtModuleDelay.Text.Trim();
            int moduleDelay = 10;
            
            if (isModuleEnabled && !string.IsNullOrEmpty(delayStr))
            {
                if (!int.TryParse(delayStr, out moduleDelay) || moduleDelay < 10)
                {
                    MessageBox.Show("선행 구동 대기 시간은 최소 10초 이상이어야 합니다. (10초 미만 설정 불가)", "대기 시간 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtModuleDelay.Focus();
                    return;
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("콘텐츠 이름을 입력해 주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("실행 파일 경로를 선택해 주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPath.Focus();
                return;
            }

            ResultItem = new ContentItem
            {
                name = name,
                icon = txtIcon.Text.Trim(),
                description = txtDesc.Text.Trim(),
                path = path,
                modulePath = modulePath,
                moduleDelay = moduleDelay,
                keepFocus = isKeepFocusEnabled
            };

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

    // --- GLOBAL SCHEDULER FORM CLASS ---
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
        private Button btnSave;
        private Button btnCancel;
        private List<ContentItem> items;

        public SchedulerForm(List<ContentItem> contentList, bool enabled, string autoStartId, string shutdownTime, bool pcShutdown)
        {
            this.items = contentList;
            this.SchedulerEnabled = enabled;
            this.AutoStartContentId = autoStartId;
            this.AutoShutdownTime = shutdownTime;
            this.IsPcShutdown = pcShutdown;

            InitializeComponent();

            UpdateSchedulerToggleUI();
            chkPc.Checked = pcShutdown;

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
            
            // Set hour and minute dropdown values
            string hour = "18";
            string minute = "00";
            if (!string.IsNullOrEmpty(shutdownTime))
            {
                string[] parts = shutdownTime.Split(':');
                if (parts.Length == 2)
                {
                    hour = parts[0];
                    minute = parts[1];
                }
            }
            cbHour.SelectedItem = hour;
            cbMinute.SelectedItem = minute;

            // Toggle controls based on enabled state
            ToggleControls(enabled);

            btnScheduleOn.Click += (s, e) => {
                this.SchedulerEnabled = true;
                UpdateSchedulerToggleUI();
                ToggleControls(true);
            };
            btnScheduleOff.Click += (s, e) => {
                this.SchedulerEnabled = false;
                UpdateSchedulerToggleUI();
                ToggleControls(false);
            };
        }

        private void UpdateSchedulerToggleUI()
        {
            if (this.SchedulerEnabled)
            {
                btnScheduleOn.BackColor = Color.FromArgb(123, 97, 255);
                btnScheduleOn.ForeColor = Color.White;
                btnScheduleOff.BackColor = Color.FromArgb(35, 37, 54);
                btnScheduleOff.ForeColor = Color.FromArgb(156, 163, 175);
            }
            else
            {
                btnScheduleOn.BackColor = Color.FromArgb(35, 37, 54);
                btnScheduleOn.ForeColor = Color.FromArgb(156, 163, 175);
                btnScheduleOff.BackColor = Color.FromArgb(80, 80, 100);
                btnScheduleOff.ForeColor = Color.White;
            }
        }

        private void ToggleControls(bool enabled)
        {
            cbContent.Enabled = enabled;
            cbHour.Enabled = enabled;
            cbMinute.Enabled = enabled;
            
            // Enabled = false로 하면 OS 기본 Disabled 렌더링에 의해 어두운 배경에 글자/체크가 묻혀 보이지 않으므로,
            // 활성화 상태를 유지하되 AutoCheck와 색상 조절을 통해 비활성화 상태를 시뮬레이션합니다.
            chkPc.AutoCheck = enabled; 
            if (enabled)
            {
                chkPc.ForeColor = Color.White;
                chkPc.Cursor = Cursors.Hand;
            }
            else
            {
                chkPc.ForeColor = Color.FromArgb(70, 75, 95); // 어두운 회색으로 비활성화 표시
                chkPc.Cursor = Cursors.Default;
                chkPc.Checked = false; // 스케줄러가 꺼지면 PC 종료 체크도 안전하게 해제
            }
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(420, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 25, 38);

            Font fontLabel = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Font fontInput = new Font("Malgun Gothic", 9.5f);
            Color textLabelColor = Color.FromArgb(156, 163, 175);
            Color inputBgColor = Color.FromArgb(15, 16, 25);
            Color inputForeColor = Color.White;

            // Custom Title
            Panel modalHeader = new Panel();
            modalHeader.Dock = DockStyle.Top;
            modalHeader.Height = 50;
            modalHeader.BackColor = Color.FromArgb(18, 19, 28);

            Label lblTitle = new Label();
            lblTitle.Text = "⏰  글로벌 자동 운용 설정";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
            lblTitle.Location = new Point(20, 15);
            lblTitle.AutoSize = true;
            modalHeader.Controls.Add(lblTitle);
            this.Controls.Add(modalHeader);

            // Scheduler toggle panel label and buttons
            CreateLabel("자동 운용 스케줄러 활성화", 25, 75, textLabelColor, fontLabel);

            btnScheduleOn = new Button();
            btnScheduleOn.Text = "ON";
            btnScheduleOn.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
            btnScheduleOn.FlatStyle = FlatStyle.Flat;
            btnScheduleOn.FlatAppearance.BorderSize = 0;
            btnScheduleOn.Size = new Size(60, 24);
            btnScheduleOn.Location = new Point(205, 72);
            btnScheduleOn.Cursor = Cursors.Hand;
            this.Controls.Add(btnScheduleOn);

            btnScheduleOff = new Button();
            btnScheduleOff.Text = "OFF";
            btnScheduleOff.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
            btnScheduleOff.FlatStyle = FlatStyle.Flat;
            btnScheduleOff.FlatAppearance.BorderSize = 0;
            btnScheduleOff.Size = new Size(60, 24);
            btnScheduleOff.Location = new Point(270, 72);
            btnScheduleOff.Cursor = Cursors.Hand;
            this.Controls.Add(btnScheduleOff);

            // Auto-start content selection
            CreateLabel("기동 시 자동 실행할 콘텐츠", 25, 120, textLabelColor, fontLabel);
            cbContent = new ComboBox();
            cbContent.Location = new Point(25, 140);
            cbContent.Width = 370;
            cbContent.BackColor = inputBgColor;
            cbContent.ForeColor = inputForeColor;
            cbContent.Font = fontInput;
            cbContent.DropDownStyle = ComboBoxStyle.DropDownList;
            cbContent.FlatStyle = FlatStyle.Flat;
            this.Controls.Add(cbContent);

            // Auto-shutdown hour/minute dropdowns
            CreateLabel("자동 종료 시간", 25, 190, textLabelColor, fontLabel);
            
            cbHour = new ComboBox();
            cbHour.Location = new Point(25, 210);
            cbHour.Width = 60;
            cbHour.BackColor = inputBgColor;
            cbHour.ForeColor = inputForeColor;
            cbHour.Font = fontInput;
            cbHour.DropDownStyle = ComboBoxStyle.DropDownList;
            cbHour.FlatStyle = FlatStyle.Flat;
            for (int i = 0; i < 24; i++) cbHour.Items.Add(i.ToString("D2"));
            this.Controls.Add(cbHour);

            CreateLabel("시", 90, 212, textLabelColor, fontLabel);

            cbMinute = new ComboBox();
            cbMinute.Location = new Point(115, 210);
            cbMinute.Width = 60;
            cbMinute.BackColor = inputBgColor;
            cbMinute.ForeColor = inputForeColor;
            cbMinute.Font = fontInput;
            cbMinute.DropDownStyle = ComboBoxStyle.DropDownList;
            cbMinute.FlatStyle = FlatStyle.Flat;
            for (int i = 0; i < 60; i++) cbMinute.Items.Add(i.ToString("D2"));
            this.Controls.Add(cbMinute);

            CreateLabel("분", 180, 212, textLabelColor, fontLabel);

            // PC Shutdown checkbox
            chkPc = new CheckBox();
            chkPc.Text = "종료 시 PC도 끄기";
            chkPc.Font = fontInput;
            chkPc.ForeColor = Color.White;
            chkPc.Location = new Point(240, 210);
            chkPc.Size = new Size(150, 24);
            chkPc.Cursor = Cursors.Hand;
            this.Controls.Add(chkPc);

            // Action buttons
            btnSave = new Button();
            btnSave.Text = "저장하기";
            btnSave.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            btnSave.BackColor = Color.FromArgb(123, 97, 255);
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
            btnCancel.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            btnCancel.BackColor = Color.Transparent;
            btnCancel.ForeColor = Color.FromArgb(156, 163, 175);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 70);
            btnCancel.Size = new Size(90, 36);
            btnCancel.Location = new Point(305, 300);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            // Border
            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(50, 50, 70), 1.5f))
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

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (this.SchedulerEnabled)
            {
                ComboBoxItem selectedItem = cbContent.SelectedItem as ComboBoxItem;
                this.AutoStartContentId = selectedItem != null ? selectedItem.Value : "";
                
                string hourStr = cbHour.SelectedItem != null ? cbHour.SelectedItem.ToString() : "18";
                string minStr = cbMinute.SelectedItem != null ? cbMinute.SelectedItem.ToString() : "00";
                this.AutoShutdownTime = hourStr + ":" + minStr;
                
                this.IsPcShutdown = chkPc.Checked;
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

    // Helper ComboBox item with value
    public class ComboBoxItem
    {
        public string Text { get; set; }
        public string Value { get; set; }
        public override string ToString()
        {
            return Text;
        }
    }
}
