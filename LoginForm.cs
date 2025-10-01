using System;
using System.Windows.Forms;
using AttendanceFingerprint.Database;

namespace AttendanceFingerprint
{
    public partial class LoginForm : Form
    {
        private readonly SQLiteDatabaseService _dbService;

        public string Username { get; private set; }
        public bool IsAdmin { get; private set; }
        public bool LoginSuccessful { get; private set; }

        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnCancel;
        private Label lblUsername;
        private Label lblPassword;
        private CheckBox chkRememberMe;

        public LoginForm()
        {
            InitializeComponent();
            _dbService = new SQLiteDatabaseService();
        }

        private void InitializeComponent()
        {
            this.txtUsername = new TextBox();
            this.txtPassword = new TextBox();
            this.btnLogin = new Button();
            this.btnCancel = new Button();
            this.lblUsername = new Label();
            this.lblPassword = new Label();
            this.chkRememberMe = new CheckBox();

            // lblUsername
            this.lblUsername.Location = new System.Drawing.Point(20, 20);
            this.lblUsername.Size = new System.Drawing.Size(100, 20);
            this.lblUsername.Text = "Usuario:";

            // txtUsername
            this.txtUsername.Location = new System.Drawing.Point(120, 20);
            this.txtUsername.Size = new System.Drawing.Size(200, 23);
            this.txtUsername.Text = "admin";

            // lblPassword
            this.lblPassword.Location = new System.Drawing.Point(20, 50);
            this.lblPassword.Size = new System.Drawing.Size(100, 20);
            this.lblPassword.Text = "Contraseña:";

            // txtPassword
            this.txtPassword.Location = new System.Drawing.Point(120, 50);
            this.txtPassword.Size = new System.Drawing.Size(200, 23);
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Text = "admin123";

            // chkRememberMe
            this.chkRememberMe.Location = new System.Drawing.Point(120, 80);
            this.chkRememberMe.Size = new System.Drawing.Size(200, 20);
            this.chkRememberMe.Text = "Recordar usuario";

            // btnLogin
            this.btnLogin.Location = new System.Drawing.Point(120, 110);
            this.btnLogin.Size = new System.Drawing.Size(90, 30);
            this.btnLogin.Text = "Ingresar";
            this.btnLogin.Click += new EventHandler(this.btnLogin_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(220, 110);
            this.btnCancel.Size = new System.Drawing.Size(90, 30);
            this.btnCancel.Text = "Cancelar";
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // Form
            this.ClientSize = new System.Drawing.Size(340, 160);
            this.Controls.AddRange(new Control[] {
                lblUsername, txtUsername, lblPassword, txtPassword,
                chkRememberMe, btnLogin, btnCancel
            });
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Iniciar Sesión";
            this.AcceptButton = btnLogin;
            this.CancelButton = btnCancel;
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Por favor complete ambos campos", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                if (_dbService.VerifyPassword(txtUsername.Text, txtPassword.Text))
                {
                    Username = txtUsername.Text;
                    IsAdmin = _dbService.IsUserAdmin(txtUsername.Text);
                    LoginSuccessful = true;

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Usuario o contraseña incorrectos", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtPassword.SelectAll();
                    txtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al verificar credenciales: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            LoginSuccessful = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}