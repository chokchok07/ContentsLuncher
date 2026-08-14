using System;
using System.Collections.Generic;

namespace ShowroomPowerController
{
    public class ScheduleSettings
    {
        public string WeekdayStart { get; set; }
        public string WeekdayEnd { get; set; }
        public string SaturdayStart { get; set; }
        public string SaturdayEnd { get; set; }
        public List<string> IgnoreDays { get; set; }

        public ScheduleSettings()
        {
            WeekdayStart = "08:50";
            WeekdayEnd = "18:10";
            SaturdayStart = "09:50";
            SaturdayEnd = "15:10";
            IgnoreDays = new List<string> { "월요일", "일요일" };
        }
    }

}
