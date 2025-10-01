using AttendanceFingerprint.Database;
using AttendanceFingerprint.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace AttendanceFingerprint
{
    public partial class Form1 : Form
    {
        private AttendanceService _attendanceService;
        private Button btnEnroll;
        private Button btnReports;
        private Label lblStatus;
        private Button btnManageEmployees;

        private string _currentUsername = null;
        private bool _isAdmin = false;

        public bool IsUserLoggedIn => _currentUsername != null;
        public bool IsUserAdmin => _isAdmin;
        public string CurrentUsername => _currentUsername;

        // Método para forzar logout
        public void Logout()
        {
            _currentUsername = null;
            _isAdmin = false;
            lblStatus.Text = "Modo público - Solo captura de huellas";
            UpdateUIBasedOnPermissions();
        }

        public Form1()
        {
            InitializeComponent();
            this.TopMost = true;
        }

        private void InitializeComponent()
        {
            this.btnEnroll = new Button();
            this.btnReports = new Button();
            this.lblStatus = new Label();

            // btnEnroll
            this.btnEnroll.Location = new System.Drawing.Point(20, 20);
            this.btnEnroll.Size = new System.Drawing.Size(100, 30);
            this.btnEnroll.Text = "Registrar";
            this.btnEnroll.Name = "btnEnroll";
            this.btnEnroll.Click += new EventHandler(this.btnEnroll_Click);

            // btnReports
            this.btnReports.Location = new System.Drawing.Point(130, 20);
            this.btnReports.Size = new System.Drawing.Size(100, 30);
            this.btnReports.Text = "Reportes";
            this.btnReports.Name = "btnReports";
            this.btnReports.Click += new EventHandler(this.btnReports_Click);

            // btnManageEmployees
            this.btnManageEmployees = new Button();
            this.btnManageEmployees.Location = new System.Drawing.Point(240, 20);
            this.btnManageEmployees.Size = new System.Drawing.Size(100, 30);
            this.btnManageEmployees.Text = "Empleados";
            this.btnManageEmployees.Name = "btnManageEmployees";
            this.btnManageEmployees.Click += new EventHandler(this.btnManageEmployees_Click);

            // lblStatus
            this.lblStatus.Location = new System.Drawing.Point(20, 60);
            this.lblStatus.Size = new System.Drawing.Size(300, 20);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Text = "Inicializando sistema...";

            // Form
            this.ClientSize = new System.Drawing.Size(350, 100);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnEnroll);
            this.Controls.Add(this.btnReports);
            this.Controls.Add(this.btnManageEmployees);
            this.Controls.Add(this.lblStatus);
            this.Text = "Sistema de Asistencia con Huella Digital";
            this.Load += new EventHandler(this.Form1_Load);
            this.FormClosing += new FormClosingEventHandler(this.Form1_FormClosing);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {

            try
            {
                // Verificar y copiar dependencias
                if (!CheckAndCopyDependencies())
                {
                    MessageBox.Show("Algunos archivos DLL no se pudieron copiar. Verifica la consola para más detalles.",
                        "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // VERIFICACIÓN CORREGIDA - NOMBRES EXACTOS
                string[] requiredDpDlls = {
            "DPFPDevNET.dll",
            "DPFPEngNET.dll",
            "DPFPGuiNET.dll",
            "DPFPShrNET.dll",
            "DPFPVerNET.dll"
        };

                string[] requiredSgDlls = {
            "sgbledev.dll",
            "sgfdusda.dll",
            "sgfpamx.dll",
            "sgfplib.dll",
            "sgwsqlib.dll"
        };

                List<string> missingDlls = new List<string>();

                foreach (string dll in requiredDpDlls)
                {
                    if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dll)))
                    {
                        missingDlls.Add(dll);
                    }
                }

                foreach (string dll in requiredSgDlls)
                {
                    if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dll)))
                    {
                        missingDlls.Add(dll);
                    }
                }

                if (missingDlls.Count > 0)
                {
                    MessageBox.Show($"Faltan los siguientes archivos DLL:\n{string.Join("\n", missingDlls)}\n\nAsegúrate de que estén en la carpeta Libs/ correspondiente.",
                        "Error de dependencias", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    btnEnroll.Enabled = false;
                    lblStatus.Text = "Error: Faltan archivos DLL";
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                using (var dbService = new SQLiteDatabaseService())
                {
                    dbService.CheckDatabaseState();
                }

                _attendanceService = new AttendanceService();
                bool initialized = await _attendanceService.InitializeDeviceAsync();

                if (!initialized)
                {
                    MessageBox.Show("No se detectó ningún lector de huellas compatible.");
                    btnEnroll.Enabled = false;
                }
                else
                {
                    lblStatus.Text = "Sistema listo - Monitoreando huellas";
                    lblStatus.ForeColor = System.Drawing.Color.Green;

                    // 🔽 INICIAR CAPTURA SOLO SI EL DISPOSITivo LO SOPORTA
                    var device = _attendanceService.GetDevice();
                    try
                    {
                        device.StartContinuousCapture();
                        Console.WriteLine("✅ Captura continua iniciada");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Dispositivo no soporta captura continua: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inicializando aplicación: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Ahora mostrar login (pero la captura ya está funcionando)
            ShowLoginForm();
        }

        private void ShowLoginForm()
        {
            using (var loginForm = new LoginForm())
            {
                if (loginForm.ShowDialog() == DialogResult.OK && loginForm.LoginSuccessful)
                {
                    _currentUsername = loginForm.Username;
                    _isAdmin = loginForm.IsAdmin;

                    lblStatus.Text = $"Conectado como: {_currentUsername}";

                    // Habilitar/deshabilitar funciones según permisos
                    UpdateUIBasedOnPermissions();
                }
                else
                {
                    // Modo público - solo captura de huellas
                    lblStatus.Text = "Modo público - Solo captura de huellas";
                    UpdateUIBasedOnPermissions();
                }
            }
        }

        private void UpdateUIBasedOnPermissions()
        {
            // Siempre habilitado - captura funciona en modo público
            // btnEnroll.Enabled = true; 

            // Funciones administrativas requieren login
            btnReports.Enabled = _isAdmin;
            btnManageEmployees.Enabled = _isAdmin;

            if (_currentUsername == null)
            {
                lblStatus.Text += " (Modo público)";
            }
            else if (!_isAdmin)
            {
                lblStatus.Text += " (Usuario limitado)";
            }
            else
            {
                lblStatus.Text += " (Administrador)";
            }
        }

        private bool CheckAndCopyDependencies()
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                bool allDllsCopied = true;

                // DLLs de DigitalPersona (nombres exactos)
                string[] dpDlls = {
            "DPFPDevNET.dll",
            "DPFPEngNET.dll",
            "DPFPGuiNET.dll",
            "DPFPShrNET.dll",
            "DPFPVerNET.dll"
        };

                // DLLs de SecuGen (nombres exactos)  
                string[] sgDlls = {
            "sgbledev.dll",
            "sgfdusda.dll",
            "sgfpamx.dll",
            "sgfplib.dll",
            "sgwsqlib.dll"
        };

                // Copiar DLLs de DigitalPersona
                string dpSourcePath = Path.Combine(Application.StartupPath, "Libs", "DigitalPersona");
                if (Directory.Exists(dpSourcePath))
                {
                    foreach (string dllName in dpDlls)
                    {
                        string sourceFile = Path.Combine(dpSourcePath, dllName);
                        string destFile = Path.Combine(appDirectory, dllName);

                        if (File.Exists(sourceFile) && !File.Exists(destFile))
                        {
                            try
                            {
                                File.Copy(sourceFile, destFile, true);
                                Console.WriteLine($"✅ Copiado: {dllName}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Error copiando {dllName}: {ex.Message}");
                                allDllsCopied = false;
                            }
                        }
                    }
                }

                // Copiar DLLs de SecuGen
                string sgSourcePath = Path.Combine(Application.StartupPath, "Libs", "SecuGen");
                if (Directory.Exists(sgSourcePath))
                {
                    foreach (string dllName in sgDlls)
                    {
                        string sourceFile = Path.Combine(sgSourcePath, dllName);
                        string destFile = Path.Combine(appDirectory, dllName);

                        if (File.Exists(sourceFile) && !File.Exists(destFile))
                        {
                            try
                            {
                                File.Copy(sourceFile, destFile, true);
                                Console.WriteLine($"✅ Copiado: {dllName}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Error copiando {dllName}: {ex.Message}");
                                allDllsCopied = false;
                            }
                        }
                    }
                }

                return allDllsCopied;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en CheckAndCopyDependencies: {ex.Message}");
                return false;
            }
        }

        private void btnReports_Click(object sender, EventArgs e)
        {
            var reportForm = new ReportsForm(_attendanceService);
            reportForm.ShowDialog(this); // Pasar 'this' como owner
        }

        private void btnManageEmployees_Click(object sender, EventArgs e)
        {
            if (!_isAdmin)
            {
                // Si no está logueado, pedir login
                ShowLoginForm();
                if (!_isAdmin) return;
            }

            var manageForm = new ManageEmployeesForm();
            manageForm.ShowDialog(this);
        }

        private void btnEnroll_Click(object sender, EventArgs e)
        {
            if (!IsUserLoggedIn) // Enrolar requiere login básico
            {
                ShowLoginForm();
                if (!IsUserLoggedIn) return;
            }

            var enrollForm = new EnrollForm(_attendanceService);
            enrollForm.ShowDialog(this);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _attendanceService?.Dispose();
        }

    }
}