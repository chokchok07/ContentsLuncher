using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Web.Script.Serialization;

namespace ShowroomPowerController
{
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
            ThemeManager.ApplyThemeTo(this, ThemeManager.IsDark);
        }

        private void InitializeConfigUI(Color boxClr)
        {
            Font fontLabel = FontHelper.GetFont(9.5f, FontStyle.Bold);
            Font fontInput = FontHelper.GetFont(9.5f);

            lSpaceManage = new Label() { Text = "공간 관리:", Location = new Point(20, 20), AutoSize = true, ForeColor = Color.White, Font = fontLabel };

            cbSpace = new ComboBox() { Location = new Point(90, 16), Size = new Size(160, 24), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = boxClr, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = fontInput };
            RefreshSpaceComboItems();

            btnSpaceAdd = new Button() { Text = "＋", Size = new Size(40, 26), Location = new Point(260, 15), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnSpaceAdd.FlatAppearance.BorderSize = 0;
            btnSpaceAdd.Click += BtnSpaceAdd_Click;

            btnSpaceDel = new Button() { Text = "－", Size = new Size(40, 26), Location = new Point(310, 15), BackColor = ColorTranslator.FromHtml("#cf2d56"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
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
            lvDevices.SelectedIndexChanged += (s, e) => UpdateEditButtonState();
            lvDevices.ItemChecked += (s, e) => UpdateEditButtonState();

            btnAdd = new Button() { Text = "➕ 장치 추가", Size = new Size(140, 36), Location = new Point(20, 460), BackColor = Color.FromArgb(35, 37, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9f, FontStyle.Bold) };
            btnAdd.Click += BtnAdd_Click;

            btnEdit = new Button() { Text = "✏️ 장치 수정", Size = new Size(140, 36), Location = new Point(170, 460), BackColor = ColorTranslator.FromHtml("#f54e00"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9f, FontStyle.Bold) };
            btnEdit.Click += BtnEdit_Click;

            btnDelete = new Button() { Text = "🗑️ 선택 삭제", Size = new Size(140, 36), Location = new Point(320, 460), BackColor = ColorTranslator.FromHtml("#cf2d56"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9f, FontStyle.Bold) };
            btnDelete.Click += BtnDelete_Click;

            btnApply = new Button() { Text = "💾 전체 변경사항 저장 및 닫기", Size = new Size(440, 36), Location = new Point(20, 500), BackColor = ColorTranslator.FromHtml("#1f8a65"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontHelper.GetFont(9.5f, FontStyle.Bold) };
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
            UpdateEditButtonState();
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
            UpdateEditButtonState();
        }

        private void UpdateEditButtonState()
        {
            int checkedCount = lvDevices.CheckedItems.Count;
            int selectedCount = lvDevices.SelectedItems.Count;

            bool canEdit = false;
            if (checkedCount == 1)
            {
                canEdit = true;
            }
            else if (checkedCount == 0 && selectedCount == 1)
            {
                canEdit = true;
            }
            else
            {
                canEdit = false;
            }

            ThemeManager.SetControlEnabledState(btnEdit, canEdit, ThemeManager.IsDark);
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            string selId = null;
            if (lvDevices.CheckedItems.Count == 1)
            {
                selId = lvDevices.CheckedItems[0].Text;
            }
            else if (lvDevices.CheckedItems.Count == 0 && lvDevices.SelectedItems.Count == 1)
            {
                selId = lvDevices.SelectedItems[0].Text;
            }
            else
            {
                MessageBox.Show("수정은 한 번에 1개의 기기만 가능합니다. 1개 기기만 선택하거나 체크하여 주십시오.", "단일 선택 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
}
