using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShowroomPowerController
{
    public class ToastForm : Form
    {
        private Timer lifeTimer;
        private int tickCount = 0;
        private readonly string deviceId;
        private readonly string ip;
        private readonly bool isRegistered;
        private readonly Action<string, string> onRegisterClick;

        private const int FadeInTicks = 6;      // ~180ms
        private const int StayTicks = 100;      // ~3000ms
        private const int FadeOutTicks = 10;    // ~300ms

        public ToastForm(string message, Color bgColor, string deviceId, string ip, bool isRegistered, Action<string, string> onRegisterClick = null)
        {
            this.deviceId = deviceId;
            this.ip = ip;
            this.isRegistered = isRegistered;
            this.onRegisterClick = onRegisterClick;

            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(360, 74);
            this.Opacity = 1.0;
            this.BackColor = bgColor;
            this.DoubleBuffered = true;

            // Position at bottom-right of primary screen
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(workArea.Right - this.Width - 20, workArea.Bottom - this.Height - 20);

            Panel contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.Transparent;
            contentPanel.Cursor = !isRegistered ? Cursors.Hand : Cursors.Default;
            contentPanel.Click += Toast_Click;

            Label lblMsg = new Label();
            lblMsg.Text = message;
            lblMsg.ForeColor = Color.White;
            lblMsg.Font = FontHelper.GetFont(9.5f, FontStyle.Bold);
            lblMsg.Location = new Point(14, 14);
            lblMsg.Size = new Size(332, 24);
            lblMsg.AutoEllipsis = true;
            lblMsg.BackColor = Color.Transparent;
            lblMsg.Cursor = !isRegistered ? Cursors.Hand : Cursors.Default;
            lblMsg.Click += Toast_Click;
            contentPanel.Controls.Add(lblMsg);

            Label lblSub = new Label();
            lblSub.Text = !isRegistered ? "👉 클릭하여 이 기기를 새 장치로 간편 등록합니다." : "3초 후 자동으로 알림이 닫힙니다.";
            lblSub.ForeColor = Color.FromArgb(235, 240, 250);
            lblSub.Font = FontHelper.GetFont(8.5f, FontStyle.Regular);
            lblSub.Location = new Point(14, 42);
            lblSub.Size = new Size(332, 20);
            lblSub.BackColor = Color.Transparent;
            lblSub.Cursor = !isRegistered ? Cursors.Hand : Cursors.Default;
            lblSub.Click += Toast_Click;
            contentPanel.Controls.Add(lblSub);

            this.Controls.Add(contentPanel);

            this.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(180, 255, 255, 255), 1.2f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            this.Click += Toast_Click;

            lifeTimer = new Timer();
            lifeTimer.Interval = 50;
            lifeTimer.Tick += LifeTimer_Tick;
            lifeTimer.Start();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        private void LifeTimer_Tick(object sender, EventArgs e)
        {
            tickCount++;

            // ~3 seconds stay (60 ticks * 50ms)
            if (tickCount > 60)
            {
                int fadeOutProgress = tickCount - 60;
                double newOpacity = 1.0 - ((double)fadeOutProgress / 10);
                if (newOpacity <= 0.05)
                {
                    lifeTimer.Stop();
                    lifeTimer.Dispose();
                    this.Close();
                    this.Dispose();
                }
                else
                {
                    this.Opacity = newOpacity;
                }
            }
        }

        private void Toast_Click(object sender, EventArgs e)
        {
            if (!isRegistered)
            {
                if (lifeTimer != null)
                {
                    lifeTimer.Stop();
                    lifeTimer.Dispose();
                }
                this.Close();
                this.Dispose();

                if (onRegisterClick != null)
                {
                    onRegisterClick(deviceId, ip);
                }
            }
            else
            {
                if (lifeTimer != null)
                {
                    lifeTimer.Stop();
                    lifeTimer.Dispose();
                }
                this.Close();
                this.Dispose();
            }
        }
    }
}
