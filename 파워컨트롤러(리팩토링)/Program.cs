using System;
using System.Windows.Forms;

namespace ShowroomPowerController
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            StartupManager.CheckAndUpdatePath();
            Application.Run(new PowerControllerForm());
        }
    }
}