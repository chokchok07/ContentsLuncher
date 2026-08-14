using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShowroomPowerController
{
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

            lblPrompt = new Label() { Text = promptText, Location = new Point(20, 15), Size = new Size(260, 20), Font = FontHelper.GetFont(9f, FontStyle.Bold) };
            txtInput = new TextBox() { Location = new Point(20, 42), Size = new Size(260, 22), Font = FontHelper.GetFont(9.5f), BackColor = Color.FromArgb(28, 29, 43), ForeColor = Color.White };
            
            btnOk = new Button() { Text = "확인", Location = new Point(125, 80), Size = new Size(75, 26), BackColor = ColorTranslator.FromHtml("#f54e00"), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
            btnOk.FlatAppearance.BorderSize = 0;
            
            btnCancel = new Button() { Text = "취소", Location = new Point(205, 80), Size = new Size(75, 26), BackColor = Color.FromArgb(35, 37, 54), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(lblPrompt);
            this.Controls.Add(txtInput);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
            ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);
        }
    }

    // --- 6. 신규 장치 입력 및 확인용 전용 독립 팝업 폼 (DeviceAddForm) ---
}
