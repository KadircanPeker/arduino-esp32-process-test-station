using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ProcessTestApp.Data;
using ProcessTestApp.Domain;

namespace ProcessTestApp
{
    public class AuditLogForm : Form
    {
        private DataGridView dgvLogs;
        private TextBox txtSearchUser;
        private ComboBox cmbActionType;
        private Button btnRefresh;
        private Label lblHeader;
        private Panel pnlHeader;
        private Panel pnlFilter;

        private readonly IAuditLogRepository _auditLogRepository;
        private List<AuditLog> _allLogs;

        public AuditLogForm()
        {
            var factory = new SqlConnectionFactory(null);
            _auditLogRepository = new AuditLogRepository(factory);
            
            InitializeComponent();
            LoadLogs();
        }

        private void InitializeComponent()
        {
            this.dgvLogs = new DataGridView();
            this.txtSearchUser = new TextBox();
            this.cmbActionType = new ComboBox();
            this.btnRefresh = new Button();
            this.lblHeader = new Label();
            this.pnlHeader = new Panel();
            this.pnlFilter = new Panel();

            ((System.ComponentModel.ISupportInitialize)(this.dgvLogs)).BeginInit();
            this.pnlHeader.SuspendLayout();
            this.pnlFilter.SuspendLayout();
            this.SuspendLayout();

            // Form Properties
            this.Text = "Sistem Değişiklik Günlüğü (Audit Log)";
            this.Size = new Size(820, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(32, 33, 36);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9F);

            // pnlHeader
            this.pnlHeader.BackColor = Color.FromArgb(43, 44, 47);
            this.pnlHeader.Dock = DockStyle.Top;
            this.pnlHeader.Height = 50;
            this.pnlHeader.Controls.Add(this.lblHeader);

            // lblHeader
            this.lblHeader.Text = "🔒 Sistem Değişiklik Günlüğü (Audit Log)";
            this.lblHeader.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            this.lblHeader.ForeColor = Color.FromArgb(26, 115, 232);
            this.lblHeader.Location = new Point(15, 12);
            this.lblHeader.AutoSize = true;

            // pnlFilter
            this.pnlFilter.BackColor = Color.FromArgb(38, 39, 42);
            this.pnlFilter.Location = new Point(15, 60);
            this.pnlFilter.Size = new Size(775, 45);
            this.pnlFilter.BorderStyle = BorderStyle.FixedSingle;

            // txtSearchUser (Kullanıcı arama)
            Label lblUser = new Label();
            lblUser.Text = "Kullanıcı:";
            lblUser.ForeColor = Color.FromArgb(170, 170, 175);
            lblUser.Location = new Point(10, 13);
            lblUser.Size = new Size(60, 20);
            this.pnlFilter.Controls.Add(lblUser);

            this.txtSearchUser.Location = new Point(70, 10);
            this.txtSearchUser.Size = new Size(130, 23);
            this.txtSearchUser.BackColor = Color.FromArgb(50, 51, 54);
            this.txtSearchUser.ForeColor = Color.White;
            this.txtSearchUser.BorderStyle = BorderStyle.FixedSingle;
            this.txtSearchUser.TextChanged += (s, e) => ApplyFilters();
            this.pnlFilter.Controls.Add(this.txtSearchUser);

            // cmbActionType (İşlem Tipi arama)
            Label lblType = new Label();
            lblType.Text = "İşlem Tipi:";
            lblType.ForeColor = Color.FromArgb(170, 170, 175);
            lblType.Location = new Point(230, 13);
            lblType.Size = new Size(70, 20);
            this.pnlFilter.Controls.Add(lblType);

            this.cmbActionType.Location = new Point(300, 10);
            this.cmbActionType.Size = new Size(200, 23);
            this.cmbActionType.BackColor = Color.FromArgb(50, 51, 54);
            this.cmbActionType.ForeColor = Color.White;
            this.cmbActionType.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbActionType.SelectedIndexChanged += (s, e) => ApplyFilters();
            this.pnlFilter.Controls.Add(this.cmbActionType);

            // btnRefresh
            this.btnRefresh.Location = new Point(650, 7);
            this.btnRefresh.Size = new Size(110, 28);
            this.btnRefresh.Text = "🔄 Yenile";
            this.btnRefresh.FlatStyle = FlatStyle.Flat;
            this.btnRefresh.FlatAppearance.BorderSize = 0;
            this.btnRefresh.BackColor = Color.FromArgb(26, 115, 232);
            this.btnRefresh.ForeColor = Color.White;
            this.btnRefresh.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnRefresh.Click += (s, e) => LoadLogs();
            this.pnlFilter.Controls.Add(this.btnRefresh);

            // dgvLogs
            this.dgvLogs.Location = new Point(15, 115);
            this.dgvLogs.Size = new Size(775, 340);
            this.dgvLogs.BackgroundColor = Color.FromArgb(43, 44, 47);
            this.dgvLogs.ForeColor = Color.White;
            this.dgvLogs.GridColor = Color.FromArgb(60, 64, 67);
            this.dgvLogs.BorderStyle = BorderStyle.None;
            this.dgvLogs.AllowUserToAddRows = false;
            this.dgvLogs.AllowUserToDeleteRows = false;
            this.dgvLogs.ReadOnly = true;
            this.dgvLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            this.dgvLogs.EnableHeadersVisualStyles = false;
            this.dgvLogs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(60, 64, 67);
            this.dgvLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            this.dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.dgvLogs.DefaultCellStyle.BackColor = Color.FromArgb(43, 44, 47);
            this.dgvLogs.DefaultCellStyle.ForeColor = Color.White;
            this.dgvLogs.DefaultCellStyle.SelectionBackColor = Color.FromArgb(26, 115, 232);
            this.dgvLogs.DefaultCellStyle.SelectionForeColor = Color.White;

            // Add controls
            this.Controls.Add(this.pnlHeader);
            this.Controls.Add(this.pnlFilter);
            this.Controls.Add(this.dgvLogs);

            ((System.ComponentModel.ISupportInitialize)(this.dgvLogs)).EndInit();
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.pnlFilter.ResumeLayout(false);
            this.pnlFilter.PerformLayout();
            this.ResumeLayout(false);
        }

