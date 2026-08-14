using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;

namespace ShowroomPowerController
{
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

        private Button btnSaveConfirm;
        private Button btnCancel;

        public DeviceItem EditedDevice { get; private set; }

        public DeviceEditForm(DeviceItem dev, List<DeviceItem> currentDevices, List<string> currentSpaces)
        {
            this.targetDevice = dev;
            this.tempDevices = currentDevices;
            this.tempSpaces = currentSpaces;

            this.Text = string.Format("✏️ 장치 정보 수정 (Edit Device) - [{0}]", dev.Name);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(460, 420);

            InitializeEditUI();
            LoadDeviceData();
            ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);
        }

        private void InitializeEditUI()
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
            
            // Row 2: Name (Y: 58)
            lName = new Label() { Text = "장비 이름:", Location = new Point(xL, yStart + yGap), AutoSize = true, Font = fontLabel };
            txtName = new TextBox() { Location = new Point(xI, yStart + yGap - 3), Size = new Size(240, 22), Font = fontInput };

            // Row 3: Type (Y: 96)
            lType = new Label() { Text = "장비 유형 (Type):", Location = new Point(xL, yStart + yGap * 2), AutoSize = true, Font = fontLabel };
            cbType = new ComboBox() { Location = new Point(xI, yStart + yGap * 2 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            cbType.Items.Add("PC");
            cbType.Items.Add("Projector");
            cbType.SelectedIndexChanged += (s, e) => SwitchAssociatedControl();

            // Row 4: IP (Y: 134)
            lIp = new Label() { Text = "IP 주소 (IpAddress):", Location = new Point(xL, yStart + yGap * 3), AutoSize = true, Font = fontLabel };
            txtIp = new TextBox() { Location = new Point(xI, yStart + yGap * 3 - 3), Size = new Size(240, 22), Font = fontInput };

            // Row 5: Port (Y: 172)
            lPort = new Label() { Text = "TCP 통신 포트:", Location = new Point(xL, yStart + yGap * 4), AutoSize = true, Font = fontLabel };
            txtPort = new TextBox() { Location = new Point(xI, yStart + yGap * 4 - 3), Size = new Size(240, 22), Font = fontInput };

            // Row 6: Space (Y: 210)
            lSpace = new Label() { Text = "소속 공간 (Space):", Location = new Point(xL, yStart + yGap * 5), AutoSize = true, Font = fontLabel };
            cbSpace = new ComboBox() { Location = new Point(xI, yStart + yGap * 5 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            foreach (var sp in tempSpaces) cbSpace.Items.Add(sp);
            if (cbSpace.Items.Count > 0) cbSpace.SelectedIndex = 0;

            // Row 7 (Shared Slot Y: 248): MAC (for PC) OR Assoc (for Projector)
            lMac = new Label() { Text = "MAC 주소 (WOL용):", Location = new Point(xL, yStart + yGap * 6), AutoSize = true, Font = fontLabel };
            txtMac = new TextBox() { Location = new Point(xI, yStart + yGap * 6 - 3), Size = new Size(240, 22), Font = fontInput };

            lAssoc = new Label() { Text = "결합 기기 ID (Assoc):", Location = new Point(xL, yStart + yGap * 6), AutoSize = true, Font = fontLabel, Visible = false };
            cbAssociated = new ComboBox() { Location = new Point(xI, yStart + yGap * 6 - 3), Size = new Size(240, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Visible = false };

            // Row 8: Desc (Y: 286)
            lDesc = new Label() { Text = "설명:", Location = new Point(xL, yStart + yGap * 7), AutoSize = true, Font = fontLabel };
            txtDesc = new TextBox() { Location = new Point(xI, yStart + yGap * 7 - 3), Size = new Size(240, 22), Font = fontInput };

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
            btnSaveConfirm = new Button() { Text = "💾 수정사항 저장", Size = new Size(160, 36), Location = new Point(70, yStart + yGap * 8 + 15), BackColor = ColorTranslator.FromHtml("#1f8a65"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
            btnSaveConfirm.Click += BtnSaveConfirm_Click;

            btnCancel = new Button() { Text = "✕ 취소", Size = new Size(110, 36), Location = new Point(255, yStart + yGap * 8 + 15), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
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
            txtDesc.Text = targetDevice.Description;

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
                lMac.Visible = false;
                txtMac.Visible = false;

                lAssoc.Visible = true;
                cbAssociated.Visible = true;

                cbAssociated.Items.Clear();
                cbAssociated.Items.Add("[ 연결 없음 ]");

                foreach (var d in tempDevices)
                {
                    if (d.Id != targetDevice.Id && d.Type == "PC") 
                    {
                        cbAssociated.Items.Add(string.Format("[{0}] {1}", d.Id, d.Name));
                    }
                }
                cbAssociated.SelectedIndex = 0;
            }
            else
            {
                lAssoc.Visible = false;
                cbAssociated.Visible = false;

                lMac.Visible = true;
                txtMac.Visible = true;
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

            EditedDevice = new DeviceItem()
            {
                Id = newId,
                Name = txtName.Text.Trim() == "" ? "수정된 장치" : txtName.Text.Trim(),
                Type = cbType.SelectedItem != null ? cbType.SelectedItem.ToString() : "Projector",
                IpAddress = txtIp.Text.Trim(),
                MacAddress = txtMac.Text.Trim(),
                Port = prt,
                Space = cbSpace.SelectedItem != null ? cbSpace.SelectedItem.ToString() : "로비",
                AssociatedDeviceId = (cbType.SelectedItem != null && cbType.SelectedItem.ToString() == "Projector") ? selectedAssocId : "",
                BootOrder = targetDevice.BootOrder,
                BootDelaySeconds = targetDevice.BootDelaySeconds,
                Description = txtDesc.Text,
                RuntimeStatus = targetDevice.RuntimeStatus,
                PowerOnSequenceMode = targetDevice.PowerOnSequenceMode
            };

            // 만약 기존 targetDevice의 속성을 갱신해야 할 경우
            targetDevice.Id = EditedDevice.Id;
            targetDevice.Name = EditedDevice.Name;
            targetDevice.Type = EditedDevice.Type;
            targetDevice.IpAddress = EditedDevice.IpAddress;
            targetDevice.MacAddress = EditedDevice.MacAddress;
            targetDevice.Port = EditedDevice.Port;
            targetDevice.Space = EditedDevice.Space;
            targetDevice.AssociatedDeviceId = EditedDevice.AssociatedDeviceId;
            targetDevice.Description = EditedDevice.Description;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
