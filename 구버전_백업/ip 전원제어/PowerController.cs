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
    // 1. 장치 데이터 모델 클래스
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
        public int RemainingSeconds { get; set; }
        public DateTime LastActiveTime { get; set; }
        public DateTime LastShutdownTime { get; set; }

        public DeviceItem()
        {
            PowerOnSequenceMode = "PC_FIRST";
            RuntimeStatus = "OFFLINE";
            RemainingSeconds = 0;
            LastActiveTime = DateTime.Now;
            LastShutdownTime = DateTime.Now.AddDays(-1);
        }
    }

    // 2. 자동 예약 스케줄 세부 설정 모델 클래스
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

    public class DeviceConfig
    {
        public List<string> Spaces { get; set; }
        public List<DeviceItem> Devices { get; set; }
        public ScheduleSettings Schedules { get; set; }
    }

    // 3. 그래픽 플리커 방지용 이중 버퍼 패널
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }
    }

    // 4. 규격이 가로 225 x 세로 175로 완전 고정/단일화되고, 하단에 연동 자식 기기들의 상태등을 내포하는 정형 카드 컨트롤
    public class DeviceCardControl : UserControl
    {
        private DeviceItem device;                  
        private List<DeviceItem> linkedDevices;     
        private PowerControllerForm mainForm;

        private System.Windows.Forms.Timer animTimer;
        private int pulseAlpha = 150;
        private bool pulseIncreasing = true;
        private bool blinkState = false;
        private int animTickCount = 0;

        private Button btnOn;                       
        private Button btnOff;                      
        private Button btnEdit;                     

        public DeviceItem Device { get { return device; } }
        public List<DeviceItem> LinkedDevices { get { return linkedDevices; } }

        public DeviceCardControl(DeviceItem dev, List<DeviceItem> linkedDevs, PowerControllerForm form)
        {
            this.device = dev;
            this.linkedDevices = linkedDevs ?? new List<DeviceItem>();
            this.mainForm = form;
            this.DoubleBuffered = true;
            this.BackColor = Color.Transparent;

            this.Size = new Size(225, 195);

            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 100;
            animTimer.Tick += AnimTimer_Tick;
            animTimer.Start();

            InitializeCardControls();
            UpdateButtonStates();
        }

        private void InitializeCardControls()
        {
            btnOn = new Button();
            btnOn.Text = "ON";
            btnOn.Size = new Size(50, 24);
            btnOn.Location = new Point(105, 160);
            btnOn.FlatStyle = FlatStyle.Flat;
            btnOn.FlatAppearance.BorderSize = 0;
            btnOn.Font = new Font("Malgun Gothic", 8f, FontStyle.Bold);
            btnOn.Cursor = Cursors.Hand;
            btnOn.Click += BtnOn_Click;

            btnOff = new Button();
            btnOff.Text = "OFF";
            btnOff.Size = new Size(50, 24);
            btnOff.Location = new Point(160, 160);
            btnOff.FlatStyle = FlatStyle.Flat;
            btnOff.FlatAppearance.BorderSize = 0;
            btnOff.Font = new Font("Malgun Gothic", 8f, FontStyle.Bold);
            btnOff.Cursor = Cursors.Hand;
            btnOff.Click += BtnOff_Click;

            this.Controls.Add(btnOn);
            this.Controls.Add(btnOff);

            btnEdit = new Button();
            btnEdit.Text = "⚙️";
            btnEdit.Size = new Size(24, 24);
            btnEdit.Location = new Point(188, 12);
            btnEdit.BackColor = Color.Transparent;
            btnEdit.ForeColor = Color.FromArgb(156, 163, 175);
            btnEdit.FlatStyle = FlatStyle.Flat;
            btnEdit.FlatAppearance.BorderSize = 0;
            btnEdit.Cursor = Cursors.Hand;
            btnEdit.Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            btnEdit.Click += BtnEdit_Click;

            btnEdit.MouseEnter += (s, ev) => btnEdit.ForeColor = Color.FromArgb(123, 97, 255);
            btnEdit.MouseLeave += (s, ev) => btnEdit.ForeColor = Color.FromArgb(156, 163, 175);

            this.Controls.Add(btnEdit);
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            animTickCount++;

            bool anyOnline = (device.RuntimeStatus == "ONLINE");
            foreach (var d in linkedDevices)
            {
                if (d.RuntimeStatus == "ONLINE") anyOnline = true;
            }

            if (anyOnline)
            {
                if (pulseIncreasing)
                {
                    pulseAlpha += 12;
                    if (pulseAlpha >= 255) { pulseAlpha = 255; pulseIncreasing = false; }
                }
                else
                {
                    pulseAlpha -= 12;
                    if (pulseAlpha <= 80) { pulseAlpha = 80; pulseIncreasing = true; }
                }
            }

            bool anyFreeze = (device.RuntimeStatus == "FREEZE");
            foreach (var d in linkedDevices)
            {
                if (d.RuntimeStatus == "FREEZE") anyFreeze = true;
            }

            if (anyFreeze)
            {
                if (animTickCount % 5 == 0)
                {
                    blinkState = !blinkState;
                }
            }

            UpdateButtonStates();
            this.Invalidate();
        }

        private void UpdateButtonStates()
        {
            if (mainForm.IsRunningSimulation)
            {
                btnOn.Enabled = false;
                btnOff.Enabled = false;
                btnEdit.Enabled = false;
                return;
            }

            btnEdit.Enabled = true;

            bool cardIsBusy = (device.RuntimeStatus == "BOOTING" || device.RuntimeStatus == "COOLING");
            foreach (var sub in linkedDevices)
            {
                if (sub.RuntimeStatus == "BOOTING" || sub.RuntimeStatus == "COOLING") cardIsBusy = true;
            }

            if (cardIsBusy)
            {
                btnOn.Enabled = false;
                btnOn.BackColor = Color.FromArgb(24, 25, 38);
                btnOn.ForeColor = Color.FromArgb(75, 85, 99);

                btnOff.Enabled = false;
                btnOff.BackColor = Color.FromArgb(24, 25, 38);
                btnOff.ForeColor = Color.FromArgb(75, 85, 99);
            }
            else
            {
                UpdateButtonState(btnOn, btnOff, device.RuntimeStatus);
            }
        }

        private void UpdateButtonState(Button onBtn, Button offBtn, string status)
        {
            Color activeOn = Color.FromArgb(123, 97, 255); 
            Color activeOff = Color.FromArgb(239, 68, 68); 
            Color disabledBg = Color.FromArgb(24, 25, 38);   
            Color disabledText = Color.FromArgb(75, 85, 99); 

            if (status == "BOOTING" || status == "COOLING")
            {
                onBtn.Enabled = false;
                onBtn.BackColor = disabledBg;
                onBtn.ForeColor = disabledText;

                offBtn.Enabled = false;
                offBtn.BackColor = disabledBg;
                offBtn.ForeColor = disabledText;
            }
            else if (status == "ONLINE")
            {
                onBtn.Enabled = false;
                onBtn.BackColor = disabledBg;
                onBtn.ForeColor = disabledText;

                offBtn.Enabled = true;
                offBtn.BackColor = activeOff;
                offBtn.ForeColor = Color.White;
            }
            else if (status == "OFFLINE")
            {
                onBtn.Enabled = true;
                onBtn.BackColor = activeOn;
                onBtn.ForeColor = Color.White;

                offBtn.Enabled = false;
                offBtn.BackColor = disabledBg;
                offBtn.ForeColor = disabledText;
            }
            else 
            {
                onBtn.Enabled = true;
                onBtn.BackColor = activeOn;
                onBtn.ForeColor = Color.White;

                offBtn.Enabled = true;
                offBtn.BackColor = activeOff;
                offBtn.ForeColor = Color.White;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
            int roundRadius = 16;
            GraphicsPath path = GetRoundedRectPath(rect, roundRadius);

            Color cardBg = Color.FromArgb(28, 29, 43);
            Color borderColor = Color.FromArgb(37, 39, 54);
            float borderWidth = 1.5f;

            bool isOnline = (device.RuntimeStatus == "ONLINE");
            bool isBooting = (device.RuntimeStatus == "BOOTING");
            bool isCooling = (device.RuntimeStatus == "COOLING");
            bool isFreeze = (device.RuntimeStatus == "FREEZE");
            bool isOffline = (device.RuntimeStatus == "OFFLINE");

            foreach (var d in linkedDevices)
            {
                if (d.RuntimeStatus == "ONLINE") isOnline = true;
                if (d.RuntimeStatus == "BOOTING") isBooting = true;
                if (d.RuntimeStatus == "COOLING") isCooling = true;
                if (d.RuntimeStatus == "FREEZE") isFreeze = true;
                if (d.RuntimeStatus != "OFFLINE") isOffline = false;
            }

            if (isOnline)
            {
                borderColor = Color.FromArgb(pulseAlpha, 123, 97, 255);
                borderWidth = 2.0f;
            }
            else if (isOffline)
            {
                cardBg = Color.FromArgb(20, 21, 31);
                borderColor = Color.FromArgb(30, 32, 45);
            }
            else if (isFreeze)
            {
                borderColor = blinkState ? Color.FromArgb(239, 68, 68) : Color.FromArgb(60, 20, 25);
                borderWidth = 2.5f;
            }
            else if (isBooting)
            {
                borderColor = Color.FromArgb(245, 158, 11);
                borderWidth = 2.0f;
            }
            else if (isCooling)
            {
                borderColor = Color.FromArgb(200, 100, 30);
                borderWidth = 2.0f;
            }

            using (SolidBrush bgBrush = new SolidBrush(cardBg))
            {
                g.FillPath(bgBrush, path);
            }

            using (Pen borderPen = new Pen(borderColor, borderWidth))
            {
                g.DrawPath(borderPen, path);
            }

            DrawDeviceSlot(g, device, 0, borderColor);

            if (linkedDevices.Count > 0)
            {
                using (Pen sepPen = new Pen(Color.FromArgb(50, 52, 74), 1f))
                {
                    g.DrawLine(sepPen, 15, 146, 210, 146);
                }

                for (int i = 0; i < linkedDevices.Count; i++)
                {
                    if (i >= 2) break; 

                    var proj = linkedDevices[i];
                    int x = 15 + i * 100;
                    int y = 154;

                    using (Pen p = new Pen(Color.FromArgb(180, 185, 210), 1.2f))
                    {
                        g.DrawRectangle(p, x, y + 2, 9, 6);
                        g.DrawEllipse(p, x + 8, y + 3, 4, 4);
                    }

                    Color dotColor = Color.FromArgb(156, 163, 175);
                    string stateName = "OFF";

                    if (proj.RuntimeStatus == "ONLINE") 
                    { 
                        dotColor = Color.FromArgb(16, 185, 129); 
                        stateName = "ON"; 
                    }
                    else if (proj.RuntimeStatus == "BOOTING") 
                    { 
                        dotColor = Color.FromArgb(245, 158, 11); 
                        stateName = string.Format("ON({0}s)", proj.RemainingSeconds); 
                    }
                    else if (proj.RuntimeStatus == "COOLING") 
                    { 
                        dotColor = Color.FromArgb(200, 100, 30); 
                        stateName = string.Format("COOL({0}s)", proj.RemainingSeconds); 
                    }
                    else if (proj.RuntimeStatus == "FREEZE") 
                    { 
                        dotColor = Color.FromArgb(239, 68, 68); 
                        stateName = "ERR"; 
                    }

                    using (SolidBrush dotBrush = new SolidBrush(dotColor))
                    {
                        g.FillEllipse(dotBrush, x + 16, y + 4, 5, 5);
                    }

                    using (Font projFont = new Font("Malgun Gothic", 7.5f, FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
                    {
                        string shortName = proj.Name.Length > 3 ? proj.Name.Substring(0, 3) : proj.Name;
                        string dispText = string.Format("{0}:{1}", shortName, stateName);
                        g.DrawString(dispText, projFont, textBrush, new PointF(x + 25, y + 1));
                    }
                }
            }
        }

        private void DrawDeviceSlot(Graphics g, DeviceItem dev, int xOffset, Color currentBorderColor)
        {
            Rectangle iconRect = new Rectangle(xOffset + 15, 15, 52, 52);
            GraphicsPath iconPath = GetRoundedRectPath(iconRect, 8);
            using (SolidBrush iconBgBrush = new SolidBrush(Color.FromArgb(35, 37, 54)))
            {
                g.FillPath(iconBgBrush, iconPath);
            }

            int cx = xOffset + 15 + 26; 
            int cy = 15 + 26;           

            if (dev.RuntimeStatus == "BOOTING" || dev.RuntimeStatus == "COOLING")
            {
                string countStr = dev.RemainingSeconds.ToString();
                using (Font numFont = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (SolidBrush numBrush = new SolidBrush(Color.FromArgb(245, 158, 11))) 
                {
                    StringFormat numSf = new StringFormat();
                    numSf.Alignment = StringAlignment.Center;
                    numSf.LineAlignment = StringAlignment.Center;
                    
                    RectangleF textRect = new RectangleF(xOffset + 15, 15, 52, 52);
                    g.DrawString(countStr, numFont, numBrush, textRect, numSf);
                }
            }
            else
            {
                if (dev.Type == "PC")
                {
                    using (Pen p = new Pen(Color.White, 2f))
                    {
                        g.DrawRectangle(p, cx - 16, cy - 13, 32, 20);
                        g.DrawLine(p, cx, cy + 7, cx, cy + 12);
                        g.DrawLine(p, cx - 8, cy + 12, cx + 8, cy + 12);
                    }
                }
                else 
                {
                    using (Pen p = new Pen(Color.White, 2f))
                    {
                        g.DrawRectangle(p, cx - 17, cy - 9, 25, 17);
                        g.DrawEllipse(p, cx + 8, cy - 6, 10, 10);
                        g.DrawLine(p, cx + 8, cy - 1, cx + 8, cy + 4);
                        g.DrawLine(p, cx - 10, cy + 8, cx - 12, cy + 12);
                        g.DrawLine(p, cx + 2, cy + 8, cx + 4, cy + 12);
                    }
                }
            }

            Font nameFont = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            Font infoFont = new Font("Malgun Gothic", 8f, FontStyle.Regular);
            Brush textBrush = dev.RuntimeStatus == "OFFLINE" ? Brushes.Gray : Brushes.White;

            RectangleF nameRect = new RectangleF(xOffset + 78, 20, 110, 18);
            StringFormat sfName = new StringFormat() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(dev.Name, nameFont, textBrush, nameRect, sfName);

            g.DrawString("IP: " + dev.IpAddress, infoFont, Brushes.Gray, new PointF(xOffset + 78, 43));
            g.DrawString(string.Format("[{0}]", dev.Type), infoFont, new SolidBrush(currentBorderColor), new PointF(xOffset + 78, 59));
            g.DrawString(string.Format("순위: {0}순위 ({1}초 대기)", dev.BootOrder, dev.BootDelaySeconds), infoFont, Brushes.LightGray, new PointF(xOffset + 78, 77));

            if (dev.RuntimeStatus == "BOOTING" || dev.RuntimeStatus == "COOLING")
            {
                Rectangle ringRect = new Rectangle(xOffset + 12, 12, 58, 58);
                int totalDuration = dev.RuntimeStatus == "BOOTING" ? 10 : 5;
                if (dev.BootDelaySeconds > 0 && dev.RuntimeStatus == "BOOTING")
                {
                    totalDuration = dev.BootDelaySeconds;
                }

                float percent = totalDuration > 0 ? (float)dev.RemainingSeconds / totalDuration : 0;
                float sweepAngle = percent * 360f;

                using (Pen baseRingPen = new Pen(Color.FromArgb(30, 255, 255, 255), 3))
                {
                    g.DrawEllipse(baseRingPen, ringRect);
                }

                Color progressColor = dev.RuntimeStatus == "BOOTING" ? Color.FromArgb(245, 158, 11) : Color.FromArgb(200, 100, 30);
                using (Pen progressRingPen = new Pen(progressColor, 3))
                {
                    progressRingPen.StartCap = LineCap.Round;
                    progressRingPen.EndCap = LineCap.Round;
                    g.DrawArc(progressRingPen, ringRect, -90, sweepAngle);
                }
            }

            string statusStr = "OFFLINE (대기 중)";
            Color statusColor = Color.FromArgb(156, 163, 175);

            if (dev.RuntimeStatus == "ONLINE")
            {
                statusStr = "ONLINE (실행 중)";
                statusColor = Color.FromArgb(16, 185, 129);
            }
            else if (dev.RuntimeStatus == "BOOTING")
            {
                statusStr = "BOOTING (부팅 중)";
                statusColor = Color.FromArgb(245, 158, 11);
            }
            else if (dev.RuntimeStatus == "COOLING")
            {
                statusStr = "COOLING (식히는 중)";
                statusColor = Color.FromArgb(200, 100, 30);
            }
            else if (dev.RuntimeStatus == "FREEZE")
            {
                statusStr = "FREEZE (신호 끊김)";
                statusColor = Color.FromArgb(239, 68, 68);
            }

            using (SolidBrush dotBrush = new SolidBrush(statusColor))
            {
                g.FillEllipse(dotBrush, xOffset + 18, 118, 6, 6);
            }

            using (Font fontStatus = new Font("Malgun Gothic", 8f, FontStyle.Bold))
            using (SolidBrush brushStatus = new SolidBrush(statusColor))
            {
                g.DrawString(statusStr, fontStatus, brushStatus, new PointF(xOffset + 28, 114));
            }
        }

        private async Task RunIndividualPowerOn(DeviceItem targetDev)
        {
            if (targetDev.RuntimeStatus == "ONLINE" || mainForm.IsRunningSimulation) return;

            mainForm.LogMessage(string.Format("====== [{0} 비동기 개별 기동 시작] ======", targetDev.Name));

            bool isProjFirst = (targetDev.Type == "PC" && targetDev.PowerOnSequenceMode == "PROJ_FIRST");

            if (isProjFirst && linkedDevices.Count > 0)
            {
                // [모드 A] 프로젝터 우선 기동 (프로젝터 예열 ➡️ PC 켬)
                mainForm.LogMessage("▶ [1단계: 프로젝터 예열] 자식 빔 프로젝터들의 기동 및 예열 카운트다운을 먼저 실행합니다.");
                
                int maxProjDelay = 0;
                foreach (var proj in linkedDevices)
                {
                    proj.RuntimeStatus = "BOOTING";
                    int dly = proj.BootDelaySeconds > 0 ? proj.BootDelaySeconds : 10;
                    proj.RemainingSeconds = dly;
                    
                    if (dly > maxProjDelay) maxProjDelay = dly;
                    mainForm.LogMessage(string.Format("   └🔗 {0} 예열 개시 ➡️ 설정 대기시간: {1}초", proj.Name, dly));

                    // 실제 네트워크 모드 활성화 시 PJLink ON 명령어 실제 송출
                    if (mainForm.IsRealNetworkControlMode)
                    {
                        mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] PJLink ON 패킷 실제 송출 -> IP: {0}:{1}", proj.IpAddress, proj.Port));
                        #pragma warning disable 4014
                        mainForm.SendPJLinkCommandAsync(proj.IpAddress, proj.Port, "%1POWR 1");
                        #pragma warning restore 4014
                    }
                }
                mainForm.UpdateVisualDashboard();

                for (int i = maxProjDelay; i > 0; i--)
                {
                    foreach (var proj in linkedDevices)
                    {
                        if (proj.RemainingSeconds > 0) proj.RemainingSeconds--;
                    }
                    mainForm.UpdateVisualDashboard();
                    await Task.Delay(1000);
                }

                foreach (var proj in linkedDevices)
                {
                    proj.RuntimeStatus = "ONLINE";
                    proj.RemainingSeconds = 0;
                    mainForm.LogMessage(string.Format("   └🔗 {0} 예열 완료 ➡️ 투사 상태 확인(ONLINE)", proj.Name));
                }
                mainForm.UpdateVisualDashboard();

                // 2단계: PC 기동
                mainForm.LogMessage(string.Format("▶ [2단계: PC 부팅] 부모 제어 장비 {0}의 기동 전원(WOL)을 송신합니다.", targetDev.Name));
                mainForm.AddWolRetryQueue(targetDev.Id);
                if (mainForm.IsRealNetworkControlMode)
                {
                    mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] WOL Magic Packet UDP 브로드캐스트 전송 -> MAC: {0}", targetDev.MacAddress));
                    mainForm.SendWOLMagicPacket(targetDev.MacAddress);
                }

                targetDev.RuntimeStatus = "BOOTING";
                int pcDelay = targetDev.BootDelaySeconds > 0 ? targetDev.BootDelaySeconds : 5;
                targetDev.RemainingSeconds = pcDelay;
                mainForm.UpdateVisualDashboard();

                for (int i = pcDelay; i >= 0; i--)
                {
                    targetDev.RemainingSeconds = i;
                    mainForm.UpdateVisualDashboard();
                    if (i > 0) await Task.Delay(1000);
                }

                targetDev.RuntimeStatus = "ONLINE";
                targetDev.RemainingSeconds = 0;
            }
            else
            {
                // [모드 B] PC 우선 기동 (PC ➡️ 프로젝터) 또는 단독 기기 기동
                if (targetDev.Type == "PC")
                {
                    mainForm.LogMessage(string.Format("▶ [1단계: PC 부팅] 부모 제어 장비 {0}의 기동 전원(WOL)을 송신합니다.", targetDev.Name));
                    mainForm.AddWolRetryQueue(targetDev.Id);
                    if (mainForm.IsRealNetworkControlMode)
                    {
                        mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] WOL Magic Packet UDP 브로드캐스트 전송 -> MAC: {0}", targetDev.MacAddress));
                        mainForm.SendWOLMagicPacket(targetDev.MacAddress);
                    }

                    targetDev.RuntimeStatus = "BOOTING";
                    int pcDelay = targetDev.BootDelaySeconds > 0 ? targetDev.BootDelaySeconds : 5;
                    targetDev.RemainingSeconds = pcDelay;
                    mainForm.UpdateVisualDashboard();

                    for (int i = pcDelay; i >= 0; i--)
                    {
                        targetDev.RemainingSeconds = i;
                        mainForm.UpdateVisualDashboard();
                        if (i > 0) await Task.Delay(1000);
                    }

                    targetDev.RuntimeStatus = "ONLINE";
                    targetDev.RemainingSeconds = 0;
                    mainForm.UpdateVisualDashboard();

                    // 2단계: 연동 자식 프로젝터 기동
                    if (linkedDevices.Count > 0)
                    {
                        mainForm.LogMessage("▶ [2단계: 프로젝터 기동] 연동된 자식 빔 프로젝터들을 순차 기동합니다.");
                        int maxProjDelay = 0;
                        foreach (var proj in linkedDevices)
                        {
                            proj.RuntimeStatus = "BOOTING";
                            int dly = proj.BootDelaySeconds > 0 ? proj.BootDelaySeconds : 10;
                            proj.RemainingSeconds = dly;
                            if (dly > maxProjDelay) maxProjDelay = dly;

                            if (mainForm.IsRealNetworkControlMode)
                            {
                                mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] PJLink ON 패킷 실제 송출 -> IP: {0}:{1}", proj.IpAddress, proj.Port));
                                #pragma warning disable 4014
                                mainForm.SendPJLinkCommandAsync(proj.IpAddress, proj.Port, "%1POWR 1");
                                #pragma warning restore 4014
                            }
                        }
                        mainForm.UpdateVisualDashboard();

                        for (int i = maxProjDelay; i > 0; i--)
                        {
                            foreach (var proj in linkedDevices)
                            {
                                if (proj.RemainingSeconds > 0) proj.RemainingSeconds--;
                            }
                            mainForm.UpdateVisualDashboard();
                            await Task.Delay(1000);
                        }

                        foreach (var proj in linkedDevices)
                        {
                            proj.RuntimeStatus = "ONLINE";
                            proj.RemainingSeconds = 0;
                        }
                    }
                }
                else if (targetDev.Type == "Projector")
                {
                    // 단독 프로젝터 기동
                    if (mainForm.IsRealNetworkControlMode)
                    {
                        mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] PJLink ON 패킷 실제 송출 -> IP: {0}:{1}", targetDev.IpAddress, targetDev.Port));
                        #pragma warning disable 4014
                        mainForm.SendPJLinkCommandAsync(targetDev.IpAddress, targetDev.Port, "%1POWR 1");
                        #pragma warning restore 4014
                    }
                    targetDev.RuntimeStatus = "BOOTING";
                    int projDelay = targetDev.BootDelaySeconds > 0 ? targetDev.BootDelaySeconds : 10;
                    targetDev.RemainingSeconds = projDelay;
                    mainForm.UpdateVisualDashboard();

                    for (int i = projDelay; i >= 0; i--)
                    {
                        targetDev.RemainingSeconds = i;
                        mainForm.UpdateVisualDashboard();
                        if (i > 0) await Task.Delay(1000);
                    }
                    targetDev.RuntimeStatus = "ONLINE";
                    targetDev.RemainingSeconds = 0;
                }
            }

            mainForm.LogMessage(string.Format("====== [{0} 및 자식 빔 프로젝터 연동 기동 완료 (ONLINE)] ======", targetDev.Name));
            mainForm.UpdateVisualDashboard();
        }

        private async Task RunIndividualPowerOff(DeviceItem targetDev)
        {
            mainForm.RemoveWolRetryQueue(targetDev.Id);
            if (targetDev.RuntimeStatus == "OFFLINE" || mainForm.IsRunningSimulation) return;

            mainForm.LogMessage(string.Format("====== [{0} 비동기 개별 종료 시작] ======", targetDev.Name));

            if (targetDev.Type == "PC")
            {
                // [1단계] 부모 PC 즉각 원격 셧다운 명령 전송 (TCP TCP shutdown agent)
                if (mainForm.IsRealNetworkControlMode)
                {
                    mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] TCP Shutdown 명령 송신 -> IP: {0}:{1}", targetDev.IpAddress, targetDev.Port));
                    #pragma warning disable 4014
                    mainForm.SendTcpShutdownCommandAsync(targetDev.IpAddress, targetDev.Port);
                    #pragma warning restore 4014
                }

                targetDev.LastShutdownTime = DateTime.Now;
                targetDev.RuntimeStatus = "OFFLINE";
                mainForm.LogMessage(string.Format("▶ [1단계: PC 종료] PC {0} 종료 신호 수신 및 셧다운 완료.", targetDev.Name));
                mainForm.UpdateVisualDashboard();

                if (linkedDevices.Count > 0)
                {
                    mainForm.LogMessage("▶ [완충 대기] 안정적인 영상 주파수 신호 차단을 위해 3초 대기합니다.");
                    for (int w = 3; w > 0; w--)
                    {
                        await Task.Delay(1000);
                    }

                    mainForm.LogMessage("▶ [2단계: 프로젝터 쿨링] 자식 빔 프로젝터들의 PJLink OFF 전원 차단 및 냉각 팬 가동을 진행합니다.");
                    foreach (var proj in linkedDevices)
                    {
                        proj.RuntimeStatus = "COOLING";
                        proj.RemainingSeconds = 5;

                        // 실제 네트워크 모드 시 프로젝터 전원 끄기 PJLink 명령어 실제 송출
                        if (mainForm.IsRealNetworkControlMode)
                        {
                            mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] PJLink OFF 패킷 실제 송출 -> IP: {0}:{1}", proj.IpAddress, proj.Port));
                            #pragma warning disable 4014
                            mainForm.SendPJLinkCommandAsync(proj.IpAddress, proj.Port, "%1POWR 0");
                            #pragma warning restore 4014
                        }
                    }
                    mainForm.UpdateVisualDashboard();

                    for (int i = 5; i >= 0; i--)
                    {
                        foreach (var proj in linkedDevices)
                        {
                            proj.RemainingSeconds = i;
                        }
                        mainForm.UpdateVisualDashboard();
                        await Task.Delay(1000);
                    }

                    foreach (var proj in linkedDevices)
                    {
                        proj.RuntimeStatus = "OFFLINE";
                        proj.RemainingSeconds = 0;
                        mainForm.LogMessage(string.Format("   └🔗 {0} 냉각 완료 ➡️ 안전 대기(Standby) 모드 진입 확인", proj.Name));
                    }
                    mainForm.UpdateVisualDashboard();
                }
            }
            else 
            {
                // 단독 프로젝터 종료 시
                if (mainForm.IsRealNetworkControlMode)
                {
                    mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] PJLink OFF 패킷 실제 송출 -> IP: {0}:{1}", targetDev.IpAddress, targetDev.Port));
                    #pragma warning disable 4014
                    mainForm.SendPJLinkCommandAsync(targetDev.IpAddress, targetDev.Port, "%1POWR 0");
                    #pragma warning restore 4014
                }

                targetDev.RuntimeStatus = "COOLING";
                targetDev.RemainingSeconds = 5;
                mainForm.UpdateVisualDashboard();

                for (int i = 5; i >= 0; i--)
                {
                    targetDev.RemainingSeconds = i;
                    mainForm.UpdateVisualDashboard();
                    await Task.Delay(1000);
                }
                targetDev.RuntimeStatus = "OFFLINE";
                targetDev.RemainingSeconds = 0;
                mainForm.LogMessage(string.Format(">> [종료 완료] 프로젝터 {0} 냉각 완료 및 대기 진입.", targetDev.Name));
                mainForm.UpdateVisualDashboard();
            }

            mainForm.LogMessage(string.Format("====== [{0} 비동기 개별 종료 및 냉각 완결] ======", targetDev.Name));
        }

        private async void BtnOn_Click(object sender, EventArgs e)
        {
            await RunIndividualPowerOn(device);
        }

        private async void BtnOff_Click(object sender, EventArgs e)
        {
            await RunIndividualPowerOff(device);
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (mainForm.IsRunningSimulation) return;
            mainForm.OpenCompositeEditForm(device.Id);
        }

        private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (animTimer != null)
                {
                    animTimer.Stop();
                    animTimer.Tick -= AnimTimer_Tick;
                    animTimer.Dispose();
                    animTimer = null;
                }
                if (btnOn != null) { btnOn.Dispose(); btnOn = null; }
                if (btnOff != null) { btnOff.Dispose(); btnOff = null; }
                if (btnEdit != null) { btnEdit.Dispose(); btnEdit = null; }
            }
            base.Dispose(disposing);
        }
    }

    // --- 5. 사용자 입력을 안전하게 받아올 수 있는 커스텀 다이얼로그 ---
    public class PromptForm : Form
    {
        private Label lblPrompt;
        private TextBox txtInput;
        private Button btnOk;
        private Button btnCancel;

        public string InputText { get { return txtInput.Text.Trim(); } }

        public PromptForm(string promptText, string title)
        {
            this.Text = title;
            this.Size = new Size(320, 160);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 25, 38);
            this.ForeColor = Color.White;

            lblPrompt = new Label() { Text = promptText, Location = new Point(20, 15), Size = new Size(260, 20), Font = new Font("Malgun Gothic", 9f, FontStyle.Bold) };
            txtInput = new TextBox() { Location = new Point(20, 42), Size = new Size(260, 22), Font = new Font("Malgun Gothic", 9.5f), BackColor = Color.FromArgb(28, 29, 43), ForeColor = Color.White };
            
            btnOk = new Button() { Text = "확인", Location = new Point(125, 80), Size = new Size(75, 26), BackColor = Color.FromArgb(123, 97, 255), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
            btnOk.FlatAppearance.BorderSize = 0;
            
            btnCancel = new Button() { Text = "취소", Location = new Point(205, 80), Size = new Size(75, 26), BackColor = Color.FromArgb(35, 37, 54), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(lblPrompt);
            this.Controls.Add(txtInput);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }

    // --- 6. 신규 장치 입력 및 확인용 전용 독립 팝업 폼 (DeviceAddForm) ---
    public class DeviceAddForm : Form
    {
        private List<DeviceItem> tempDevices;
        private List<string> tempSpaces;

        private TextBox txtId;
        private TextBox txtName;
        private ComboBox cbType;
        private TextBox txtIp;
        private TextBox txtMac;
        private TextBox txtPort;
        private ComboBox cbSpace;
        private ComboBox cbAssociated;
        private Label lblAssociatedInfo;
        private ComboBox cbOrder;
        private TextBox txtDelay;
        private TextBox txtDesc;
        private ComboBox cbSequenceMode;
        private Label lblSequenceModeInfo;

        private Button btnAddConfirm;
        private Button btnCancel;

        private bool isSuggesting = false;
        private bool isUserCustomId = false;

        public DeviceItem AddedDevice { get; private set; }

        public DeviceAddForm(List<DeviceItem> currentDevices, List<string> currentSpaces)
        {
            this.tempDevices = currentDevices;
            this.tempSpaces = currentSpaces;

            this.Text = "➕ 신규 장치 추가 등록 (Add New Device)";
            this.Size = new Size(480, 620);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeAddUI(boxClr);
            SuggestNextDeviceId();
        }

        private void InitializeAddUI(Color boxClr)
        {
            Font fontLabel = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Font fontInput = new Font("Malgun Gothic", 9.5f);

            int xL = 25; int xI = 180; int yStart = 25; int yGap = 40;

            Label lId = new Label() { Text = "장비 고유 ID:", Location = new Point(xL, yStart), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtId = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };
            txtId.TextChanged += (s, e) => {
                if (!isSuggesting) isUserCustomId = true;
            };
            
            Label lName = new Label() { Text = "장비 이름:", Location = new Point(xL, yStart + yGap), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtName = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };

            Label lType = new Label() { Text = "장비 유형 (Type):", Location = new Point(xL, yStart + yGap * 2), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbType = new ComboBox() { Location = new Point(xI, yStart + yGap * 2 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbType.Items.Add("PC");
            cbType.Items.Add("Projector");
            cbType.SelectedIndex = 0;
            cbType.SelectedIndexChanged += (s, e) => {
                SwitchAssociatedControl();
                SuggestNextDeviceId();
            };

            Label lIp = new Label() { Text = "IP 주소 (IpAddress):", Location = new Point(xL, yStart + yGap * 3), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtIp = new TextBox() { Location = new Point(xI, yStart + yGap * 3 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = "192.168.0.100" };

            Label lMac = new Label() { Text = "MAC 주소 (WOL용):", Location = new Point(xL, yStart + yGap * 4), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtMac = new TextBox() { Location = new Point(xI, yStart + yGap * 4 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = "00-11-22-33-44-55" };

            Label lPort = new Label() { Text = "TCP 통신 포트:", Location = new Point(xL, yStart + yGap * 5), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtPort = new TextBox() { Location = new Point(xI, yStart + yGap * 5 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = "9999" };

            Label lSpace = new Label() { Text = "소속 공간 (Space):", Location = new Point(xL, yStart + yGap * 6), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbSpace = new ComboBox() { Location = new Point(xI, yStart + yGap * 6 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            foreach (var sp in tempSpaces) cbSpace.Items.Add(sp);
            if (cbSpace.Items.Count > 0) cbSpace.SelectedIndex = 0;

            Label lAssoc = new Label() { Text = "결합 기기 ID (Assoc):", Location = new Point(xL, yStart + yGap * 7), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbAssociated = new ComboBox() { Location = new Point(xI, yStart + yGap * 7 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            lblAssociatedInfo = new Label() { Text = "(연동은 프로젝터 설정에서 직접 매핑합니다)", Location = new Point(xI, yStart + yGap * 7 - 1), Size = new Size(240, 25), ForeColor = Color.Gray, Font = new Font("Malgun Gothic", 8f, FontStyle.Italic) };

            Label lOrder = new Label() { Text = "켜짐 우선순위 (Order):", Location = new Point(xL, yStart + yGap * 8), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbOrder = new ComboBox() { Location = new Point(xI, yStart + yGap * 8 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            for (int i = 1; i <= 10; i++) cbOrder.Items.Add(string.Format("{0} 순위", i));
            cbOrder.SelectedIndex = 0;

            Label lDelay = new Label() { Text = "기동 후 대기시간 (Delay):", Location = new Point(xL, yStart + yGap * 9), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtDelay = new TextBox() { Location = new Point(xI, yStart + yGap * 9 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = "0" };

            Label lSeqMode = new Label() { Text = "연동 기기 우선순위:", Location = new Point(xL, yStart + yGap * 10), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbSequenceMode = new ComboBox() { Location = new Point(xI, yStart + yGap * 10 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbSequenceMode.Items.Add("PC 우선 기동 (PC ➡️ 프로젝터)");
            cbSequenceMode.Items.Add("프로젝터 우선 (프로젝터 예열 ➡️ PC)");
            cbSequenceMode.SelectedIndex = 0;
            lblSequenceModeInfo = new Label() { Text = "(PC 장비에서 연동 순서를 설정합니다)", Location = new Point(xI, yStart + yGap * 10 - 1), Size = new Size(240, 25), ForeColor = Color.Gray, Font = new Font("Malgun Gothic", 8f, FontStyle.Italic) };

            Label lDesc = new Label() { Text = "설명:", Location = new Point(xL, yStart + yGap * 11), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtDesc = new TextBox() { Location = new Point(xI, yStart + yGap * 11 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = "신규 등록" };

            this.Controls.Add(lId); this.Controls.Add(txtId);
            this.Controls.Add(lName); this.Controls.Add(txtName);
            this.Controls.Add(lType); this.Controls.Add(cbType);
            this.Controls.Add(lIp); this.Controls.Add(txtIp);
            this.Controls.Add(lMac); this.Controls.Add(txtMac);
            this.Controls.Add(lPort); this.Controls.Add(txtPort);
            this.Controls.Add(lSpace); this.Controls.Add(cbSpace);
            this.Controls.Add(lAssoc); this.Controls.Add(cbAssociated); this.Controls.Add(lblAssociatedInfo);
            this.Controls.Add(lOrder); this.Controls.Add(cbOrder);
            this.Controls.Add(lDelay); this.Controls.Add(txtDelay);
            this.Controls.Add(lSeqMode); this.Controls.Add(cbSequenceMode); this.Controls.Add(lblSequenceModeInfo);
            this.Controls.Add(lDesc); this.Controls.Add(txtDesc);

            btnAddConfirm = new Button() { Text = "➕ 확인 및 추가", Size = new Size(180, 36), Location = new Point(80, 520), BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnAddConfirm.Click += BtnAddConfirm_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(120, 36), Location = new Point(275, 520), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(btnAddConfirm);
            this.Controls.Add(btnCancel);

            SwitchAssociatedControl();
        }

        private void SwitchAssociatedControl()
        {
            string devType = cbType.SelectedItem != null ? cbType.SelectedItem.ToString() : "PC";
            if (devType == "Projector")
            {
                cbAssociated.Visible = true;
                lblAssociatedInfo.Visible = false;

                cbAssociated.Items.Clear();
                cbAssociated.Items.Add("[ 연결 없음 ]");

                foreach (var d in tempDevices)
                {
                    if (d.Type == "PC") cbAssociated.Items.Add(string.Format("[{0}] {1}", d.Id, d.Name));
                }
                cbAssociated.SelectedIndex = 0;
            }
            else
            {
                cbAssociated.Visible = false;
                lblAssociatedInfo.Visible = true;
            }

            if (devType == "PC")
            {
                cbSequenceMode.Visible = true;
                lblSequenceModeInfo.Visible = false;
            }
            else
            {
                cbSequenceMode.Visible = false;
                lblSequenceModeInfo.Visible = true;
            }
        }

        private void BtnAddConfirm_Click(object sender, EventArgs e)
        {
            string newId = txtId.Text.Trim();
            if (string.IsNullOrEmpty(newId))
            {
                MessageBox.Show("장비 고유 ID는 필수값입니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (tempDevices.Exists(d => d.Id == newId))
            {
                int idx = 1;
                string testId = newId + "_" + idx;
                while (tempDevices.Exists(d => d.Id == testId))
                {
                    idx++;
                    testId = newId + "_" + idx;
                }
                newId = testId;
            }

            int prt, dly;
            int.TryParse(txtPort.Text, out prt);
            int.TryParse(txtDelay.Text, out dly);

            string selectedAssocId = "";
            if (cbAssociated.Visible && cbAssociated.SelectedItem != null)
            {
                string tag = cbAssociated.SelectedItem.ToString();
                if (tag.StartsWith("["))
                {
                    int endIdx = tag.IndexOf(']');
                    if (endIdx > 1) selectedAssocId = tag.Substring(1, endIdx - 1);
                }
            }

            AddedDevice = new DeviceItem()
            {
                Id = newId,
                Name = txtName.Text.Trim() == "" ? "신규 장치" : txtName.Text.Trim(),
                Type = cbType.SelectedItem != null ? cbType.SelectedItem.ToString() : "PC",
                IpAddress = txtIp.Text.Trim(),
                MacAddress = txtMac.Text.Trim(),
                Port = prt,
                Space = cbSpace.SelectedItem != null ? cbSpace.SelectedItem.ToString() : "로비",
                AssociatedDeviceId = (cbType.SelectedItem != null && cbType.SelectedItem.ToString() == "Projector") ? selectedAssocId : "",
                BootOrder = cbOrder.SelectedIndex + 1,
                BootDelaySeconds = dly,
                Description = txtDesc.Text,
                PowerOnSequenceMode = (cbType.SelectedItem != null && cbType.SelectedItem.ToString() == "PC" && cbSequenceMode.SelectedIndex == 1) ? "PROJ_FIRST" : "PC_FIRST"
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void SuggestNextDeviceId()
        {
            if (isUserCustomId) return;

            string selectedType = cbType.SelectedItem != null ? cbType.SelectedItem.ToString() : "PC";
            string prefix = selectedType == "PC" ? "PC_" : "PROJ_";

            int maxIndex = 0;
            foreach (var d in tempDevices)
            {
                if (d.Id.StartsWith(prefix))
                {
                    string suffix = d.Id.Substring(prefix.Length);
                    int idx;
                    if (int.TryParse(suffix, out idx))
                    {
                        if (idx > maxIndex) maxIndex = idx;
                    }
                }
            }

            int nextIndex = maxIndex + 1;
            string recommendedId = string.Format("{0}{1:D2}", prefix, nextIndex);

            isSuggesting = true;
            txtId.Text = recommendedId;
            isSuggesting = false;
        }
    }

    // --- 6-2. 기존 장치 정보 수정 모달 다이얼로그 (DeviceEditForm) ---
    public class DeviceEditForm : Form
    {
        private List<DeviceItem> tempDevices;
        private List<string> tempSpaces;
        private DeviceItem targetDevice;

        private TextBox txtId;
        private TextBox txtName;
        private ComboBox cbType;
        private TextBox txtIp;
        private TextBox txtMac;
        private TextBox txtPort;
        private ComboBox cbSpace;
        private ComboBox cbAssociated;
        private Label lblAssociatedInfo;
        private ComboBox cbOrder;
        private TextBox txtDelay;
        private TextBox txtDesc;
        private ComboBox cbSequenceMode;
        private Label lblSequenceModeInfo;

        private Button btnSaveConfirm;
        private Button btnCancel;

        public DeviceItem EditedDevice { get; private set; }

        public DeviceEditForm(DeviceItem dev, List<DeviceItem> currentDevices, List<string> currentSpaces)
        {
            this.targetDevice = dev;
            this.tempDevices = currentDevices;
            this.tempSpaces = currentSpaces;

            this.Text = string.Format("✏️ 장치 정보 수정 (Edit Device) - [{0}]", dev.Name);
            this.Size = new Size(480, 620);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeEditUI(boxClr);
            LoadDeviceData();
        }

        private void InitializeEditUI(Color boxClr)
        {
            Font fontLabel = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Font fontInput = new Font("Malgun Gothic", 9.5f);

            int xL = 25; int xI = 180; int yStart = 25; int yGap = 40;

            Label lId = new Label() { Text = "장비 고유 ID:", Location = new Point(xL, yStart), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtId = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };
            
            Label lName = new Label() { Text = "장비 이름:", Location = new Point(xL, yStart + yGap), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtName = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };

            Label lType = new Label() { Text = "장비 유형 (Type):", Location = new Point(xL, yStart + yGap * 2), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbType = new ComboBox() { Location = new Point(xI, yStart + yGap * 2 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbType.Items.Add("PC");
            cbType.Items.Add("Projector");
            cbType.SelectedIndexChanged += (s, e) => SwitchAssociatedControl();

            Label lIp = new Label() { Text = "IP 주소 (IpAddress):", Location = new Point(xL, yStart + yGap * 3), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtIp = new TextBox() { Location = new Point(xI, yStart + yGap * 3 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };

            Label lMac = new Label() { Text = "MAC 주소 (WOL용):", Location = new Point(xL, yStart + yGap * 4), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtMac = new TextBox() { Location = new Point(xI, yStart + yGap * 4 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };

            Label lPort = new Label() { Text = "TCP 통신 포트:", Location = new Point(xL, yStart + yGap * 5), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtPort = new TextBox() { Location = new Point(xI, yStart + yGap * 5 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };

            Label lSpace = new Label() { Text = "소속 공간 (Space):", Location = new Point(xL, yStart + yGap * 6), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbSpace = new ComboBox() { Location = new Point(xI, yStart + yGap * 6 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            foreach (var sp in tempSpaces) cbSpace.Items.Add(sp);

            Label lAssoc = new Label() { Text = "결합 기기 ID (Assoc):", Location = new Point(xL, yStart + yGap * 7), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbAssociated = new ComboBox() { Location = new Point(xI, yStart + yGap * 7 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            lblAssociatedInfo = new Label() { Text = "(연동은 프로젝터 설정에서 직접 매핑합니다)", Location = new Point(xI, yStart + yGap * 7 - 1), Size = new Size(240, 25), ForeColor = Color.Gray, Font = new Font("Malgun Gothic", 8f, FontStyle.Italic) };

            Label lOrder = new Label() { Text = "켜짐 우선순위 (Order):", Location = new Point(xL, yStart + yGap * 8), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbOrder = new ComboBox() { Location = new Point(xI, yStart + yGap * 8 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            for (int i = 1; i <= 10; i++) cbOrder.Items.Add(string.Format("{0} 순위", i));

            Label lDelay = new Label() { Text = "기동 후 대기시간 (Delay):", Location = new Point(xL, yStart + yGap * 9), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtDelay = new TextBox() { Location = new Point(xI, yStart + yGap * 9 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };

            Label lSeqMode = new Label() { Text = "연동 기기 우선순위:", Location = new Point(xL, yStart + yGap * 10), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            cbSequenceMode = new ComboBox() { Location = new Point(xI, yStart + yGap * 10 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cbSequenceMode.Items.Add("PC 우선 기동 (PC ➡️ 프로젝터)");
            cbSequenceMode.Items.Add("프로젝터 우선 (프로젝터 예열 ➡️ PC)");
            cbSequenceMode.SelectedIndex = 0;
            lblSequenceModeInfo = new Label() { Text = "(PC 장비에서 연동 순서를 설정합니다)", Location = new Point(xI, yStart + yGap * 10 - 1), Size = new Size(240, 25), ForeColor = Color.Gray, Font = new Font("Malgun Gothic", 8f, FontStyle.Italic) };

            Label lDesc = new Label() { Text = "설명:", Location = new Point(xL, yStart + yGap * 11), AutoSize = true, ForeColor = Color.White, Font = fontLabel };
            txtDesc = new TextBox() { Location = new Point(xI, yStart + yGap * 11 - 3), Size = new Size(240, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White };

            this.Controls.Add(lId); this.Controls.Add(txtId);
            this.Controls.Add(lName); this.Controls.Add(txtName);
            this.Controls.Add(lType); this.Controls.Add(cbType);
            this.Controls.Add(lIp); this.Controls.Add(txtIp);
            this.Controls.Add(lMac); this.Controls.Add(txtMac);
            this.Controls.Add(lPort); this.Controls.Add(txtPort);
            this.Controls.Add(lSpace); this.Controls.Add(cbSpace);
            this.Controls.Add(lAssoc); this.Controls.Add(cbAssociated); this.Controls.Add(lblAssociatedInfo);
            this.Controls.Add(lOrder); this.Controls.Add(cbOrder);
            this.Controls.Add(lDelay); this.Controls.Add(txtDelay);
            this.Controls.Add(lSeqMode); this.Controls.Add(cbSequenceMode); this.Controls.Add(lblSequenceModeInfo);
            this.Controls.Add(lDesc); this.Controls.Add(txtDesc);

            btnSaveConfirm = new Button() { Text = "💾 수정사항 저장", Size = new Size(180, 36), Location = new Point(80, 520), BackColor = Color.FromArgb(123, 97, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnSaveConfirm.Click += BtnSaveConfirm_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(120, 36), Location = new Point(275, 520), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(btnSaveConfirm);
            this.Controls.Add(btnCancel);
        }

        private void LoadDeviceData()
        {
            txtId.Text = targetDevice.Id;
            txtName.Text = targetDevice.Name;
            cbType.SelectedItem = targetDevice.Type;
            txtIp.Text = targetDevice.IpAddress;
            txtMac.Text = targetDevice.MacAddress;
            txtPort.Text = targetDevice.Port.ToString();
            cbSpace.SelectedItem = targetDevice.Space;
            txtDelay.Text = targetDevice.BootDelaySeconds.ToString();
            txtDesc.Text = targetDevice.Description;
            cbSequenceMode.SelectedIndex = (targetDevice.PowerOnSequenceMode == "PROJ_FIRST") ? 1 : 0;

            int order = targetDevice.BootOrder;
            if (order < 1) order = 1;
            if (order > 10) order = 10;
            cbOrder.SelectedIndex = order - 1;

            SwitchAssociatedControl();
            if (targetDevice.Type == "Projector")
            {
                int selIndex = 0;
                int counter = 1;
                foreach (var d in tempDevices)
                {
                    if (d.Id == targetDevice.Id || d.Type != "PC") continue;
                    if (d.Id == targetDevice.AssociatedDeviceId)
                    {
                        selIndex = counter;
                        break;
                    }
                    counter++;
                }
                if (cbAssociated.Items.Count > selIndex) cbAssociated.SelectedIndex = selIndex;
            }
        }

        private void SwitchAssociatedControl()
        {
            string devType = cbType.SelectedItem != null ? cbType.SelectedItem.ToString() : "PC";
            if (devType == "Projector")
            {
                cbAssociated.Visible = true;
                lblAssociatedInfo.Visible = false;

                cbAssociated.Items.Clear();
                cbAssociated.Items.Add("[ 연결 없음 ]");

                foreach (var d in tempDevices)
                {
                    if (d.Id != targetDevice.Id && d.Type == "PC") cbAssociated.Items.Add(string.Format("[{0}] {1}", d.Id, d.Name));
                }
                cbAssociated.SelectedIndex = 0;
            }
            else
            {
                cbAssociated.Visible = false;
                lblAssociatedInfo.Visible = true;
            }

            if (devType == "PC")
            {
                cbSequenceMode.Visible = true;
                lblSequenceModeInfo.Visible = false;
            }
            else
            {
                cbSequenceMode.Visible = false;
                lblSequenceModeInfo.Visible = true;
            }
        }

        private void BtnSaveConfirm_Click(object sender, EventArgs e)
        {
            string newId = txtId.Text.Trim();
            if (string.IsNullOrEmpty(newId))
            {
                MessageBox.Show("장비 고유 ID는 필수값입니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (newId != targetDevice.Id && tempDevices.Exists(d => d.Id == newId))
            {
                MessageBox.Show("이미 존재하는 장비 고유 ID입니다. 다른 ID를 입력해 주세요.", "ID 중복 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int prt, dly;
            int.TryParse(txtPort.Text, out prt);
            int.TryParse(txtDelay.Text, out dly);

            string selectedAssocId = "";
            if (cbAssociated.Visible && cbAssociated.SelectedItem != null)
            {
                string tag = cbAssociated.SelectedItem.ToString();
                if (tag.StartsWith("["))
                {
                    int endIdx = tag.IndexOf(']');
                    if (endIdx > 1) selectedAssocId = tag.Substring(1, endIdx - 1);
                }
            }

            EditedDevice = new DeviceItem()
            {
                Id = newId,
                Name = txtName.Text.Trim() == "" ? "수정된 장치" : txtName.Text.Trim(),
                Type = cbType.SelectedItem != null ? cbType.SelectedItem.ToString() : "PC",
                IpAddress = txtIp.Text.Trim(),
                MacAddress = txtMac.Text.Trim(),
                Port = prt,
                Space = cbSpace.SelectedItem != null ? cbSpace.SelectedItem.ToString() : "로비",
                AssociatedDeviceId = (cbType.SelectedItem != null && cbType.SelectedItem.ToString() == "Projector") ? selectedAssocId : "",
                BootOrder = cbOrder.SelectedIndex + 1,
                BootDelaySeconds = dly,
                Description = txtDesc.Text,
                RuntimeStatus = targetDevice.RuntimeStatus,
                PowerOnSequenceMode = (cbType.SelectedItem != null && cbType.SelectedItem.ToString() == "PC" && cbSequenceMode.SelectedIndex == 1) ? "PROJ_FIRST" : "PC_FIRST"
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    // --- 7. 통합 연동 기기 조율을 위한 1:N 전용 계층형 편집 다이얼로그 (DeviceCompositeEditForm) ---
    public class ProjRowControls
    {
        public DeviceItem TargetProj { get; set; }
        public ComboBox cbOrder { get; set; }
        public TextBox txtDelay { get; set; }
    }

    public class DeviceCompositeEditForm : Form
    {
        private DeviceItem parentDevice;
        private List<DeviceItem> childDevices;
        private List<DeviceItem> allDevices;
        private PowerControllerForm mainForm;

        private TextBox txtName;
        private TextBox txtIp;
        private TextBox txtMac;
        private TextBox txtPort;
        private ComboBox cbOrder;
        private TextBox txtDelay;
        private TextBox txtDesc;

        private GroupBox gbSubs;
        private List<ProjRowControls> subProjRows = new List<ProjRowControls>();

        private Button btnSave;
        private Button btnCancel;

        public DeviceCompositeEditForm(string parentId, List<DeviceItem> currentAllDevices, PowerControllerForm parentForm)
        {
            this.mainForm = parentForm;
            this.allDevices = currentAllDevices;
            
            this.parentDevice = currentAllDevices.Find(d => d.Id == parentId);
            if (this.parentDevice == null) return;

            this.childDevices = currentAllDevices.FindAll(d => d.Type == "Projector" && d.AssociatedDeviceId == parentId);

            this.Text = string.Format("⚙️ 통합 기동 세부 설정 - [{0}]", parentDevice.Name);
            this.Size = new Size(600, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeFormControls(boxClr);
        }

        private void InitializeFormControls(Color boxClr)
        {
            Font fontLabel = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Font fontInput = new Font("Malgun Gothic", 9.5f);

            GroupBox gbParent = new GroupBox();
            gbParent.Text = string.Format(" 부모 제어 장비 설정 ({0}) ", parentDevice.Type);
            gbParent.ForeColor = Color.White;
            gbParent.Font = fontLabel;
            gbParent.Size = new Size(540, 220);
            gbParent.Location = new Point(20, 10);

            int xL = 15; int xI = 140; int yStart = 25; int yGap = 30;

            Label lName = new Label() { Text = "장비 이름:", Location = new Point(xL, yStart), AutoSize = true };
            txtName = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(180, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = parentDevice.Name };

            Label lIp = new Label() { Text = "IP 주소:", Location = new Point(xL, yStart + yGap), AutoSize = true };
            txtIp = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(180, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = parentDevice.IpAddress };

            Label lMac = new Label() { Text = "MAC 주소:", Location = new Point(xL, yStart + yGap * 2), AutoSize = true };
            txtMac = new TextBox() { Location = new Point(xI, yStart + yGap * 2 - 3), Size = new Size(180, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = parentDevice.MacAddress };

            Label lPort = new Label() { Text = "TCP 포트:", Location = new Point(xL, yStart + yGap * 3), AutoSize = true };
            txtPort = new TextBox() { Location = new Point(xI, yStart + yGap * 3 - 3), Size = new Size(180, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = parentDevice.Port.ToString() };

            Label lOrder = new Label() { Text = "켜짐 우선순위:", Location = new Point(xL, yStart + yGap * 4), AutoSize = true };
            cbOrder = new ComboBox() { Location = new Point(xI, yStart + yGap * 4 - 3), Size = new Size(180, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            for (int k = 1; k <= 10; k++) cbOrder.Items.Add(string.Format("{0} 순위", k));
            cbOrder.SelectedIndex = Math.Min(Math.Max(parentDevice.BootOrder - 1, 0), 9);

            Label lDelay = new Label() { Text = "기동 후 대기시간:", Location = new Point(xL, yStart + yGap * 5), AutoSize = true };
            txtDelay = new TextBox() { Location = new Point(xI, yStart + yGap * 5 - 3), Size = new Size(180, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = parentDevice.BootDelaySeconds.ToString() };

            Label lDesc = new Label() { Text = "설명:", Location = new Point(345, yStart), AutoSize = true };
            txtDesc = new TextBox() { Location = new Point(345, yStart + 22), Size = new Size(180, 140), Multiline = true, Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = parentDevice.Description };

            gbParent.Controls.Add(lName); gbParent.Controls.Add(txtName);
            gbParent.Controls.Add(lIp); gbParent.Controls.Add(txtIp);
            gbParent.Controls.Add(lMac); gbParent.Controls.Add(txtMac);
            gbParent.Controls.Add(lPort); gbParent.Controls.Add(txtPort);
            gbParent.Controls.Add(lOrder); gbParent.Controls.Add(cbOrder);
            gbParent.Controls.Add(lDelay); gbParent.Controls.Add(txtDelay);
            gbParent.Controls.Add(lDesc); gbParent.Controls.Add(txtDesc);

            gbSubs = new GroupBox();
            gbSubs.Text = " 하위 귀속 빔 프로젝터 연동 설정 및 기동 딜레이 조율 (1:N) ";
            gbSubs.ForeColor = Color.White;
            gbSubs.Font = fontLabel;
            gbSubs.Size = new Size(540, 200);
            gbSubs.Location = new Point(20, 240);

            if (childDevices.Count == 0)
            {
                Label lblEmpty = new Label() 
                { 
                    Text = "연동된 하위 빔 프로젝터 장치가 없습니다.\n(설정창에서 프로젝터를 추가하여 이 PC와 매핑해 주십시오.)", 
                    Location = new Point(30, 70), 
                    Size = new Size(480, 60), 
                    ForeColor = Color.Gray, 
                    Font = new Font("Malgun Gothic", 10f, FontStyle.Italic),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                gbSubs.Controls.Add(lblEmpty);
            }
            else
            {
                int yOffset = 35;
                foreach (var proj in childDevices)
                {
                    Label lblProjName = new Label() { Text = string.Format("📹 {0}", proj.Name), Location = new Point(15, yOffset), Size = new Size(160, 20), ForeColor = Color.FromArgb(123, 97, 255), AutoEllipsis = true };
                    
                    Label lblOrderText = new Label() { Text = "우선순위:", Location = new Point(185, yOffset), AutoSize = true, ForeColor = Color.LightGray };
                    ComboBox cbProjOrder = new ComboBox() { Location = new Point(245, yOffset - 3), Size = new Size(90, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                    for (int k = 1; k <= 10; k++) cbProjOrder.Items.Add(string.Format("{0} 순위", k));
                    cbProjOrder.SelectedIndex = Math.Min(Math.Max(proj.BootOrder - 1, 0), 9);

                    Label lblDelayText = new Label() { Text = "기동 대기초:", Location = new Point(355, yOffset), AutoSize = true, ForeColor = Color.LightGray };
                    TextBox txtProjDelay = new TextBox() { Location = new Point(435, yOffset - 3), Size = new Size(70, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = proj.BootDelaySeconds.ToString() };

                    gbSubs.Controls.Add(lblProjName);
                    gbSubs.Controls.Add(lblOrderText);
                    gbSubs.Controls.Add(cbProjOrder);
                    gbSubs.Controls.Add(lblDelayText);
                    gbSubs.Controls.Add(txtProjDelay);

                    subProjRows.Add(new ProjRowControls { TargetProj = proj, cbOrder = cbProjOrder, txtDelay = txtProjDelay });
                    yOffset += 45;
                }
            }

            btnSave = new Button() { Text = "💾 변경사항 일괄 저장", Size = new Size(180, 36), Location = new Point(200, 465), BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(120, 36), Location = new Point(395, 465), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(gbParent);
            this.Controls.Add(gbSubs);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            int parentPort, parentDelay;
            if (!int.TryParse(txtPort.Text, out parentPort) || !int.TryParse(txtDelay.Text, out parentDelay))
            {
                MessageBox.Show("포트 및 대기 지연시간은 숫자만 입력 가능합니다.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            parentDevice.Name = txtName.Text.Trim();
            parentDevice.IpAddress = txtIp.Text.Trim();
            parentDevice.MacAddress = txtMac.Text.Trim();
            parentDevice.Port = parentPort;
            parentDevice.BootOrder = cbOrder.SelectedIndex + 1;
            parentDevice.BootDelaySeconds = parentDelay;
            parentDevice.Description = txtDesc.Text;

            foreach (var row in subProjRows)
            {
                int subDelay;
                if (!int.TryParse(row.txtDelay.Text, out subDelay)) subDelay = 0;
                
                row.TargetProj.BootOrder = row.cbOrder.SelectedIndex + 1;
                row.TargetProj.BootDelaySeconds = subDelay;
            }

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            try
            {
                DeviceConfig wrap = new DeviceConfig() 
                { 
                    Spaces = mainForm.CurrentSpaces,
                    Devices = allDevices,
                    Schedules = mainForm.CurrentScheduleSettings
                };
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string jsonText = serializer.Serialize(wrap);

                File.WriteAllText(configPath, jsonText, Encoding.UTF8);
                mainForm.ApplyUpdatedDevices(allDevices, mainForm.CurrentSpaces);

                MessageBox.Show("성공적으로 변경사항이 devices.json에 일괄 영구 저장되었습니다.", "저장 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // --- 8. 스케줄 상세 조정 전용 다이얼로그 (ScheduleConfigForm) ---
    public class ScheduleConfigForm : Form
    {
        private ScheduleSettings settings;
        private List<DeviceItem> allDevices;
        private List<string> allSpaces;
        private PowerControllerForm mainForm;

        private TextBox txtWeekdayStart;
        private TextBox txtWeekdayEnd;
        private TextBox txtSaturdayStart;
        private TextBox txtSaturdayEnd;

        private CheckBox chkMon;
        private CheckBox chkTue;
        private CheckBox chkWed;
        private CheckBox chkThu;
        private CheckBox chkFri;
        private CheckBox chkSat;
        private CheckBox chkSun;

        private Button btnSave;
        private Button btnCancel;

        public ScheduleConfigForm(ScheduleSettings currentSettings, List<DeviceItem> devs, List<string> sps, PowerControllerForm parent)
        {
            this.settings = currentSettings;
            this.allDevices = devs;
            this.allSpaces = sps;
            this.mainForm = parent;

            this.Text = "⏰ 자동 스케줄 세부 시간 조율 (Auto Schedule Config)";
            this.Size = new Size(420, 390);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeConfigUI(boxClr);
        }

        private void InitializeConfigUI(Color boxClr)
        {
            Font fontLabel = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            Font fontInput = new Font("Malgun Gothic", 9.5f);

            GroupBox gbTimes = new GroupBox();
            gbTimes.Text = " 요일별 전원 기동/종료 예약 시각 ";
            gbTimes.Size = new Size(360, 150);
            gbTimes.Location = new Point(20, 15);
            gbTimes.ForeColor = Color.White;
            gbTimes.Font = fontLabel;

            int xL = 15; int xI = 140; int yStart = 30; int yGap = 50;

            Label lWd = new Label() { Text = "평일(화~금) 시간:", Location = new Point(xL, yStart), AutoSize = true };
            txtWeekdayStart = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.WeekdayStart };
            Label lWdWave = new Label() { Text = "~", Location = new Point(xI + 85, yStart - 1), AutoSize = true };
            txtWeekdayEnd = new TextBox() { Location = new Point(xI + 105, yStart - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.WeekdayEnd };

            Label lSat = new Label() { Text = "토요일 시간:", Location = new Point(xL, yStart + yGap), AutoSize = true };
            txtSaturdayStart = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.SaturdayStart };
            Label lSatWave = new Label() { Text = "~", Location = new Point(xI + 85, yStart + yGap - 1), AutoSize = true };
            txtSaturdayEnd = new TextBox() { Location = new Point(xI + 105, yStart + yGap - 3), Size = new Size(80, 22), Font = fontInput, BackColor = boxClr, ForeColor = Color.White, Text = settings.SaturdayEnd };

            gbTimes.Controls.Add(lWd); gbTimes.Controls.Add(txtWeekdayStart); gbTimes.Controls.Add(lWdWave); gbTimes.Controls.Add(txtWeekdayEnd);
            gbTimes.Controls.Add(lSat); gbTimes.Controls.Add(txtSaturdayStart); gbTimes.Controls.Add(lSatWave); gbTimes.Controls.Add(txtSaturdayEnd);

            GroupBox gbIgnore = new GroupBox();
            gbIgnore.Text = " 자동 기동 무조건 차단 요일 (Ignore Days) ";
            gbIgnore.Size = new Size(360, 110);
            gbIgnore.Location = new Point(20, 180);
            gbIgnore.ForeColor = Color.White;
            gbIgnore.Font = fontLabel;

            chkMon = CreateDayCheck("월", "월요일", 15, 35);
            chkTue = CreateDayCheck("화", "화요일", 65, 35);
            chkWed = CreateDayCheck("수", "수요일", 115, 35);
            chkThu = CreateDayCheck("목", "목요일", 165, 35);
            chkFri = CreateDayCheck("금", "금요일", 215, 35);
            chkSat = CreateDayCheck("토", "토요일", 265, 35);
            chkSun = CreateDayCheck("일", "일요일", 315, 35);

            gbIgnore.Controls.Add(chkMon);
            gbIgnore.Controls.Add(chkTue);
            gbIgnore.Controls.Add(chkWed);
            gbIgnore.Controls.Add(chkThu);
            gbIgnore.Controls.Add(chkFri);
            gbIgnore.Controls.Add(chkSat);
            gbIgnore.Controls.Add(chkSun);

            Label lNote = new Label() { Text = "* 지정된 차단 요일은 스케줄 시각이 도달해도 전원을 제어하지 않습니다.", Location = new Point(15, 75), Size = new Size(330, 25), ForeColor = Color.Gray, Font = new Font("Malgun Gothic", 7.5f, FontStyle.Italic) };
            gbIgnore.Controls.Add(lNote);

            btnSave = new Button() { Text = "💾 시간표 저장", Size = new Size(160, 36), Location = new Point(60, 305), BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(100, 36), Location = new Point(240, 305), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(gbTimes);
            this.Controls.Add(gbIgnore);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
        }

        private CheckBox CreateDayCheck(string label, string tag, int x, int y)
        {
            CheckBox chk = new CheckBox();
            chk.Text = label;
            chk.Tag = tag;
            chk.Location = new Point(x, y);
            chk.Size = new Size(42, 20);
            chk.ForeColor = Color.White;
            chk.Checked = settings.IgnoreDays.Contains(tag);
            return chk;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!IsValidTimeFormat(txtWeekdayStart.Text) || !IsValidTimeFormat(txtWeekdayEnd.Text) ||
                !IsValidTimeFormat(txtSaturdayStart.Text) || !IsValidTimeFormat(txtSaturdayEnd.Text))
            {
                MessageBox.Show("시간 형식이 올바르지 않습니다. 반드시 '시:분(예: 08:50)' 규격으로 입력해 주십시오.", "형식 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            settings.WeekdayStart = txtWeekdayStart.Text.Trim();
            settings.WeekdayEnd = txtWeekdayEnd.Text.Trim();
            settings.SaturdayStart = txtSaturdayStart.Text.Trim();
            settings.SaturdayEnd = txtSaturdayEnd.Text.Trim();

            settings.IgnoreDays.Clear();
            List<CheckBox> chks = new List<CheckBox> { chkMon, chkTue, chkWed, chkThu, chkFri, chkSat, chkSun };
            foreach (var chk in chks)
            {
                if (chk.Checked) settings.IgnoreDays.Add(chk.Tag.ToString());
            }

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            try
            {
                DeviceConfig wrap = new DeviceConfig() 
                { 
                    Spaces = allSpaces,
                    Devices = allDevices,
                    Schedules = settings
                };
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string jsonText = serializer.Serialize(wrap);

                File.WriteAllText(configPath, jsonText, Encoding.UTF8);
                mainForm.ApplyScheduleSettings(settings);

                MessageBox.Show("자동 스케줄 설정 시간이 성공적으로 저장되었습니다.\n상세 가이드 텍스트가 실시간 업데이트됩니다.", "설정 저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsValidTimeFormat(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr)) return false;
            string[] parts = timeStr.Split(':');
            if (parts.Length != 2) return false;
            
            int hh, mm;
            if (!int.TryParse(parts[0], out hh) || !int.TryParse(parts[1], out mm)) return false;
            if (hh < 0 || hh > 23 || mm < 0 || mm > 59) return false;
            
            return true;
        }
    }

    // --- 9. REAL DEVICE CONFIG POPUP FORM ---
    public class DeviceConfigForm : Form
    {
        private List<DeviceItem> tempDevices;
        private List<string> tempSpaces;
        private string filterSpace;
        private PowerControllerForm mainForm;

        private ListView lvDevices;
        private Label lSpaceManage;
        private ComboBox cbSpace;
        private Button btnSpaceAdd;
        private Button btnSpaceDel;

        private Button btnAdd;             
        private Button btnEdit;            
        private Button btnDelete;          
        private Button btnApply;           

        public DeviceConfigForm(List<DeviceItem> currentDevices, List<string> currentSpaces, string filterSpace, PowerControllerForm parent)
        {
            this.mainForm = parent;
            this.filterSpace = filterSpace;
            this.tempSpaces = new List<string>(currentSpaces);
            this.tempDevices = new List<DeviceItem>();
            
            foreach (var item in currentDevices)
            {
                tempDevices.Add(new DeviceItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Type = item.Type,
                    IpAddress = item.IpAddress,
                    MacAddress = item.MacAddress,
                    Port = item.Port,
                    Space = item.Space,
                    AssociatedDeviceId = item.AssociatedDeviceId,
                    BootOrder = item.BootOrder,
                    BootDelaySeconds = item.BootDelaySeconds,
                    Description = item.Description,
                    RuntimeStatus = item.RuntimeStatus
                });
            }

            string filterLabel = filterSpace == "ALL" ? "전체 기기" : string.Format("공간: {0}", filterSpace);
            this.Text = string.Format("⚙️ 장치 편집 및 추가 (Device Manager) - [{0}]", filterLabel);

            this.Size = new Size(500, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeConfigUI(boxClr);
            LoadDeviceList();
        }

        private void InitializeConfigUI(Color boxClr)
        {
            Font fontLabel = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            Font fontInput = new Font("Malgun Gothic", 9.5f);

            lSpaceManage = new Label() { Text = "공간 관리:", Location = new Point(20, 20), AutoSize = true, ForeColor = Color.White, Font = fontLabel };

            cbSpace = new ComboBox() { Location = new Point(90, 16), Size = new Size(160, 24), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontInput };
            RefreshSpaceComboItems();

            btnSpaceAdd = new Button() { Text = "＋", Size = new Size(40, 26), Location = new Point(260, 15), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnSpaceAdd.FlatAppearance.BorderSize = 0;
            btnSpaceAdd.Click += BtnSpaceAdd_Click;

            btnSpaceDel = new Button() { Text = "－", Size = new Size(40, 26), Location = new Point(310, 15), BackColor = Color.FromArgb(239, 68, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnSpaceDel.FlatAppearance.BorderSize = 0;
            btnSpaceDel.Click += BtnSpaceDel_Click;

            lvDevices = new ListView();
            lvDevices.Size = new Size(440, 380);
            lvDevices.Location = new Point(20, 60);
            lvDevices.View = View.Details;
            lvDevices.FullRowSelect = true;
            lvDevices.GridLines = true;
            lvDevices.CheckBoxes = true; 
            lvDevices.BackColor = boxClr;
            lvDevices.ForeColor = Color.White;
            lvDevices.Columns.Add("ID", 85);
            lvDevices.Columns.Add("이름", 145);
            lvDevices.Columns.Add("공간", 120);
            lvDevices.Columns.Add("종류", 80);
            lvDevices.SelectedIndexChanged += LvDevices_SelectedIndexChanged;

            btnAdd = new Button() { Text = "➕ 장치 추가", Size = new Size(140, 36), Location = new Point(20, 460), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9f, FontStyle.Bold) };
            btnAdd.Click += BtnAdd_Click;

            btnEdit = new Button() { Text = "✏️ 장치 수정", Size = new Size(140, 36), Location = new Point(170, 460), BackColor = Color.FromArgb(123, 97, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9f, FontStyle.Bold) };
            btnEdit.Click += BtnEdit_Click;

            btnDelete = new Button() { Text = "🗑️ 선택 삭제", Size = new Size(140, 36), Location = new Point(320, 460), BackColor = Color.FromArgb(239, 68, 68), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9f, FontStyle.Bold) };
            btnDelete.Click += BtnDelete_Click;

            btnApply = new Button() { Text = "💾 전체 변경사항 저장 및 닫기", Size = new Size(440, 36), Location = new Point(20, 500), BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            btnApply.Click += BtnApply_Click;

            this.Controls.Add(lSpaceManage);
            this.Controls.Add(cbSpace);
            this.Controls.Add(btnSpaceAdd);
            this.Controls.Add(btnSpaceDel);
            this.Controls.Add(lvDevices);
            this.Controls.Add(btnAdd);
            this.Controls.Add(btnEdit);
            this.Controls.Add(btnDelete);
            this.Controls.Add(btnApply);
        }

        private void RefreshSpaceComboItems()
        {
            cbSpace.Items.Clear();
            foreach (var sp in tempSpaces)
            {
                cbSpace.Items.Add(sp);
            }
            if (cbSpace.Items.Count > 0)
            {
                cbSpace.SelectedIndex = 0;
            }
        }

        private void LoadDeviceList()
        {
            lvDevices.Items.Clear();
            foreach (var dev in tempDevices)
            {
                if (filterSpace != "ALL" && dev.Space != filterSpace)
                {
                    continue;
                }

                ListViewItem item = new ListViewItem(dev.Id);
                item.SubItems.Add(dev.Name);
                item.SubItems.Add(dev.Space);
                item.SubItems.Add(dev.Type);
                lvDevices.Items.Add(item);
            }

            if (lvDevices.Items.Count > 0)
            {
                lvDevices.Items[0].Selected = true;
            }
        }

        public void SetTargetDeviceFocus(string deviceId)
        {
            foreach (ListViewItem item in lvDevices.Items)
            {
                if (item.Text == deviceId)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        private void LvDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 인라인 편집 창이 제거되어 선택 바인딩 동작 없음
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (lvDevices.SelectedItems.Count == 0)
            {
                MessageBox.Show("수정하려는 기기를 목록에서 선택하여 주십시오.", "선택 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selId = lvDevices.SelectedItems[0].Text;
            DeviceItem dev = tempDevices.Find(d => d.Id == selId);
            if (dev == null) return;

            using (DeviceEditForm editForm = new DeviceEditForm(dev, tempDevices, tempSpaces))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    var edited = editForm.EditedDevice;
                    if (edited != null)
                    {
                        // 기존 ID가 바뀐 경우, 다른 기기들의 AssociatedDeviceId 도 변경해 주어야 함
                        if (dev.Id != edited.Id)
                        {
                            foreach (var d in tempDevices)
                            {
                                if (d.AssociatedDeviceId == dev.Id) d.AssociatedDeviceId = edited.Id;
                            }
                        }

                        dev.Id = edited.Id;
                        dev.Name = edited.Name;
                        dev.Type = edited.Type;
                        dev.IpAddress = edited.IpAddress;
                        dev.MacAddress = edited.MacAddress;
                        dev.Port = edited.Port;
                        dev.Space = edited.Space;
                        dev.AssociatedDeviceId = edited.AssociatedDeviceId;
                        dev.BootOrder = edited.BootOrder;
                        dev.BootDelaySeconds = edited.BootDelaySeconds;
                        dev.Description = edited.Description;

                        LoadDeviceList();
                        SetTargetDeviceFocus(edited.Id);
                        MessageBox.Show(string.Format("장치 '{0}'의 정보가 임시 수정되었습니다.\n하단의 '전체 변경사항 저장 및 닫기'를 누르면 영구 적용됩니다.", edited.Id), "수정 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void BtnSpaceAdd_Click(object sender, EventArgs e)
        {
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
                    if (tempSpaces.Contains(newSpace))
                    {
                        MessageBox.Show("이미 존재하는 공간 이름입니다.", "중복 경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    tempSpaces.Add(newSpace);
                    RefreshSpaceComboItems();
                    cbSpace.SelectedItem = newSpace;
                    MessageBox.Show(string.Format("공간 '{0}'이 성공적으로 임시 등록되었습니다.\n저장 및 닫기를 누르면 완전 저장됩니다.", newSpace), "공간 추가 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnSpaceDel_Click(object sender, EventArgs e)
        {
            if (cbSpace.SelectedItem == null) return;
            string targetSpace = cbSpace.SelectedItem.ToString();

            List<DeviceItem> affected = tempDevices.FindAll(d => d.Space == targetSpace);
            if (affected.Count > 0)
            {
                var result = MessageBox.Show(
                    string.Format("현재 공간 '{0}'을 소속으로 사용하는 기기가 {1}대 있습니다.\n정말 이 공간을 삭제하시겠습니까?\n(삭제 시 해당 기기들의 소속 공간은 기본값으로 변경됩니다.)", targetSpace, affected.Count), 
                    "공간 강제 삭제 경고", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes) return;

                string fallbackSpace = tempSpaces.Find(s => s != targetSpace);
                if (string.IsNullOrEmpty(fallbackSpace)) fallbackSpace = "미지정";
                
                if (!tempSpaces.Contains(fallbackSpace)) tempSpaces.Add(fallbackSpace);

                foreach (var dev in affected)
                {
                    dev.Space = fallbackSpace;
                }
            }

            tempSpaces.Remove(targetSpace);
            RefreshSpaceComboItems();
            LoadDeviceList();
            MessageBox.Show(string.Format("공간 '{0}'이 목록에서 제외되었습니다.", targetSpace), "삭제 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (DeviceAddForm addForm = new DeviceAddForm(tempDevices, tempSpaces))
            {
                if (addForm.ShowDialog() == DialogResult.OK)
                {
                    var newDev = addForm.AddedDevice;
                    if (newDev != null)
                    {
                        tempDevices.Add(newDev);
                        LoadDeviceList();
                        SetTargetDeviceFocus(newDev.Id);
                        MessageBox.Show(string.Format("신규 장치 '{0}'(이름: {1})가 성공적으로 목록에 임시 추가되었습니다.\n'전체 변경사항 저장 및 닫기'를 누르면 영구 기록됩니다.", newDev.Id, newDev.Name), "추가 등록 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            List<string> deleteTargetIds = new List<string>();

            foreach (ListViewItem item in lvDevices.CheckedItems)
            {
                deleteTargetIds.Add(item.Text);
            }

            if (deleteTargetIds.Count == 0 && lvDevices.SelectedItems.Count > 0)
            {
                deleteTargetIds.Add(lvDevices.SelectedItems[0].Text);
            }

            if (deleteTargetIds.Count == 0)
            {
                MessageBox.Show("삭제하려는 기기의 왼쪽 체크박스를 복수 체크하거나 행을 선택하여 주십시오.", "삭제 지정 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                string.Format("선택하신 {0}개의 장치를 리스트에서 일괄 제외하시겠습니까?\n(연동 고리 및 세트 관계도 안전하게 함께 정리됩니다.)", deleteTargetIds.Count),
                "복수 장치 일괄 삭제 경고",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult == DialogResult.Yes)
            {
                foreach (string id in deleteTargetIds)
                {
                    foreach (var d in tempDevices)
                    {
                        if (d.AssociatedDeviceId == id) d.AssociatedDeviceId = "";
                    }
                    tempDevices.RemoveAll(d => d.Id == id);
                }

                LoadDeviceList();
                MessageBox.Show(string.Format("총 {0}개의 기기가 목록에서 제외되었습니다.\n변경사항을 유지하려면 최종 '전체 변경사항 저장'을 수행하십시오.", deleteTargetIds.Count), "임시 삭제 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }



        private void NormalizeAssociatedIds()
        {
            foreach (var dev in tempDevices)
            {
                if (dev.Type == "PC" && !string.IsNullOrEmpty(dev.AssociatedDeviceId))
                {
                    string targetProjId = dev.AssociatedDeviceId;
                    var projDev = tempDevices.Find(d => d.Id == targetProjId && d.Type == "Projector");
                    if (projDev != null)
                    {
                        projDev.AssociatedDeviceId = dev.Id; 
                    }
                    dev.AssociatedDeviceId = ""; 
                }
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            NormalizeAssociatedIds();

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
            try
            {
                DeviceConfig wrap = new DeviceConfig() 
                { 
                    Spaces = tempSpaces,
                    Devices = tempDevices,
                    Schedules = mainForm.CurrentScheduleSettings
                };
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string jsonText = serializer.Serialize(wrap);

                File.WriteAllText(configPath, jsonText, Encoding.UTF8);
                mainForm.ApplyUpdatedDevices(tempDevices, tempSpaces);

                MessageBox.Show("성공적으로 devices.json에 저장되었습니다.\n대시보드가 즉시 갱신됩니다.", "저장 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // --- 10. 마스터 폼 ---
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
        
        private Button configBtn;

        private Panel schedulePanel;
        private CheckBox autoScheduleCheckBox;
        private Label nextScheduleLabel;
        private Label virtualDayLabel;
        private ComboBox virtualDayComboBox;
        private Button triggerScheduleTestBtn;
        private Button scheduleConfigBtn; 

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
        private TextBox logTextBox;

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
        public List<string> CurrentSpaces { get { return spaces; } }
        public ScheduleSettings CurrentScheduleSettings { get { return scheduleSettings; } }
        public bool IsRealNetworkControlMode { get { return true; } }

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

            LoadDevicesConfig();
            InitializeComponent();
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
            titlePanel.Location = new Point(0, 0);
            titlePanel.Size = new Size(1200, 60);
            titlePanel.BackColor = titleBarBg;
            titlePanel.MouseDown += TitlePanel_MouseDown;
            titlePanel.MouseMove += TitlePanel_MouseMove;
            titlePanel.MouseUp += TitlePanel_MouseUp;

            titleLabel = new Label();
            titleLabel.Text = "⚡   시연실 통합 전원 제어반   |   [정식 운영 빌드 v1.0.0]";
            titleLabel.ForeColor = Color.FromArgb(245, 158, 11);
            titleLabel.Font = new Font("Malgun Gothic", 12f, FontStyle.Bold);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(20, 18);
            titleLabel.MouseDown += TitlePanel_MouseDown;
            titleLabel.MouseMove += TitlePanel_MouseMove;
            titleLabel.MouseUp += TitlePanel_MouseUp;

            statusSummaryLabel = new Label();
            statusSummaryLabel.Text = "가동 상황: 0대 실행 중 / 총 0대";
            statusSummaryLabel.ForeColor = textGray;
            statusSummaryLabel.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            statusSummaryLabel.AutoSize = true;
            statusSummaryLabel.Location = new Point(580, 22);

            btnClose = new Button();
            btnClose.Text = "✕";
            btnClose.Font = new Font("Malgun Gothic", 11f, FontStyle.Bold);
            btnClose.ForeColor = textGray;
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 68, 68);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(185, 28, 28);
            btnClose.Size = new Size(45, 60);
            btnClose.Location = new Point(1155, 0);
            btnClose.Click += (s, e) => this.Close();

            btnMinimize = new Button();
            btnMinimize.Text = "—";
            btnMinimize.Font = new Font("Malgun Gothic", 10f, FontStyle.Bold);
            btnMinimize.ForeColor = textGray;
            btnMinimize.BackColor = Color.Transparent;
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 255, 255, 10);
            btnMinimize.Size = new Size(45, 60);
            btnMinimize.Location = new Point(1110, 0);
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            titlePanel.Controls.Add(titleLabel);
            titlePanel.Controls.Add(statusSummaryLabel);
            titlePanel.Controls.Add(btnClose);
            titlePanel.Controls.Add(btnMinimize);

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

                    using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(24, 25, 38)))
                    {
                        e.Graphics.FillPath(fillBrush, path);
                    }
                    using (Pen borderPen = new Pen(Color.FromArgb(50, 50, 70), 1f))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }
            };

            autoScheduleCheckBox = new CheckBox();
            autoScheduleCheckBox.Text = "스케줄 자동 제어 활성화 (Auto Scheduler)";
            autoScheduleCheckBox.ForeColor = Color.White;
            autoScheduleCheckBox.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
            autoScheduleCheckBox.Checked = true;
            autoScheduleCheckBox.AutoSize = true;
            autoScheduleCheckBox.Location = new Point(20, 13);
            autoScheduleCheckBox.BackColor = Color.Transparent;
            autoScheduleCheckBox.CheckedChanged += AutoScheduleCheckBox_CheckedChanged;

            nextScheduleLabel = new Label();
            nextScheduleLabel.ForeColor = Color.FromArgb(245, 158, 11);
            nextScheduleLabel.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Regular);
            nextScheduleLabel.AutoSize = true;
            nextScheduleLabel.BackColor = Color.Transparent;
            nextScheduleLabel.Location = new Point(20, 45);

            virtualDayLabel = new Label();
            virtualDayLabel.Text = "가상 요일:";
            virtualDayLabel.ForeColor = Color.White;
            virtualDayLabel.Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            virtualDayLabel.AutoSize = true;
            virtualDayLabel.BackColor = Color.Transparent;
            virtualDayLabel.Location = new Point(670, 17);

            virtualDayComboBox = new ComboBox();
            virtualDayComboBox.Size = new Size(110, 25);
            virtualDayComboBox.Location = new Point(740, 13);
            virtualDayComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            virtualDayComboBox.BackColor = Color.FromArgb(28, 29, 43);
            virtualDayComboBox.ForeColor = Color.White;
            virtualDayComboBox.FlatStyle = FlatStyle.Flat;
            virtualDayComboBox.Font = new Font("Malgun Gothic", 9f);

            scheduleConfigBtn = new Button();
            scheduleConfigBtn.Text = "⏰ 스케줄 설정...";
            scheduleConfigBtn.Size = new Size(110, 28);
            scheduleConfigBtn.Location = new Point(860, 10);
            scheduleConfigBtn.BackColor = Color.FromArgb(35, 37, 54);
            scheduleConfigBtn.ForeColor = Color.White;
            scheduleConfigBtn.FlatStyle = FlatStyle.Flat;
            scheduleConfigBtn.FlatAppearance.BorderSize = 1;
            scheduleConfigBtn.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 70);
            scheduleConfigBtn.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
            scheduleConfigBtn.Cursor = Cursors.Hand;
            scheduleConfigBtn.Click += ScheduleConfigBtn_Click;

            triggerScheduleTestBtn = new Button();
            triggerScheduleTestBtn.Text = "⚡ 가상 예약 트리거";
            triggerScheduleTestBtn.Size = new Size(160, 30);
            triggerScheduleTestBtn.Location = new Point(980, 10);
            triggerScheduleTestBtn.BackColor = Color.FromArgb(35, 37, 54);
            triggerScheduleTestBtn.ForeColor = Color.White;
            triggerScheduleTestBtn.FlatStyle = FlatStyle.Flat;
            triggerScheduleTestBtn.FlatAppearance.BorderSize = 1;
            triggerScheduleTestBtn.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 70);
            triggerScheduleTestBtn.Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            triggerScheduleTestBtn.Cursor = Cursors.Hand;
            triggerScheduleTestBtn.Click += TriggerScheduleTestBtn_Click;

            schedulePanel.Controls.Add(autoScheduleCheckBox);
            schedulePanel.Controls.Add(nextScheduleLabel);
            schedulePanel.Controls.Add(virtualDayLabel);
            schedulePanel.Controls.Add(virtualDayComboBox);
            schedulePanel.Controls.Add(scheduleConfigBtn);
            schedulePanel.Controls.Add(triggerScheduleTestBtn);

            // 3. 공간별 통합 제어반 (DoubleBufferedPanel을 활용한 꽉 찬 박스 디자인)
            zoneControlGroup = new DoubleBufferedPanel();
            zoneControlGroup.Size = new Size(1152, 120);
            zoneControlGroup.Location = new Point(24, 168);
            zoneControlGroup.Paint += (s, e) =>
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(24, 25, 38)))
                using (Pen borderPen = new Pen(Color.FromArgb(50, 52, 74), 1.5f))
                {
                    e.Graphics.FillRectangle(bgBrush, 0, 0, zoneControlGroup.Width, zoneControlGroup.Height);
                    e.Graphics.DrawRectangle(borderPen, 0, 0, zoneControlGroup.Width - 1, zoneControlGroup.Height - 1);
                }
            };

            Label zoneTitleLabel = new Label();
            zoneTitleLabel.Text = "공간별 통합 제어반 (Tab Zone Control)";
            zoneTitleLabel.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
            zoneTitleLabel.ForeColor = Color.FromArgb(168, 162, 235);
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
            addSpaceBtn.FlatAppearance.BorderColor = Color.FromArgb(123, 97, 255);
            addSpaceBtn.BackColor = Color.FromArgb(24, 25, 38);
            addSpaceBtn.ForeColor = Color.FromArgb(123, 97, 255);
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
            deleteSpaceBtn.FlatAppearance.BorderColor = Color.FromArgb(239, 68, 68);
            deleteSpaceBtn.BackColor = Color.FromArgb(24, 25, 38);
            deleteSpaceBtn.ForeColor = Color.FromArgb(239, 68, 68);
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
            zoneOnBtn.BackColor = Color.FromArgb(123, 97, 255);
            zoneOnBtn.ForeColor = Color.White;
            zoneOnBtn.FlatStyle = FlatStyle.Flat;
            zoneOnBtn.FlatAppearance.BorderSize = 0;
            zoneOnBtn.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
            zoneOnBtn.Cursor = Cursors.Hand;
            zoneOnBtn.Click += ZoneOnBtn_Click;

            zoneOffBtn = new Button();
            zoneOffBtn.Text = "⏹️  전체 끄기";
            zoneOffBtn.Size = new Size(240, 52);
            zoneOffBtn.Location = new Point(880, 40);
            zoneOffBtn.BackColor = Color.FromArgb(239, 68, 68);
            zoneOffBtn.ForeColor = Color.White;
            zoneOffBtn.FlatStyle = FlatStyle.Flat;
            zoneOffBtn.FlatAppearance.BorderSize = 0;
            zoneOffBtn.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
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
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(24, 25, 38)))
                using (Pen borderPen = new Pen(Color.FromArgb(50, 52, 74), 1.5f))
                {
                    e.Graphics.FillRectangle(bgBrush, 0, 0, dashboardGroup.Width, dashboardGroup.Height);
                    e.Graphics.DrawRectangle(borderPen, 0, 0, dashboardGroup.Width - 1, dashboardGroup.Height - 1);
                }
            };

            Label dashTitleLabel = new Label();
            dashTitleLabel.Text = "3. 장비 현황 모니터 (비주얼 카드 뷰)";
            dashTitleLabel.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
            dashTitleLabel.ForeColor = Color.FromArgb(168, 162, 235);
            dashTitleLabel.BackColor = Color.Transparent;
            dashTitleLabel.AutoSize = true;
            dashTitleLabel.Location = new Point(20, 15);
            dashboardGroup.Controls.Add(dashTitleLabel);

            configBtn = new Button();
            configBtn.Text = "⚙️ 장치 편집(설정)";
            configBtn.Size = new Size(140, 22);
            configBtn.Location = new Point(290, 13);
            configBtn.BackColor = buttonBaseBg;
            configBtn.ForeColor = Color.White;
            configBtn.FlatStyle = FlatStyle.Flat;
            configBtn.FlatAppearance.BorderSize = 0;
            configBtn.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
            configBtn.Cursor = Cursors.Hand;
            configBtn.Click += ConfigBtn_Click;

            cardContainerPanel = new FlowLayoutPanel();
            cardContainerPanel.Size = new Size(1112, 380);
            cardContainerPanel.Location = new Point(20, 48);
            cardContainerPanel.AutoScroll = true;
            cardContainerPanel.BackColor = Color.FromArgb(18, 19, 28);
            cardContainerPanel.FlowDirection = FlowDirection.LeftToRight;
            cardContainerPanel.WrapContents = true;
            cardContainerPanel.Padding = new Padding(10);

            dashboardGroup.Controls.Add(configBtn);
            dashboardGroup.Controls.Add(cardContainerPanel);

            // 5. Simulation Log Group
            logGroup = new DoubleBufferedPanel();
            logGroup.Paint += (s, e) =>
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(24, 25, 38)))
                using (Pen borderPen = new Pen(Color.FromArgb(50, 52, 74), 1.5f))
                {
                    e.Graphics.FillRectangle(bgBrush, 0, 0, logGroup.Width, logGroup.Height);
                    e.Graphics.DrawRectangle(borderPen, 0, 0, logGroup.Width - 1, logGroup.Height - 1);
                }
            };

            Label logTitleLabel = new Label();
            logTitleLabel.Text = "실시간 로그";
            logTitleLabel.Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold);
            logTitleLabel.ForeColor = Color.FromArgb(168, 162, 235);
            logTitleLabel.BackColor = Color.Transparent;
            logTitleLabel.AutoSize = true;
            logTitleLabel.Location = new Point(20, 15);
            logGroup.Controls.Add(logTitleLabel);
            logGroup.Size = new Size(1152, 165);
            logGroup.Location = new Point(24, 760);


            logTextBox = new TextBox();
            logTextBox.Text = "";
            logTextBox.Multiline = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(1112, 110);
            logTextBox.Location = new Point(20, 42);
            logTextBox.BackColor = Color.FromArgb(18, 19, 28);
            logTextBox.ForeColor = Color.FromArgb(243, 244, 246);
            logTextBox.BorderStyle = BorderStyle.None;
            logTextBox.Font = new Font("Consolas", 9f);
            logTextBox.ReadOnly = true;

            logGroup.Controls.Add(logTextBox);

            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(35, 37, 54), 1.5f))
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
                nextScheduleLabel.ForeColor = Color.FromArgb(245, 158, 11);
            }
            else
            {
                nextScheduleLabel.Text = "⏰ 자동 예약 대기 중지 (연장 점검/운영 모드)";
                nextScheduleLabel.ForeColor = Color.FromArgb(239, 68, 68);
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

        private void AppendLog(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action<string>(AppendLog), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            logTextBox.AppendText(string.Format("[{0}] {1}\r\n", timestamp, message));
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToCaret();
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
            btn.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Bold);
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
                    btn.BackColor = Color.FromArgb(123, 97, 255);
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(123, 97, 255);
                }
                else
                {
                    btn.BackColor = Color.FromArgb(24, 25, 38);
                    btn.ForeColor = Color.FromArgb(156, 163, 175);
                    btn.FlatAppearance.BorderColor = Color.FromArgb(50, 52, 74);
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
            zoneOnBtn.Enabled = enable;
            zoneOffBtn.Enabled = enable;
            configBtn.Enabled = enable;
            autoScheduleCheckBox.Enabled = enable;
            triggerScheduleTestBtn.Enabled = enable;
            virtualDayComboBox.Enabled = enable;
            scheduleConfigBtn.Enabled = enable;
            
            foreach (Control ctrl in tabFlowPanel.Controls)
            {
                Button btn = ctrl as Button;
                if (btn != null) btn.Enabled = enable;
            }
        }


        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PowerControllerForm());
        }
    }
}
