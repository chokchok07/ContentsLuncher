using System;

namespace ShowroomPowerController
{
    public class DeviceItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string IpAddress { get; set; }
        public string MacAddress { get; set; }
        public int Port { get; set; }
        public string Space { get; set; }
        public string AssociatedDeviceId { get; set; }
        public int BootOrder { get; set; }
        public int BootDelaySeconds { get; set; }
        public string Description { get; set; }

        public string PowerOnSequenceMode { get; set; } // PC_FIRST 또는 PROJ_FIRST
        public string RuntimeStatus { get; set; } // OFFLINE, BOOTING, ONLINE, COOLING, FREEZE
        public string ContentState { get; set; } // 구동대기중, 콘텐츠구동중
        public int RemainingSeconds { get; set; }
        public DateTime LastActiveTime { get; set; }
        public DateTime LastShutdownTime { get; set; }

        public DeviceItem()
        {
            PowerOnSequenceMode = "PC_FIRST";
            RuntimeStatus = "OFFLINE";
            ContentState = "구동대기중";
            RemainingSeconds = 0;
            LastActiveTime = DateTime.Now;
            LastShutdownTime = DateTime.Now.AddDays(-1);
        }
    }

    // 2. 자동 예약 스케줄 세부 설정 모델 클래스
}
