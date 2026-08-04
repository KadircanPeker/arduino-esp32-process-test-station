using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProcessTestApp
{
    public class LoginForm : Form
    {
        private Label lblTitle;
        private Label lblUsername;
        private TextBox txtUsername;
        private Label lblPassword;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnCancel;

        // Giriş yapan kullanıcının bilgileri
        public static string LoggedInUserFullName
        {
            get { return Application.SessionManager.LoggedInUserFullName; }
        }
        public static string LoggedInUserRole
        {
            get { return Application.SessionManager.LoggedInUserRole; }
        }
        public static string LoggedInUsername
        {
            get { return Application.SessionManager.LoggedInUsername; }
        }

        public LoginForm()
        {
            InitializeComponent();
            
        }

        private void InitializeComponent()
        {
            this.lblTitle = new Label();
            this.lblUsername = new Label();
            this.txtUsername = new TextBox();
            this.lblPassword = new Label();
            this.txtPassword = new TextBox();
            this.btnLogin = new Button();
            this.btnCancel = new Button();
            this.SuspendLayout();

            // ==========================================
            // GENEL FORM TASARIMI (DARK THEME)
            // ==========================================
            this.BackColor = Color.FromArgb(32, 33, 36); // Koyu antrasit
            this.ForeColor = Color.White;
            this.ClientSize = new Size(380, 235);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Kullanıcı Girişi - Arduino / ESP32 Test İstasyonu";

            // lblTitle (Başlık)
            this.lblTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            this.lblTitle.ForeColor = Color.FromArgb(26, 115, 232); // Mavi Accent
            this.lblTitle.Location = new Point(15, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(350, 30);
            this.lblTitle.Text = "PROSES TEST VE İZLENEBİLİRLİK";
            this.lblTitle.TextAlign = ContentAlignment.MiddleCenter;

            // lblUsername (Kullanıcı Adı Etiketi)
            this.lblUsername.Font = new Font("Segoe UI", 9F);
            this.lblUsername.Location = new Point(20, 55);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new Size(150, 18);
            this.lblUsername.Text = "Kullanıcı Adı:";

            // txtUsername (Kullanıcı Adı Kutusu)
            this.txtUsername.Location = new Point(20, 75);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new Size(340, 25);
            this.txtUsername.BackColor = Color.FromArgb(43, 44, 47);
            this.txtUsername.ForeColor = Color.White;
            this.txtUsername.BorderStyle = BorderStyle.FixedSingle;
            this.txtUsername.Font = new Font("Segoe UI", 9.5F);

            // lblPassword (Şifre Etiketi)
            this.lblPassword.Font = new Font("Segoe UI", 9F);
            this.lblPassword.Location = new Point(20, 110);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new Size(150, 18);
            this.lblPassword.Text = "Şifre:";

            // txtPassword (Şifre Giriş Kutusu)
            this.txtPassword.Location = new Point(20, 130);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new Size(340, 25);
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.BackColor = Color.FromArgb(43, 44, 47);
            this.txtPassword.ForeColor = Color.White;
            this.txtPassword.BorderStyle = BorderStyle.FixedSingle;
            this.txtPassword.Font = new Font("Segoe UI", 9.5F);

            // btnLogin (Giriş Yap Butonu - Mavi)
            this.btnLogin.Location = new Point(20, 180);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new Size(160, 32);
            this.btnLogin.Text = "Giriş Yap";
            this.btnLogin.FlatStyle = FlatStyle.Flat;
            this.btnLogin.FlatAppearance.BorderSize = 0;
            this.btnLogin.BackColor = Color.FromArgb(26, 115, 232);
            this.btnLogin.ForeColor = Color.White;
            this.btnLogin.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            this.btnLogin.UseVisualStyleBackColor = false;
            this.btnLogin.Click += new EventHandler(this.btnLogin_Click);

            // btnCancel (İptal Butonu - Gri)
            this.btnCancel.Location = new Point(200, 180);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(160, 32);
            this.btnCancel.Text = "İptal";
            this.btnCancel.FlatStyle = FlatStyle.Flat;
            this.btnCancel.FlatAppearance.BorderSize = 0;
            this.btnCancel.BackColor = Color.FromArgb(128, 134, 139);
            this.btnCancel.ForeColor = Color.White;
            this.btnCancel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // Accept Button (Enter'a basınca giriş yapması için)
            this.AcceptButton = this.btnLogin;

            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.btnCancel);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Kullanıcı adı ve şifre alanları boş bırakılamaz!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var connectionFactory = new Data.SqlConnectionFactory(null);
                var userRepository = new Data.UserRepository(connectionFactory);

                var user = userRepository.Authenticate(username, password);

                if (user != null)
                {
                    Application.SessionManager.SetSession(user);

                    // Audit Log tablosuna başarılı girişi kaydet
                    var auditRepo = new Data.AuditLogRepository(connectionFactory);
                    auditRepo.Add(new Domain.AuditLog(username, "USER_LOGIN_SUCCESS", "Kullanici basariyla giris yapti: " + user.FullName, null, null));

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    // Audit Log tablosuna başarısız girişi kaydet (Güvenlik analitiği için!)
                    var auditRepo = new Data.AuditLogRepository(connectionFactory);
                    auditRepo.Add(new Domain.AuditLog(username, "USER_LOGIN_FAIL", "Kullanici girisi basarisiz: sifre uyusmadi", null, null));

                    MessageBox.Show("Hatalı kullanıcı adı veya şifre!", "Giriş Başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Giriş doğrulaması sırasında hata oluştu:\n" + ex.Message, "SQL Doğrulama Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

    }
}
