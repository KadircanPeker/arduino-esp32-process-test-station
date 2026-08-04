using System;
using System.Drawing;
using System.Windows.Forms;
using ProcessTestApp.Domain;
using ProcessTestApp.Data;
using ProcessTestApp.Infrastructure;

namespace ProcessTestApp
{
    public class FirstRunAdminForm : Form
    {
        private TextBox txtUsername;
        private TextBox txtFullName;
        private TextBox txtPassword;
        private TextBox txtPasswordConfirm;
        private Button btnCreate;
        private Button btnCancel;
        private IUserRepository _userRepository;

        public FirstRunAdminForm(IUserRepository userRepository)
        {
            _userRepository = userRepository;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "İlk Yönetici Hesabı Oluşturma";
            this.Size = new Size(400, 320);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblInfo = new Label()
            {
                Text = "Sistemde kayıtlı bir Yönetici (Administrator) hesabı bulunamadı. Lütfen ilk yönetici hesabını oluşturun.",
                Location = new Point(20, 15),
                Size = new Size(340, 45),
                ForeColor = Color.DarkSlateBlue,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            Label lblUsername = new Label() { Text = "Kullanıcı Adı:", Location = new Point(20, 75), Size = new Size(120, 20) };
            txtUsername = new TextBox() { Location = new Point(150, 72), Size = new Size(210, 23) };

            Label lblFullName = new Label() { Text = "Ad Soyad:", Location = new Point(20, 110), Size = new Size(120, 20) };
            txtFullName = new TextBox() { Location = new Point(150, 107), Size = new Size(210, 23) };

            Label lblPassword = new Label() { Text = "Parola:", Location = new Point(20, 145), Size = new Size(120, 20) };
            txtPassword = new TextBox() { Location = new Point(150, 142), Size = new Size(210, 23), PasswordChar = '*' };

            Label lblConfirm = new Label() { Text = "Parola Tekrar:", Location = new Point(20, 180), Size = new Size(120, 20) };
            txtPasswordConfirm = new TextBox() { Location = new Point(150, 177), Size = new Size(210, 23), PasswordChar = '*' };

            btnCreate = new Button() { Text = "Oluştur", Location = new Point(170, 225), Size = new Size(90, 30), DialogResult = DialogResult.None };
            btnCreate.Click += BtnCreate_Click;

            btnCancel = new Button() { Text = "İptal", Location = new Point(270, 225), Size = new Size(90, 30) };
            btnCancel.Click += (s, e) => { this.Close(); };

            this.Controls.Add(lblInfo);
            this.Controls.Add(lblUsername);
            this.Controls.Add(txtUsername);
            this.Controls.Add(lblFullName);
            this.Controls.Add(txtFullName);
            this.Controls.Add(lblPassword);
            this.Controls.Add(txtPassword);
            this.Controls.Add(lblConfirm);
            this.Controls.Add(txtPasswordConfirm);
            this.Controls.Add(btnCreate);
            this.Controls.Add(btnCancel);
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string fullName = txtFullName.Text.Trim();
            string password = txtPassword.Text;
            string confirm = txtPasswordConfirm.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Tüm alanları doldurmanız gerekmektedir.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (password != confirm)
            {
                MessageBox.Show("Girilen parolalar eşleşmiyor.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!PasswordHasher.IsPasswordStrong(password))
            {
                MessageBox.Show("Parola en az 12 karakter olmalı; büyük harf, küçük harf ve rakam içermelidir.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var adminUser = new User(username, password, fullName, RoleNames.Administrator);

            if (_userRepository.Register(adminUser))
            {
                MessageBox.Show("Yönetici hesabı başarıyla oluşturuldu.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Kayıt oluşturulurken bir hata meydana geldi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
