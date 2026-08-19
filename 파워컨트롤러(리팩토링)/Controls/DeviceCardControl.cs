using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShowroomPowerController
{
    public class DeviceCardControl : UserControl
    {
        private DeviceItem device;                  
        private List<DeviceItem> linkedDevices;     
        private PowerControllerForm mainForm;

        private static Random rnd = new Random();
        private int animPhaseOffset;
        private System.Windows.Forms.Timer animTimer;
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
            this.animPhaseOffset = rnd.Next(0, 20);

            this.Size = new Size(225, 215);

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
            btnOn.Size = new Size(96, 28);
            btnOn.Location = new Point(12, 175);
            btnOn.FlatStyle = FlatStyle.Flat;
            btnOn.FlatAppearance.BorderSize = 0;
            btnOn.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnOn.Cursor = Cursors.Hand;
            btnOn.Click += BtnOn_Click;

            btnOff = new Button();
            btnOff.Text = "OFF";
            btnOff.Size = new Size(96, 28);
            btnOff.Location = new Point(117, 175);
            btnOff.FlatStyle = FlatStyle.Flat;
            btnOff.FlatAppearance.BorderSize = 0;
            btnOff.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnOff.Cursor = Cursors.Hand;
            btnOff.Click += BtnOff_Click;

            this.Controls.Add(btnOn);
            this.Controls.Add(btnOff);

            btnEdit = new Button();
            btnEdit.Text = "⚙️";
            btnEdit.Size = new Size(24, 24);
            btnEdit.Location = new Point(188, 12);
            btnEdit.BackColor = Color.Transparent;
            btnEdit.ForeColor = ThemeManager.MutedTextColor;
            btnEdit.FlatStyle = FlatStyle.Flat;
            btnEdit.FlatAppearance.BorderSize = 0;
            btnEdit.Cursor = Cursors.Hand;
            btnEdit.Font = FontHelper.GetFont(9f, FontStyle.Bold);
            btnEdit.Click += BtnEdit_Click;

            btnEdit.MouseEnter += (s, ev) => btnEdit.ForeColor = ColorTranslator.FromHtml("#f54e00");
            btnEdit.MouseLeave += (s, ev) => btnEdit.ForeColor = ThemeManager.MutedTextColor;

            this.Controls.Add(btnEdit);
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            animTickCount++;

            UpdateButtonStates();

            bool needsRedraw = (device.RuntimeStatus == "ONLINE" || device.RuntimeStatus == "FREEZE" || device.RuntimeStatus == "BOOTING" || device.RuntimeStatus == "COOLING");
            if (!needsRedraw)
            {
                foreach (var sub in linkedDevices)
                {
                    if (sub.RuntimeStatus == "BOOTING" || sub.RuntimeStatus == "COOLING" || sub.RuntimeStatus == "ONLINE" || sub.RuntimeStatus == "FREEZE")
                    {
                        needsRedraw = true;
                        break;
                    }
                }
            }

            if (needsRedraw)
            {
                this.Invalidate();
            }
        }

        private void UpdateButtonStates()
        {
            if (mainForm.IsRunningSimulation)
            {
                ThemeManager.SetControlEnabledState(btnOn, false, ThemeManager.IsDark);
                ThemeManager.SetControlEnabledState(btnOff, false, ThemeManager.IsDark);
                ThemeManager.SetControlEnabledState(btnEdit, false, ThemeManager.IsDark);
                return;
            }

            ThemeManager.SetControlEnabledState(btnEdit, true, ThemeManager.IsDark);

            bool cardIsBusy = (device.RuntimeStatus == "BOOTING" || device.RuntimeStatus == "COOLING");
            foreach (var sub in linkedDevices)
            {
                if (sub.RuntimeStatus == "BOOTING" || sub.RuntimeStatus == "COOLING") cardIsBusy = true;
            }

            if (cardIsBusy)
            {
                ThemeManager.SetControlEnabledState(btnOn, false, ThemeManager.IsDark);
                ThemeManager.SetControlEnabledState(btnOff, false, ThemeManager.IsDark);
            }
            else
            {
                UpdateButtonState(btnOn, btnOff, device.RuntimeStatus);
            }
        }

        private void UpdateButtonState(Button onBtn, Button offBtn, string status)
        {
            if (status == "BOOTING" || status == "COOLING")
            {
                ThemeManager.SetControlEnabledState(onBtn, false, ThemeManager.IsDark);
                ThemeManager.SetControlEnabledState(offBtn, false, ThemeManager.IsDark);
            }
            else if (status == "ONLINE")
            {
                ThemeManager.SetControlEnabledState(onBtn, false, ThemeManager.IsDark);
                ThemeManager.SetControlEnabledState(offBtn, true, ThemeManager.IsDark);
            }
            else if (status == "OFFLINE")
            {
                ThemeManager.SetControlEnabledState(onBtn, true, ThemeManager.IsDark);
                ThemeManager.SetControlEnabledState(offBtn, false, ThemeManager.IsDark);
            }
            else 
            {
                ThemeManager.SetControlEnabledState(onBtn, true, ThemeManager.IsDark);
                ThemeManager.SetControlEnabledState(offBtn, true, ThemeManager.IsDark);
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

            Color cardBg = ThemeManager.CardBgColor;
            Color borderColor = ThemeManager.BorderColor;
            float borderWidth = 1.5f;

            if (device.RuntimeStatus == "ONLINE")
            {
                cardBg = ThemeManager.CardBgColor;
                
                if (device.ContentState == "콘텐츠구동중")
                {
                    borderColor = Color.FromArgb(255, ColorTranslator.FromHtml("#1f8a65"));
                }
                else
                {
                    double angle = ((animTickCount + animPhaseOffset) % 20) / 20.0 * 2.0 * Math.PI;
                    int pulseAlpha = (int)(160 + 95 * Math.Sin(angle));
                    if (pulseAlpha < 80) pulseAlpha = 80;
                    if (pulseAlpha > 255) pulseAlpha = 255;
                    borderColor = Color.FromArgb(pulseAlpha, ColorTranslator.FromHtml("#1f8a65"));
                }
                borderWidth = 2.0f;
            }
            else if (device.RuntimeStatus == "OFFLINE")
            {
                cardBg = ThemeManager.IsDark ? Color.FromArgb(20, 21, 31) : Color.FromArgb(245, 245, 240);
                borderColor = ThemeManager.BorderColorSoft;
                borderWidth = 1.0f;
            }
            else if (device.RuntimeStatus == "FREEZE")
            {
                cardBg = ThemeManager.CardBgColor;
                bool blinkState = ((animTickCount + animPhaseOffset) / 5) % 2 == 0;
                borderColor = blinkState ? ColorTranslator.FromHtml("#cf2d56") : Color.FromArgb(70, 20, 25);
                borderWidth = 2.5f;
            }
            else if (device.RuntimeStatus == "BOOTING")
            {
                cardBg = ThemeManager.CardBgColor;
                borderColor = ColorTranslator.FromHtml("#f54e00");
                borderWidth = 2.0f;
            }
            else if (device.RuntimeStatus == "COOLING")
            {
                cardBg = ThemeManager.CardBgColor;
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
                using (Pen sepPen = new Pen(ThemeManager.BorderColorSoft, 1f))
                {
                    g.DrawLine(sepPen, 15, 110, 210, 110);
                }

                for (int i = 0; i < linkedDevices.Count; i++)
                {
                    if (i >= 2) break; 

                    var proj = linkedDevices[i];
                    int x = 15 + i * 100;
                    int y = 122;

                    using (Pen p = new Pen(ThemeManager.MutedTextColor, 1.2f))
                    {
                        g.DrawRectangle(p, x, y + 2, 9, 6);
                        g.DrawEllipse(p, x + 8, y + 3, 4, 4);
                    }

                    Color dotColor = ThemeManager.MutedTextColor;
                    string stateName = "OFF";

                    if (proj.RuntimeStatus == "ONLINE") 
                    { 
                        dotColor = Color.FromArgb(16, 185, 129); 
                        stateName = "ON"; 
                    }
                    else if (proj.RuntimeStatus == "BOOTING") 
                    { 
                        dotColor = ColorTranslator.FromHtml("#f54e00"); 
                        stateName = string.Format("ON({0}s)", proj.RemainingSeconds); 
                    }
                    else if (proj.RuntimeStatus == "COOLING") 
                    { 
                        dotColor = Color.FromArgb(200, 100, 30); 
                        stateName = string.Format("COOL({0}s)", proj.RemainingSeconds); 
                    }
                    else if (proj.RuntimeStatus == "FREEZE") 
                    { 
                        dotColor = ColorTranslator.FromHtml("#cf2d56"); 
                        stateName = "ERR"; 
                    }

                    using (SolidBrush dotBrush = new SolidBrush(dotColor))
                    {
                        g.FillEllipse(dotBrush, x + 16, y + 4, 5, 5);
                    }

                    using (Font projFont = FontHelper.GetFont(7.5f, FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(ThemeManager.MutedTextColor))
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
            using (SolidBrush iconBgBrush = new SolidBrush(ThemeManager.IsDark ? Color.FromArgb(35, 37, 54) : Color.FromArgb(235, 235, 230)))
            {
                g.FillPath(iconBgBrush, iconPath);
            }

            int cx = xOffset + 15 + 26; 
            int cy = 15 + 26;           

            if (dev.RuntimeStatus == "BOOTING" || dev.RuntimeStatus == "COOLING")
            {
                string countStr = dev.RemainingSeconds.ToString();
                using (Font numFont = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (SolidBrush numBrush = new SolidBrush(ColorTranslator.FromHtml("#f54e00"))) 
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
                    using (Pen p = new Pen(ThemeManager.TextColor, 2f))
                    {
                        g.DrawRectangle(p, cx - 16, cy - 13, 32, 20);
                        g.DrawLine(p, cx, cy + 7, cx, cy + 12);
                        g.DrawLine(p, cx - 8, cy + 12, cx + 8, cy + 12);
                    }
                }
                else 
                {
                    using (Pen p = new Pen(ThemeManager.TextColor, 2f))
                    {
                        g.DrawRectangle(p, cx - 17, cy - 9, 25, 17);
                        g.DrawEllipse(p, cx + 8, cy - 6, 10, 10);
                        g.DrawLine(p, cx + 8, cy - 1, cx + 8, cy + 4);
                        g.DrawLine(p, cx - 10, cy + 8, cx - 12, cy + 12);
                        g.DrawLine(p, cx + 2, cy + 8, cx + 4, cy + 12);
                    }
                }
            }

            Font nameFont = FontHelper.GetFont(9.5f, FontStyle.Bold);
            Font infoFont = FontHelper.GetFont(8f, FontStyle.Regular);
            Brush textBrush = dev.RuntimeStatus == "OFFLINE" ? new SolidBrush(ThemeManager.MutedTextColor) : new SolidBrush(ThemeManager.TextColor);

            RectangleF nameRect = new RectangleF(xOffset + 78, 16, 110, 18);
            StringFormat sfName = new StringFormat() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(dev.Name, nameFont, textBrush, nameRect, sfName);

            g.DrawString("IP: " + dev.IpAddress, infoFont, new SolidBrush(ThemeManager.MutedTextColor), new PointF(xOffset + 78, 35));

            string modeTag = "";
            if (dev.Type == "PC")
            {
                if (dev.PowerOnSequenceMode == "PROJ_FIRST") modeTag = " (프로젝터 우선)";
                else if (dev.PowerOnSequenceMode == "SIMULTANEOUS") modeTag = " (동시 가동)";
                else modeTag = " (PC 우선)";
            }

            string spaceTypeStr = dev.Type == "PC" ? "PC" : "프로젝터";
            string spaceTag = string.IsNullOrEmpty(dev.Space) 
                ? string.Format("[{0}]{1}", spaceTypeStr, modeTag) 
                : string.Format("📍 {0} · {1}{2}", dev.Space, spaceTypeStr, modeTag);

            RectangleF tagRect = new RectangleF(xOffset + 78, 51, 145, 16);
            g.DrawString(spaceTag, infoFont, new SolidBrush(ThemeManager.MutedTextColor), tagRect, sfName);

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

                Color progressColor = dev.RuntimeStatus == "BOOTING" ? ColorTranslator.FromHtml("#f54e00") : Color.FromArgb(200, 100, 30);
                using (Pen progressRingPen = new Pen(progressColor, 3))
                {
                    progressRingPen.StartCap = LineCap.Round;
                    progressRingPen.EndCap = LineCap.Round;
                    g.DrawArc(progressRingPen, ringRect, -90, sweepAngle);
                }
            }

            string statusStr = "OFFLINE (대기 중)";
            Color statusColor = ThemeManager.MutedTextColor;

            if (dev.RuntimeStatus == "ONLINE")
            {
                statusStr = string.IsNullOrEmpty(dev.ContentState) ? "ONLINE (실행 중)" : dev.ContentState;
                statusColor = Color.FromArgb(16, 185, 129);
            }
            else if (dev.RuntimeStatus == "BOOTING")
            {
                statusStr = "BOOTING (부팅 중)";
                statusColor = ColorTranslator.FromHtml("#f54e00");
            }
            else if (dev.RuntimeStatus == "COOLING")
            {
                statusStr = "COOLING (식히는 중)";
                statusColor = Color.FromArgb(200, 100, 30);
            }
            else if (dev.RuntimeStatus == "FREEZE")
            {
                statusStr = "FREEZE (신호 끊김)";
                statusColor = ColorTranslator.FromHtml("#cf2d56");
            }

            using (SolidBrush dotBrush = new SolidBrush(statusColor))
            {
                g.FillEllipse(dotBrush, xOffset + 18, 85, 6, 6);
            }

            using (Font fontStatus = FontHelper.GetFont(8f, FontStyle.Bold))
            using (SolidBrush brushStatus = new SolidBrush(statusColor))
            {
                g.DrawString(statusStr, fontStatus, brushStatus, new PointF(xOffset + 28, 81));
            }
        }

        private async Task RunIndividualPowerOn(DeviceItem targetDev)
        {
            if (targetDev.RuntimeStatus == "ONLINE" || mainForm.IsRunningSimulation) return;

            mainForm.LogMessage(string.Format("====== [{0} 비동기 개별 기동 시작] ======", targetDev.Name));

            bool isProjFirst = (targetDev.Type == "PC" && targetDev.PowerOnSequenceMode == "PROJ_FIRST");
            bool isSimultaneous = (targetDev.Type == "PC" && targetDev.PowerOnSequenceMode == "SIMULTANEOUS");

            if (isProjFirst && linkedDevices.Count > 0)
            {
                // [모드 1] 프로젝터 우선 가동 (1단계: 프로젝터 ➡️ 2단계: PC)
                mainForm.LogMessage("▶ [1단계: 프로젝터 예열] 귀속 빔 프로젝터 예열을 먼저 개시합니다.");
                
                int maxProjDelay = 0;
                foreach (var proj in linkedDevices)
                {
                    proj.RuntimeStatus = "BOOTING";
                    int dly = proj.BootDelaySeconds > 0 ? proj.BootDelaySeconds : 10;
                    proj.RemainingSeconds = dly;
                    
                    if (dly > maxProjDelay) maxProjDelay = dly;
                    mainForm.LogMessage(string.Format("   └🔗 {0} 예열 개시 (대기: {1}초)", proj.Name, dly));

                    if (mainForm.IsRealNetworkControlMode)
                    {
                        mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] PJLink ON 송출 -> IP: {0}:{1}", proj.IpAddress, proj.Port));
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
                    mainForm.LogMessage(string.Format("   └🔗 {0} 예열 완료 ➡️ 투사 준비(ONLINE)", proj.Name));
                }
                mainForm.UpdateVisualDashboard();

                // 2단계: PC 기동
                mainForm.LogMessage(string.Format("▶ [2단계: PC 부팅] 부모 제어 장비 {0}의 기동 전원(WOL)을 송신합니다.", targetDev.Name));
                mainForm.AddWolRetryQueue(targetDev.Id);
                if (mainForm.IsRealNetworkControlMode)
                {
                    mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] WOL Magic Packet 전송 -> MAC: {0}", targetDev.MacAddress));
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
            else if (isSimultaneous && linkedDevices.Count > 0)
            {
                // [모드 3] 동시 가동: PC와 프로젝터를 동시에 즉시 켬
                mainForm.LogMessage(string.Format("▶ [동시 가동] {0} 본체(WOL) 및 귀속 빔 프로젝터 전원 신호를 동시에 송출합니다.", targetDev.Name));
                
                int maxDelay = 10;
                foreach (var proj in linkedDevices)
                {
                    proj.RuntimeStatus = "BOOTING";
                    int dly = proj.BootDelaySeconds > 0 ? proj.BootDelaySeconds : 10;
                    proj.RemainingSeconds = dly;
                    if (dly > maxDelay) maxDelay = dly;

                    if (mainForm.IsRealNetworkControlMode)
                    {
                        #pragma warning disable 4014
                        mainForm.SendPJLinkCommandAsync(proj.IpAddress, proj.Port, "%1POWR 1");
                        #pragma warning restore 4014
                    }
                }

                mainForm.AddWolRetryQueue(targetDev.Id);
                if (mainForm.IsRealNetworkControlMode)
                {
                    mainForm.SendWOLMagicPacket(targetDev.MacAddress);
                }

                targetDev.RuntimeStatus = "BOOTING";
                targetDev.RemainingSeconds = maxDelay;
                mainForm.UpdateVisualDashboard();

                for (int i = maxDelay; i >= 0; i--)
                {
                    targetDev.RemainingSeconds = i;
                    foreach (var proj in linkedDevices)
                    {
                        if (proj.RemainingSeconds > 0) proj.RemainingSeconds = i;
                    }
                    mainForm.UpdateVisualDashboard();
                    if (i > 0) await Task.Delay(1000);
                }

                targetDev.RuntimeStatus = "ONLINE";
                targetDev.RemainingSeconds = 0;
                foreach (var proj in linkedDevices)
                {
                    proj.RuntimeStatus = "ONLINE";
                    proj.RemainingSeconds = 0;
                }
            }
            else
            {
                // [모드 2] PC 우선 가동 (1단계: PC 부팅 ➡️ 2단계: 프로젝터 켬) 또는 단독 기기
                if (targetDev.Type == "PC")
                {
                    mainForm.LogMessage(string.Format("▶ [1단계: PC 부팅] 부모 제어 장비 {0}의 기동 전원(WOL)을 송신합니다.", targetDev.Name));
                    mainForm.AddWolRetryQueue(targetDev.Id);
                    if (mainForm.IsRealNetworkControlMode)
                    {
                        mainForm.LogMessage(string.Format("   ⚡ [실장비 통신] WOL Magic Packet 전송 -> MAC: {0}", targetDev.MacAddress));
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

                    // 2단계: 연동된 프로젝터 기동
                    if (linkedDevices.Count > 0)
                    {
                        mainForm.LogMessage("▶ [2단계: 프로젝터 켜기] 귀속 빔 프로젝터의 전원(PJLink)을 송출합니다.");
                        int maxProjDelay = 0;
                        foreach (var proj in linkedDevices)
                        {
                            proj.RuntimeStatus = "BOOTING";
                            int dly = proj.BootDelaySeconds > 0 ? proj.BootDelaySeconds : 10;
                            proj.RemainingSeconds = dly;
                            if (dly > maxProjDelay) maxProjDelay = dly;

                            if (mainForm.IsRealNetworkControlMode)
                            {
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
                        mainForm.UpdateVisualDashboard();
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
}
