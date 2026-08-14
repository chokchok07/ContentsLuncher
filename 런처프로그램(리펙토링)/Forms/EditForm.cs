using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShowroomLauncher
{
    public class EditForm : Form
    {
        public ContentItem ResultItem { get; private set; }

        private TextBox txtName;
        private TextBox txtIcon;
        private TextBox txtDesc;
        private TextBox txtPath;
        private TextBox txtModulePath;
        private TextBox txtModuleDelay;
        private Button btnBrowse;
        private Button btnBrowseModule;
        private Button btnModuleOn;
        private Button btnModuleOff;
        private bool isModuleEnabled = false;
        private Button btnKeepFocusOn;
        private Button btnKeepFocusOff;
        private bool isKeepFocusEnabled = false;
        private Button btnSave;
        private Button btnCancel;

        private Panel pnlIconPreview;
        private Label lblEmojiPreview;
        private PictureBox pbImagePreview;

        private bool isDarkMode = true;
        private Color colorCanvas;
        private Color colorHeaderBg;
        private Color colorInputBg;
        private Color colorInputDisabledBg;
        private Color colorInputFore;
        private Color colorTextLabel;
        private Color colorButtonBrowseBg;
        private Color colorButtonBrowseFore;
        private Color colorButtonSaveBg;
        private Color colorButtonSaveFore;
        private Color colorButtonCancelBorder;
        private Color colorTextMuted;

        public EditForm(ContentItem item, bool isDarkMode)
        {
            this.isDarkMode = isDarkMode;
            InitializeTheme();
            InitializeComponent(item != null);

            if (item != null)
            {
                txtName.Text = item.name;
                txtIcon.Text = item.icon;
                txtDesc.Text = item.description;
                txtPath.Text = item.path;
                txtModulePath.Text = item.modulePath;
                txtModuleDelay.Text = item.moduleDelay.ToString();

                isModuleEnabled = !string.IsNullOrEmpty(item.modulePath);
                isKeepFocusEnabled = item.keepFocus;
            }
            ToggleModuleControls(isModuleEnabled);
            ToggleKeepFocus(isKeepFocusEnabled);

            txtIcon.TextChanged += (s, e) => UpdateIconPreview();
            UpdateIconPreview();
        }

        private void InitializeTheme()
        {
            if (isDarkMode)
            {
                // Dark Mode
                colorCanvas = Color.FromArgb(24, 25, 38);
                colorHeaderBg = Color.FromArgb(18, 19, 28);
                colorInputBg = Color.FromArgb(15, 16, 25);
                colorInputDisabledBg = Color.FromArgb(30, 30, 40);
                colorInputFore = Color.White;
                colorTextLabel = Color.FromArgb(156, 163, 175);
                colorButtonBrowseBg = Color.FromArgb(35, 37, 54);
                colorButtonBrowseFore = Color.White;
                colorButtonSaveBg = Color.FromArgb(123, 97, 255);
                colorButtonSaveFore = Color.White;
                colorButtonCancelBorder = Color.FromArgb(50, 50, 70);
                colorTextMuted = Color.FromArgb(120, 120, 140);
            }
            else
            {
                // Light Mode
                colorCanvas = Color.FromArgb(247, 247, 244);
                colorHeaderBg = Color.FromArgb(250, 250, 247);
                colorInputBg = Color.FromArgb(255, 255, 255);
                colorInputDisabledBg = Color.FromArgb(239, 238, 232);
                colorInputFore = Color.FromArgb(38, 37, 30);
                colorTextLabel = Color.FromArgb(128, 125, 114);
                colorButtonBrowseBg = Color.FromArgb(230, 229, 224);
                colorButtonBrowseFore = Color.FromArgb(38, 37, 30);
                colorButtonSaveBg = Color.FromArgb(245, 78, 0);
                colorButtonSaveFore = Color.White;
                colorButtonCancelBorder = Color.FromArgb(230, 229, 224);
                colorTextMuted = Color.FromArgb(160, 158, 150);
            }
        }

        private void InitializeComponent(bool isEdit)
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(550, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = colorCanvas;

            Font fontLabel = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);
            Color textLabelColor = colorTextLabel;
            Color inputBgColor = colorInputBg;
            Color inputForeColor = colorInputFore;

            // Custom Title Banner
            Panel modalHeader = new Panel();
            modalHeader.Dock = DockStyle.Top;
            modalHeader.Height = 50;
            modalHeader.BackColor = colorHeaderBg;

            Label lblTitle = new Label();
            lblTitle.Text = isEdit ? "✏️  시연 콘텐츠 수정" : "➕  새 시연 콘텐츠 추가";
            lblTitle.ForeColor = colorInputFore;
            lblTitle.Font = FontHelper.GetFont(10.5f, FontStyle.Bold);
            lblTitle.Location = new Point(20, 15);
            lblTitle.AutoSize = true;
            modalHeader.Controls.Add(lblTitle);
            this.Controls.Add(modalHeader);

            int startY = 70;
            int gap = 62;

            // Name
            CreateLabel("콘텐츠 이름 *", 20, startY, textLabelColor, fontLabel);
            txtName = CreateTextBox(20, startY + 20, 260, inputBgColor, inputForeColor, fontInput);

            // Icon (Emoji or Image Path)
            CreateLabel("아이콘 (이모지 / 이미지 경로)", 295, startY, textLabelColor, fontLabel);
            txtIcon = CreateTextBox(295, startY + 20, 120, inputBgColor, inputForeColor, fontInput);
            txtIcon.Text = "🚀";

            Button btnBrowseIcon = new Button();
            btnBrowseIcon.Text = "🖼️";
            btnBrowseIcon.Font = new Font("Segoe UI Emoji", 8.5f, FontStyle.Bold);
            btnBrowseIcon.BackColor = colorButtonBrowseBg;
            btnBrowseIcon.ForeColor = colorButtonBrowseFore;
            btnBrowseIcon.FlatStyle = FlatStyle.Flat;
            btnBrowseIcon.FlatAppearance.BorderSize = 0;
            btnBrowseIcon.Location = new Point(425, startY + 20);
            btnBrowseIcon.Size = new Size(40, 26);
            btnBrowseIcon.Cursor = Cursors.Hand;
            btnBrowseIcon.Click += BtnBrowseIcon_Click;
            this.Controls.Add(btnBrowseIcon);

            // Icon Preview
            CreateLabel("미리보기", 475, startY - 2, textLabelColor, FontHelper.GetFont(7.5f, FontStyle.Bold));
            pnlIconPreview = new Panel();
            pnlIconPreview.Location = new Point(475, startY + 13);
            pnlIconPreview.Size = new Size(40, 40);
            pnlIconPreview.BackColor = colorInputBg;
            
            // Round corners for premium preview panel
            pnlIconPreview.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int radius = 8;
                    path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
                    path.AddArc(pnlIconPreview.Width - radius * 2 - 1, 0, radius * 2, radius * 2, 270, 90);
                    path.AddArc(pnlIconPreview.Width - radius * 2 - 1, pnlIconPreview.Height - radius * 2 - 1, radius * 2, radius * 2, 0, 90);
                    path.AddArc(0, pnlIconPreview.Height - radius * 2 - 1, radius * 2, radius * 2, 90, 90);
                    path.CloseAllFigures();
                    pnlIconPreview.Region = new Region(path);
                    
                    using (Pen pen = new Pen(colorButtonCancelBorder, 1))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            };

            lblEmojiPreview = new Label();
            lblEmojiPreview.Dock = DockStyle.Fill;
            lblEmojiPreview.TextAlign = ContentAlignment.MiddleCenter;
            lblEmojiPreview.Font = new Font("Segoe UI Emoji", 14f);
            lblEmojiPreview.ForeColor = colorInputFore;

            pbImagePreview = new PictureBox();
            pbImagePreview.Dock = DockStyle.Fill;
            pbImagePreview.SizeMode = PictureBoxSizeMode.Zoom;
            pbImagePreview.Visible = false;

            pnlIconPreview.Controls.Add(lblEmojiPreview);
            pnlIconPreview.Controls.Add(pbImagePreview);
            this.Controls.Add(pnlIconPreview);

            // Description
            CreateLabel("콘텐츠 설명", 20, startY + gap, textLabelColor, fontLabel);
            txtDesc = CreateTextBox(20, startY + gap + 20, 510, inputBgColor, inputForeColor, fontInput);
            txtDesc.Multiline = true;
            txtDesc.Height = 60;

            // Path (.exe)
            CreateLabel("실행 파일 절대 경로 (.exe) *", 20, startY + gap * 2 + 25, textLabelColor, fontLabel);
            txtPath = CreateTextBox(20, startY + gap * 2 + 45, 410, inputBgColor, inputForeColor, fontInput);
            
            btnBrowse = new Button();
            btnBrowse.Text = "📂  찾기";
            btnBrowse.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnBrowse.BackColor = colorButtonBrowseBg;
            btnBrowse.ForeColor = colorButtonBrowseFore;
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Location = new Point(440, startY + gap * 2 + 45);
            btnBrowse.Size = new Size(90, 26);
            btnBrowse.Cursor = Cursors.Hand;
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);

            // Startup Module (.exe) & Delay (Seconds) with 2-Button Toggle UI
            CreateLabel("선행 구동 모듈 사용 여부", 20, 300, textLabelColor, fontLabel);

            btnModuleOn = new Button();
            btnModuleOn.Text = "ON";
            btnModuleOn.Font = FontHelper.GetFont(8f, FontStyle.Bold);
            btnModuleOn.FlatStyle = FlatStyle.Flat;
            btnModuleOn.FlatAppearance.BorderSize = 0;
            btnModuleOn.Size = new Size(50, 22);
            btnModuleOn.Location = new Point(220, 296);
            btnModuleOn.Cursor = Cursors.Hand;
            btnModuleOn.Click += (s, e) => ToggleModuleControls(true);
            this.Controls.Add(btnModuleOn);

            btnModuleOff = new Button();
            btnModuleOff.Text = "OFF";
            btnModuleOff.Font = FontHelper.GetFont(8f, FontStyle.Bold);
            btnModuleOff.FlatStyle = FlatStyle.Flat;
            btnModuleOff.FlatAppearance.BorderSize = 0;
            btnModuleOff.Size = new Size(50, 22);
            btnModuleOff.Location = new Point(275, 296);
            btnModuleOff.Cursor = Cursors.Hand;
            btnModuleOff.Click += (s, e) => ToggleModuleControls(false);
            this.Controls.Add(btnModuleOff);

            txtModulePath = CreateTextBox(20, 320, 300, inputBgColor, inputForeColor, fontInput);

            btnBrowseModule = new Button();
            btnBrowseModule.Text = "📂  찾기";
            btnBrowseModule.Font = FontHelper.GetFont(8.5f, FontStyle.Bold);
            btnBrowseModule.BackColor = colorButtonBrowseBg;
            btnBrowseModule.ForeColor = colorButtonBrowseFore;
            btnBrowseModule.FlatStyle = FlatStyle.Flat;
            btnBrowseModule.FlatAppearance.BorderSize = 0;
            btnBrowseModule.Location = new Point(330, 320);
            btnBrowseModule.Size = new Size(60, 26);
            btnBrowseModule.Cursor = Cursors.Hand;
            btnBrowseModule.Click += BtnBrowseModule_Click;
            this.Controls.Add(btnBrowseModule);
            CreateLabel("대기 시간 (초)", 400, 300, textLabelColor, fontLabel);
            txtModuleDelay = CreateTextBox(400, 320, 130, inputBgColor, inputForeColor, fontInput);
            txtModuleDelay.Text = "10";
            // Tiny explanations
            CreateLabel("(선행 구동기, 트래커 제어 등 메인 콘텐츠 기동 전에 필수 구동할 파일)", 20, 350, colorTextMuted, FontHelper.GetFont(7.5f));
            CreateLabel("(모듈 켜진 후 대기할 시간)", 400, 350, colorTextMuted, FontHelper.GetFont(7.5f));

            // Action buttons
            btnSave = new Button();
            btnSave.Text = "저장하기";
            btnSave.Font = FontHelper.GetFont(9.5f, FontStyle.Bold);
            btnSave.BackColor = colorButtonSaveBg;
            btnSave.ForeColor = colorButtonSaveFore;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Size = new Size(100, 36);
            btnSave.Location = new Point(320, this.Height - 55);
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button();
            btnCancel.Text = "취소";
            btnCancel.Font = FontHelper.GetFont(9.5f, FontStyle.Bold);
            btnCancel.BackColor = Color.Transparent;
            btnCancel.ForeColor = colorTextLabel;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = colorButtonCancelBorder;
            btnCancel.Size = new Size(90, 36);
            btnCancel.Location = new Point(440, this.Height - 55);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            // Focus Holding Toggle UI
            CreateLabel("포커스 유지 여부", 20, 385, textLabelColor, fontLabel);

            btnKeepFocusOn = new Button();
            btnKeepFocusOn.Text = "ON";
            btnKeepFocusOn.Font = FontHelper.GetFont(8f, FontStyle.Bold);
            btnKeepFocusOn.FlatStyle = FlatStyle.Flat;
            btnKeepFocusOn.FlatAppearance.BorderSize = 0;
            btnKeepFocusOn.Size = new Size(50, 22);
            btnKeepFocusOn.Location = new Point(180, 381);
            btnKeepFocusOn.Cursor = Cursors.Hand;
            btnKeepFocusOn.Click += (s, e) => ToggleKeepFocus(true);
            this.Controls.Add(btnKeepFocusOn);

            btnKeepFocusOff = new Button();
            btnKeepFocusOff.Text = "OFF";
            btnKeepFocusOff.Font = FontHelper.GetFont(8f, FontStyle.Bold);
            btnKeepFocusOff.FlatStyle = FlatStyle.Flat;
            btnKeepFocusOff.FlatAppearance.BorderSize = 0;
            btnKeepFocusOff.Size = new Size(50, 22);
            btnKeepFocusOff.Location = new Point(235, 381);
            btnKeepFocusOff.Cursor = Cursors.Hand;
            btnKeepFocusOff.Click += (s, e) => ToggleKeepFocus(false);
            this.Controls.Add(btnKeepFocusOff);

            CreateLabel("(단일 빌드는 ON, 래퍼/컨테이너 빌드는 OFF 권장)", 295, 385, colorTextMuted, FontHelper.GetFont(7.5f));

            // Modal border line
            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(colorButtonCancelBorder, 1.5f))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };
        }

        private void ToggleModuleControls(bool enabled)
        {
            this.isModuleEnabled = enabled;
            txtModulePath.Enabled = enabled;
            btnBrowseModule.Enabled = enabled;
            txtModuleDelay.Enabled = enabled;

            if (enabled)
            {
                btnModuleOn.BackColor = colorButtonSaveBg;
                btnModuleOn.ForeColor = colorButtonSaveFore;
                btnModuleOff.BackColor = colorButtonBrowseBg;
                btnModuleOff.ForeColor = colorTextLabel;
                
                txtModulePath.BackColor = colorInputBg;
                txtModuleDelay.BackColor = colorInputBg;
            }
            else
            {
                btnModuleOn.BackColor = colorButtonBrowseBg;
                btnModuleOn.ForeColor = colorTextLabel;
                btnModuleOff.BackColor = isDarkMode ? Color.FromArgb(80, 80, 100) : Color.FromArgb(200, 198, 190);
                btnModuleOff.ForeColor = Color.White;
                
                txtModulePath.BackColor = colorInputDisabledBg;
                txtModuleDelay.BackColor = colorInputDisabledBg;
            }
        }

        private void ToggleKeepFocus(bool enabled)
        {
            this.isKeepFocusEnabled = enabled;
            if (enabled)
            {
                btnKeepFocusOn.BackColor = colorButtonSaveBg;
                btnKeepFocusOn.ForeColor = colorButtonSaveFore;
                btnKeepFocusOff.BackColor = colorButtonBrowseBg;
                btnKeepFocusOff.ForeColor = colorTextLabel;
            }
            else
            {
                btnKeepFocusOn.BackColor = colorButtonBrowseBg;
                btnKeepFocusOn.ForeColor = colorTextLabel;
                btnKeepFocusOff.BackColor = isDarkMode ? Color.FromArgb(80, 80, 100) : Color.FromArgb(200, 198, 190);
                btnKeepFocusOff.ForeColor = Color.White;
            }
        }

        private void UpdateIconPreview()
        {
            if (pnlIconPreview == null || lblEmojiPreview == null || pbImagePreview == null) return;

            string iconText = txtIcon.Text.Trim();
            
            if (string.IsNullOrEmpty(iconText))
            {
                lblEmojiPreview.Text = "❓";
                lblEmojiPreview.Visible = true;
                pbImagePreview.Visible = false;
                return;
            }

            bool isFile = false;
            try
            {
                if (System.IO.File.Exists(iconText))
                {
                    isFile = true;
                }
            }
            catch { }

            if (isFile)
            {
                try
                {
                    if (pbImagePreview.Image != null)
                    {
                        pbImagePreview.Image.Dispose();
                        pbImagePreview.Image = null;
                    }
                    pbImagePreview.Image = Image.FromFile(iconText);
                    pbImagePreview.Visible = true;
                    lblEmojiPreview.Visible = false;
                }
                catch
                {
                    lblEmojiPreview.Text = iconText;
                    lblEmojiPreview.Visible = true;
                    pbImagePreview.Visible = false;
                }
            }
            else
            {
                lblEmojiPreview.Text = iconText;
                lblEmojiPreview.Visible = true;
                pbImagePreview.Visible = false;
            }
        }

        private void CreateLabel(string text, int x, int y, Color color, Font font)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.ForeColor = color;
            lbl.Font = font;
            lbl.Location = new Point(x, y);
            lbl.AutoSize = true;
            this.Controls.Add(lbl);
        }

        private TextBox CreateTextBox(int x, int y, int w, Color bg, Color fg, Font font)
        {
            TextBox txt = new TextBox();
            txt.Location = new Point(x, y);
            txt.Width = w;
            txt.BackColor = bg;
            txt.ForeColor = fg;
            txt.Font = font;
            txt.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(txt);
            return txt;
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
                ofd.Title = "시연할 실행 파일을 선택해 주세요";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = ofd.FileName;
                    if (string.IsNullOrEmpty(txtName.Text))
                    {
                        txtName.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            }
        }

        private void BtnBrowseIcon_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|모든 파일 (*.*)|*.*";
                ofd.Title = "아이콘으로 사용할 이미지 파일을 선택해 주세요";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtIcon.Text = ofd.FileName;
                }
            }
        }

        private void BtnBrowseModule_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
                ofd.Title = "선행 구동할 모듈 실행 파일을 선택해 주세요";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtModulePath.Text = ofd.FileName;
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string path = txtPath.Text.Trim();
            string modulePath = isModuleEnabled ? txtModulePath.Text.Trim() : "";
            string delayStr = txtModuleDelay.Text.Trim();
            int moduleDelay = 10;
            
            if (isModuleEnabled && !string.IsNullOrEmpty(delayStr))
            {
                if (!int.TryParse(delayStr, out moduleDelay) || moduleDelay < 10)
                {
                    MessageBox.Show("선행 구동 대기 시간은 최소 10초 이상이어야 합니다. (10초 미만 설정 불가)", "대기 시간 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtModuleDelay.Focus();
                    return;
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("콘텐츠 이름을 입력해 주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("실행 파일 경로를 선택해 주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPath.Focus();
                return;
            }

            ResultItem = new ContentItem
            {
                name = name,
                icon = txtIcon.Text.Trim(),
                description = txtDesc.Text.Trim(),
                path = path,
                modulePath = modulePath,
                moduleDelay = moduleDelay,
                keepFocus = isKeepFocusEnabled
            };

            this.DialogResult = DialogResult.OK;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                Point pos = this.PointToClient(new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16));
                int border = 8; // 테두리 감지 마진 (px)

                bool left = pos.X <= border;
                bool right = pos.X >= this.ClientSize.Width - border;
                bool top = pos.Y <= border;
                bool bottom = pos.Y >= this.ClientSize.Height - border;

                if (left && top) m.Result = (IntPtr)HTTOPLEFT;
                else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
                else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            }
        }
    }
}
