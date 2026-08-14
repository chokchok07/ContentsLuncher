using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using System.Text;

[assembly: AssemblyTitle("ShowroomLauncher")]
[assembly: AssemblyDescription("Showroom Launcher")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Uangel")]
[assembly: AssemblyProduct("ShowroomLauncher")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("83b53030-43d8-4686-a5ff-6062595ae1e4")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ShowroomLauncher
{
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
                
                // 윈도우 시작프로그램 경로 자가복구 호출
                StartupManager.CheckAndUpdatePath();

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
}
