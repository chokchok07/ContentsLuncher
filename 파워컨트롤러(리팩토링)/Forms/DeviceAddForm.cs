using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;

namespace ShowroomPowerController
{
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
        private TextBox txtDesc;

        private Label lId;
        private Label lName;
        private Label lType;
        private Label lIp;
        private Label lPort;
        private Label lSpace;
        private Label lMac;
        private Label lAssoc;
        private Label lDesc;

        private Button btnAddConfirm;
        private Button btnCancel;

        private bool isSuggesting = false;
        private bool isUserCustomId = false;

        public DeviceItem AddedDevice { get; private set; }

        public DeviceAddForm(List<DeviceItem> currentDevices, List<string> currentSpaces)
            : this(currentDevices, currentSpaces, null, null)
        {
        }

        public DeviceAddForm(List<DeviceItem> currentDevices, List<string> currentSpaces, string presetParentId, string presetSpace, string presetType)
            : this(currentDevices, currentSpaces, null, null)
        {
            if (presetType == "Projector")
            {
                this.Text = "➕ 빔 프로젝터 연동 추가 (Add Projector)";
                cbType.SelectedItem = "Projector";
                cbType.Enabled = false;
                txtPort.Text = "4352";
                txtDesc.Text = "빔 프로젝터";
            }
            if (!string.IsNullOrEmpty(presetSpace) && cbSpace.Items.Contains(presetSpace))
            {
                cbSpace.SelectedItem = presetSpace;
                cbSpace.Enabled = false;
            }
            if (!string.IsNullOrEmpty(presetParentId))
            {
                for (int i = 0; i < cbAssociated.Items.Count; i++)
                {
                    if (cbAssociated.Items[i].ToString().Contains("[" + presetParentId + "]"))
                    {
                        cbAssociated.SelectedIndex = i;
                        cbAssociated.Enabled = false;
                        break;
                    }
                }
            }

            SwitchAssociatedControl();
        }