        private void LoadLogs()
        {
            try
            {
                _allLogs = _auditLogRepository.GetLogs(200);
                
                // İşlem tiplerini yükle
                var actionTypes = new HashSet<string>();
                actionTypes.Add("TÜMÜ");
                foreach (var log in _allLogs)
                {
                    if (!string.IsNullOrEmpty(log.ActionType))
                    {
                        actionTypes.Add(log.ActionType);
                    }
                }

                cmbActionType.Items.Clear();
                foreach (var t in actionTypes)
                {
                    cmbActionType.Items.Add(t);
                }
                cmbActionType.SelectedIndex = 0;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Sistem logları yüklenirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_allLogs == null) return;

            string searchUser = txtSearchUser.Text.Trim().ToLower();
            string selectedType = cmbActionType.SelectedItem != null ? cmbActionType.SelectedItem.ToString() : "TÜMÜ";

            DataTable dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Username", typeof(string));
            dt.Columns.Add("ActionTime", typeof(DateTime));
            dt.Columns.Add("ActionType", typeof(string));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("OldValue", typeof(string));
            dt.Columns.Add("NewValue", typeof(string));

            foreach (var log in _allLogs)
            {
                // Filtreleme mantığı
                if (!string.IsNullOrEmpty(searchUser) && !log.Username.ToLower().Contains(searchUser))
                {
                    continue;
                }

                if (selectedType != "TÜMÜ" && log.ActionType != selectedType)
                {
                    continue;
                }

                dt.Rows.Add(
                    log.Id,
                    log.Username,
                    log.ActionTime,
                    log.ActionType,
                    log.Description,
                    log.OldValue ?? "",
                    log.NewValue ?? ""
                );
            }

            dgvLogs.DataSource = dt;

            // Başlıkları Türkçeleştir
            dgvLogs.Columns["Id"].HeaderText = "Log ID";
            dgvLogs.Columns["Id"].Width = 60;
            dgvLogs.Columns["Username"].HeaderText = "Kullanıcı";
            dgvLogs.Columns["Username"].Width = 80;
            dgvLogs.Columns["ActionTime"].HeaderText = "Tarih / Saat";
            dgvLogs.Columns["ActionTime"].Width = 120;
            dgvLogs.Columns["ActionType"].HeaderText = "İşlem Tipi";
            dgvLogs.Columns["ActionType"].Width = 130;
            dgvLogs.Columns["Description"].HeaderText = "Açıklama";
            dgvLogs.Columns["Description"].Width = 200;
            dgvLogs.Columns["OldValue"].HeaderText = "Eski Değer";
            dgvLogs.Columns["NewValue"].HeaderText = "Yeni Değer";
        }
    }
}
