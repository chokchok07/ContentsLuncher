using System.Collections.Generic;

namespace ShowroomPowerController
{
    public class DeviceConfig
    {
        public List<string> Spaces { get; set; }
        public List<DeviceItem> Devices { get; set; }
        public ScheduleSettings Schedules { get; set; }
    }

    // 3. 그래픽 플리커 방지용 이중 버퍼 패널
}
