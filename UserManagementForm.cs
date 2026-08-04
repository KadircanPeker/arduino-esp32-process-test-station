using System;
using System.Drawing;
using System.Windows.Forms;
using ProcessTestApp.Application;
using ProcessTestApp.Data;
using ProcessTestApp.Domain;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp
{
    public class UserManagementForm : Form
    {
        private readonly IUserRepository _userRepository;
        private TextBox _username;
        private TextBox _fullName;
        private TextBox _password;
        private ComboBox _role;

        public UserManagementForm(IUserRepository userRepository)
        {
            _userRepository = userRepository;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Kullanıcı ve Operatör Yönetimi";
            ClientSize = new Size(450, 335);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(24, 30, 42);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            Controls.Add(new Label { Text = "YENİ PERSONEL HESABI", Location = new Point(25, 20), AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(56, 189, 248) });
            Controls.Add(new Label { Text = "Kullanıcı adı", Location = new Point(25, 70), AutoSize = true });
            Controls.Add(new Label { Text = "Ad soyad", Location = new Point(25, 112), AutoSize = true });
            Controls.Add(new Label { Text = "Parola", Location = new Point(25, 154), AutoSize = true });
            Controls.Add(new Label { Text = "Rol", Location = new Point(25, 196), AutoSize = true });

            _username = CreateTextBox(145, 66);
            _fullName = CreateTextBox(145, 108);
            _password = CreateTextBox(145, 150);
            _password.PasswordChar = '*';
            _role = new ComboBox { Location = new Point(145, 192), Size = new Size(260, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(38, 46, 61), ForeColor = Color.White };
            _role.Items.AddRange(new object[] { RoleNames.Operator, RoleNames.Supervisor, RoleNames.QualityEngineer, RoleNames.ProcessEngineer, RoleNames.Administrator });
            _role.SelectedIndex = 0;
            Controls.AddRange(new Control[] { _username, _fullName, _password, _role });

            var save = new Button { Text = "Kullanıcıyı Kaydet", Location = new Point(145, 245), Size = new Size(155, 36), BackColor = Color.FromArgb(34, 197, 94), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            save.Click += Save_Click;
            var close = new Button { Text = "Kapat", Location = new Point(310, 245), Size = new Size(95, 36), BackColor = Color.FromArgb(71, 85, 105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            close.Click += delegate { Close(); };
            Controls.AddRange(new Control[] { save, close });
        }

        private TextBox CreateTextBox(int x, int y)
        {
            var box = new TextBox { Location = new Point(x, y), Size = new Size(260, 25), BackColor = Color.FromArgb(38, 46, 61), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(box);
            return box;
        }

        private void Save_Click(object sender, EventArgs e)
        {
            if (SessionManager.CurrentUser == null || RoleNames.NormalizeRoleName(SessionManager.CurrentUser.Role) != RoleNames.Administrator)
            {
                MessageBox.Show("Bu işlem yalnızca Administrator rolü tarafından yapılabilir.", "Yetki Hatası", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            string username = _username.Text.Trim();
            string fullName = _fullName.Text.Trim();
            string password = _password.Text;
            string role = Convert.ToString(_role.SelectedItem);
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Tüm alanları doldurun.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!PasswordHasher.IsPasswordStrong(password))
            {
                MessageBox.Show("Parola en az 12 karakter olmalı; büyük harf, küçük harf ve rakam içermelidir.", "Zayıf Parola", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_userRepository.UserExists(username))
            {
                MessageBox.Show("Bu kullanıcı adı zaten kayıtlı.", "Kayıt Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_userRepository.Register(new User(username, password, fullName, role)))
            {
                var audit = new AuditLogRepository(new SqlConnectionFactory(null));
                audit.Add(new AuditLog(SessionManager.LoggedInUsername, "USER_CREATED", username + " / " + role, null, null));
                MessageBox.Show("Kullanıcı başarıyla oluşturuldu.", "Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _username.Clear();
                _fullName.Clear();
                _password.Clear();
            }
            else
            {
                MessageBox.Show("Kullanıcı kaydedilemedi.", "Kayıt Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