        public DeviceAddForm(List<DeviceItem> currentDevices, List<string> currentSpaces, string presetDeviceId, string presetIp)
        {
            this.tempDevices = currentDevices;
            this.tempSpaces = currentSpaces;

            this.Text = "➕ 신규 장치 추가 등록 (Add New Device)";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(460, 420);
            
            Color bgClr = Color.FromArgb(18, 19, 28);
            Color boxClr = Color.FromArgb(28, 29, 43);
            this.BackColor = bgClr;

            InitializeAddUI(boxClr);

            if (!string.IsNullOrEmpty(presetDeviceId))
            {
                isUserCustomId = true;
                txtId.Text = presetDeviceId;
            }
            else
            {
                SuggestNextDeviceId();
            }

            if (!string.IsNullOrEmpty(presetIp))
            {
                txtIp.Text = presetIp;
            }

            ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);
        }

        private void InitializeAddUI(Color boxClr)
        {
            Font fontLabel = FontHelper.GetFont(9f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);

            int xL = 25; 
            int xI = 180; 
            int yStart = 20; 
            int yGap = 38;

            // Row 1: ID (Y: 20)
            lId = new Label() { Text = "장비 고유 ID:", Location = new Point(xL, yStart), AutoSize = true, Font = fontLabel };
            txtId = new TextBox() { Location = new Point(xI, yStart - 3), Size = new Size(240, 22), Font = fontInput };
            txtId.TextChanged += (s, e) => {
                if (!isSuggesting) isUserCustomId = true;
            };
            
            // Row 2: Name (Y: 58)
            lName = new Label() { Text = "장비 이름:", Location = new Point(xL, yStart + yGap), AutoSize = true, Font = fontLabel };
            txtName = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(240, 22), Font = fontInput };

            // Row 3: Type (Y: 96)
            lType = new Label() { Text = "장비 유형 (Type):", Location = new Point(xL, yStart + yGap * 2), AutoSize = true, Font = fontLabel };
            cbType = new ComboBox() { Location = new Point(xI, yStart + yGap * 2 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            cbType.Items.Add("PC");
            cbType.Items.Add("Projector");
            cbType.SelectedIndex = 0;
            cbType.SelectedIndexChanged += (s, e) => {
                SwitchAssociatedControl();
                SuggestNextDeviceId();
            };

            // Row 4: IP (Y: 134)
            lIp = new Label() { Text = "IP 주소 (IpAddress):", Location = new Point(xL, yStart + yGap * 3), AutoSize = true, Font = fontLabel };
            txtIp = new TextBox() { Location = new Point(xI, yStart + yGap * 3 - 3), Size = new Size(240, 22), Font = fontInput, Text = "192.168.0.100" };

            // Row 5: Port (Y: 172)
            lPort = new Label() { Text = "TCP 통신 포트:", Location = new Point(xL, yStart + yGap * 4), AutoSize = true, Font = fontLabel };
            txtPort = new TextBox() { Location = new Point(xI, yStart + yGap * 4 - 3), Size = new Size(240, 22), Font = fontInput, Text = "9999" };

            // Row 6: Space (Y: 210)
            lSpace = new Label() { Text = "소속 공간 (Space):", Location = new Point(xL, yStart + yGap * 5), AutoSize = true, Font = fontLabel };
            cbSpace = new ComboBox() { Location = new Point(xI, yStart + yGap * 5 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            foreach (var sp in tempSpaces) cbSpace.Items.Add(sp);
            if (cbSpace.Items.Count > 0) cbSpace.SelectedIndex = 0;

            // Row 7 (Shared Slot Y: 248): MAC (for PC) OR Assoc (for Projector)
            lMac = new Label() { Text = "MAC 주소 (WOL용):", Location = new Point(xL, yStart + yGap * 6), AutoSize = true, Font = fontLabel };
            txtMac = new TextBox() { Location = new Point(xI, yStart + yGap * 6 - 3), Size = new Size(240, 22), Font = fontInput, Text = "00-11-22-33-44-55" };

            lAssoc = new Label() { Text = "결합 기기 ID (Assoc):", Location = new Point(xL, yStart + yGap * 6), AutoSize = true, Font = fontLabel, Visible = false };
            cbAssociated = new ComboBox() { Location = new Point(xI, yStart + yGap * 6 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Visible = false };

            // Row 8: Desc (Y: 286)
            lDesc = new Label() { Text = "설명:", Location = new Point(xL, yStart + yGap * 7), AutoSize = true, Font = fontLabel };
            txtDesc = new TextBox() { Location = new Point(xI, yStart + yGap * 7 - 3), Size = new Size(240, 22), Font = fontInput, Text = "신규 등록" };

            this.Controls.Add(lId); this.Controls.Add(txtId);
            this.Controls.Add(lName); this.Controls.Add(txtName);
            this.Controls.Add(lType); this.Controls.Add(cbType);
            this.Controls.Add(lIp); this.Controls.Add(txtIp);
            this.Controls.Add(lPort); this.Controls.Add(txtPort);
            this.Controls.Add(lSpace); this.Controls.Add(cbSpace);
            this.Controls.Add(lMac); this.Controls.Add(txtMac);
            this.Controls.Add(lAssoc); this.Controls.Add(cbAssociated);
            this.Controls.Add(lDesc); this.Controls.Add(txtDesc);

            // Row 9: Action Buttons (Y: 345)
            btnAddConfirm = new Button() { Text = "➕ 확인 및 추가", Size = new Size(160, 36), Location = new Point(70, yStart + yGap * 8 + 15), BackColor = ColorTranslator.FromHtml("#1f8a65"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
            btnAddConfirm.Click += BtnAddConfirm_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(110, 36), Location = new Point(255, yStart + yGap * 8 + 15), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
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
                // 프로젝터: MAC 숨기고 결합 기기 선택 활성화
                if (lMac != null) lMac.Visible = false;
                if (txtMac != null) txtMac.Visible = false;

                if (lAssoc != null) lAssoc.Visible = true;
                if (cbAssociated != null)
                {
                    cbAssociated.Visible = true;
                    if (cbAssociated.Items.Count == 0)
                    {
                        cbAssociated.Items.Add("[ 연결 없음 ]");
                        foreach (var d in tempDevices)
                        {
                            if (d.Type == "PC") cbAssociated.Items.Add(string.Format("[{0}] {1}", d.Id, d.Name));
                        }
                        cbAssociated.SelectedIndex = 0;
                    }
                }

                if (txtPort != null && txtPort.Text == "9999") txtPort.Text = "4352";
            }
            else
            {
                // PC: 결합 기기 숨기고 MAC 주소 입력 활성화
                if (lAssoc != null) lAssoc.Visible = false;
                if (cbAssociated != null) cbAssociated.Visible = false;

                if (lMac != null) lMac.Visible = true;
                if (txtMac != null) txtMac.Visible = true;

                if (txtPort != null && txtPort.Text == "4352") txtPort.Text = "9999";
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

            int prt;
            int.TryParse(txtPort.Text, out prt);

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
                BootOrder = 1,
                BootDelaySeconds = 10,
                Description = txtDesc.Text,
                PowerOnSequenceMode = "PC_FIRST"
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
}
