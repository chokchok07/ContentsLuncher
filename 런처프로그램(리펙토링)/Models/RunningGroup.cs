using System.Diagnostics;

namespace ShowroomLauncher
{
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
}
