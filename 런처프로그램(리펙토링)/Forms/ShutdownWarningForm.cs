using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShowroomLauncher
{
    public class ShutdownWarningForm : Form
    {
        private Label lblMessage;
        private Label lblTimer;
        private Button btnShutdown;
        private Button btnCancel;
        private Timer countdownTimer;
        private int remainingSeconds = 20;

        public ShutdownWarningForm(bool isDarkMode)
        {
            this.Text = "\u26A0\uFE0F \uC2DC\uC2A4\uD15C \uC790\uB3D9 \uC885\uB550 \uC608\uACE0";
            this.Size = new Size(450, 260);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = isDarkMode ? Color.FromArgb(24, 25, 38) : Color.FromArgb(247, 247, 244);

            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(239, 68, 68), 2f))
                {
                    e.Graphics.DrawRectangle(borderPen, 1, 1, this.Width - 2, this.Height - 2);
                }
            };

            Font fontTitle = FontHelper.GetFont(14f, FontStyle.Bold);
            Font fontText = FontHelper.GetFont(10f);
            Font fontTimer = FontHelper.GetFont(28f, FontStyle.Bold);
            Font fontButton = FontHelper.GetFont(9.5f, FontStyle.Bold);

            Label lblTitle = new Label()
            {
                Text = "\u26A0\uFE0F  \uC2DC\uC2A4\uD15C \uC790\uB3D9 \uC885\uB550 \uC608\uACE0",
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
                ForeColor = isDarkMode ? Color.White : Color.FromArgb(38, 37, 30),
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
                BackColor = isDarkMode ? Color.FromArgb(55, 57, 84) : Color.FromArgb(230, 229, 224),
                ForeColor = isDarkMode ? Color.White : Color.FromArgb(38, 37, 30),
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
}
