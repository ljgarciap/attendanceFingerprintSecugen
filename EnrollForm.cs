using System;
using System.Linq;
using System.Windows.Forms;
using AttendanceFingerprint.Services;
using AttendanceFingerprint.Database;
using AttendanceFingerprint.Models;

namespace AttendanceFingerprint
{
    public class EnrollForm : Form
    {
        private readonly AttendanceService _attendanceService;
        private EnrollmentService _enrollmentService;
        private int _currentEmployeeId;

        private TextBox txtFirstName;
        private TextBox txtLastName;
        private TextBox txtIdentification;
        private Button btnSave;
        private Button btnEnrollFingerprint;
        private ProgressBar progressBar;
        private Label labelProgress;
        private Label label1;
        private Label label2;
        private Label label3;

        public EnrollForm(AttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
            _enrollmentService = new EnrollmentService();
            InitializeComponent();

            // Configurar eventos
            _enrollmentService.EnrollmentProgress += EnrollmentService_EnrollmentProgress;
            _enrollmentService.EnrollmentCompleted += EnrollmentService_EnrollmentCompleted;
        }

        private void InitializeComponent()
        {
            // Configurar el formulario primero
            this.Text = "Registrar Empleado";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new System.Drawing.Size(380, 230);

            // Crear y configurar controles
            this.txtFirstName = new TextBox();
            this.txtLastName = new TextBox();
            this.txtIdentification = new TextBox();
            this.btnSave = new Button();
            this.btnEnrollFingerprint = new Button();
            this.progressBar = new ProgressBar();
            this.labelProgress = new Label();
            this.label1 = new Label();
            this.label2 = new Label();
            this.label3 = new Label();

            // txtFirstName
            this.txtFirstName.Location = new System.Drawing.Point(100, 20);
            this.txtFirstName.Size = new System.Drawing.Size(200, 23);
            this.txtFirstName.Name = "txtFirstName";

            // txtLastName
            this.txtLastName.Location = new System.Drawing.Point(100, 50);
            this.txtLastName.Size = new System.Drawing.Size(200, 23);
            this.txtLastName.Name = "txtLastName";

            // txtIdentification
            this.txtIdentification.Location = new System.Drawing.Point(100, 80);
            this.txtIdentification.Size = new System.Drawing.Size(200, 23);
            this.txtIdentification.Name = "txtIdentification";

            // btnSave
            this.btnSave.Location = new System.Drawing.Point(100, 120);
            this.btnSave.Size = new System.Drawing.Size(100, 30);
            this.btnSave.Text = "Guardar";
            this.btnSave.Name = "btnSave";
            this.btnSave.Click += new EventHandler(this.btnSave_Click);

            // btnEnrollFingerprint
            this.btnEnrollFingerprint.Location = new System.Drawing.Point(210, 120);
            this.btnEnrollFingerprint.Size = new System.Drawing.Size(150, 30);
            this.btnEnrollFingerprint.Text = "Registrar Huella";
            this.btnEnrollFingerprint.Name = "btnEnrollFingerprint";
            this.btnEnrollFingerprint.Enabled = false;
            this.btnEnrollFingerprint.Click += new EventHandler(this.btnEnrollFingerprint_Click);

            // progressBar
            this.progressBar.Location = new System.Drawing.Point(100, 160);
            this.progressBar.Size = new System.Drawing.Size(260, 23);
            this.progressBar.Name = "progressBar";
            this.progressBar.Visible = false;

            // labelProgress
            this.labelProgress.Location = new System.Drawing.Point(100, 190);
            this.labelProgress.Size = new System.Drawing.Size(260, 30);
            this.labelProgress.Name = "labelProgress";
            this.labelProgress.Text = "";
            this.labelProgress.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // Labels
            this.label1.Location = new System.Drawing.Point(20, 23);
            this.label1.Text = "Nombre:";
            this.label1.Size = new System.Drawing.Size(70, 15);
            this.label1.Name = "label1";

            this.label2.Location = new System.Drawing.Point(20, 53);
            this.label2.Text = "Apellido:";
            this.label2.Size = new System.Drawing.Size(70, 15);
            this.label2.Name = "label2";

            this.label3.Location = new System.Drawing.Point(20, 83);
            this.label3.Text = "Identificación:";
            this.label3.Size = new System.Drawing.Size(70, 15);
            this.label3.Name = "label3";

            // Agregar controles al formulario
            this.Controls.Add(this.txtFirstName);
            this.Controls.Add(this.txtLastName);
            this.Controls.Add(this.txtIdentification);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnEnrollFingerprint);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.labelProgress);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label3);
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirstName.Text) ||
                string.IsNullOrWhiteSpace(txtLastName.Text) ||
                string.IsNullOrWhiteSpace(txtIdentification.Text))
            {
                MessageBox.Show("Por favor complete todos los campos");
                return;
            }

            try
            {
                using (var dbService = new SQLiteDatabaseService())
                {
                    // Verificar duplicados
                    var employees = dbService.GetEmployees();
                    if (employees.Any(emp => emp.Identification == txtIdentification.Text.Trim()))
                    {
                        MessageBox.Show("❌ Ya existe un empleado con esta identificación");
                        return;
                    }

                    var employee = new Employee
                    {
                        FirstName = txtFirstName.Text.Trim(),
                        LastName = txtLastName.Text.Trim(),
                        Identification = txtIdentification.Text.Trim()
                    };

                    this.Enabled = false;
                    btnSave.Text = "Registrando...";

                    int employeeId = dbService.SaveEmployee(employee);
                    _currentEmployeeId = employeeId;

                    if (employeeId > 0)
                    {
                        MessageBox.Show("✅ Empleado registrado exitosamente. Ahora puede registrar su huella digital.");
                        btnEnrollFingerprint.Enabled = true;
                        btnSave.Enabled = false;
                    }
                    else
                    {
                        MessageBox.Show("❌ Error al registrar empleado");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error al registrar empleado: {ex.Message}");
                Console.WriteLine($"❌ Error detallado: {ex.ToString()}");
            }
            finally
            {
                this.Enabled = true;
                btnSave.Text = "Guardar";
            }
        }

        private async void btnEnrollFingerprint_Click(object sender, EventArgs e)
        {
            if (_currentEmployeeId <= 0)
            {
                MessageBox.Show("❌ Primero debe guardar el empleado");
                return;
            }

            try
            {
                progressBar.Visible = true;
                progressBar.Value = 0;
                labelProgress.Text = "Preparando enrolamiento...";
                btnEnrollFingerprint.Enabled = false;

                Console.WriteLine($"🔹 Iniciando enrolamiento para usuario ID: {_currentEmployeeId}");

                // Pausar monitoreo general antes de enrolar
                _attendanceService.PauseMonitoring();

                var device = _attendanceService.GetDevice();

                if (device == null)
                {
                    MessageBox.Show("❌ No hay dispositivo disponible");
                    ResetEnrollmentUI();
                    return;
                }

                // Suscribir a eventos
                device.OnEnrollmentProgress += Device_OnEnrollmentProgress;
                device.OnEnrollmentCompleted += Device_OnEnrollmentCompleted;

                bool enrollmentStarted = await device.EnrollAsync(_currentEmployeeId);

                if (!enrollmentStarted)
                {
                    MessageBox.Show("❌ No se pudo iniciar el enrolamiento");
                    ResetEnrollmentUI();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error iniciando enrolamiento: {ex.Message}");
                Console.WriteLine($"❌ Error detallado: {ex.ToString()}");
                ResetEnrollmentUI();
                _attendanceService.ResumeMonitoring();
            }
        }

        private void Device_OnEnrollmentProgress(object sender, EnrollmentEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => {
                    progressBar.Value = e.Progress;
                    labelProgress.Text = $"Progreso: {e.Progress}% - {e.Message}";
                }));
            }
            else
            {
                progressBar.Value = e.Progress;
                labelProgress.Text = $"Progreso: {e.Progress}% - {e.Message}";
            }
        }

        private void Device_OnEnrollmentCompleted(object sender, EnrollmentEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ProcessEnrollmentCompletion(e)));
            }
            else
            {
                ProcessEnrollmentCompletion(e);
            }
        }

        private void ProcessEnrollmentCompletion(EnrollmentEventArgs e)
        {
            try
            {
                if (e.Success && !string.IsNullOrEmpty(e.TemplateData))
                {
                    using (var db = new SQLiteDatabaseService())
                    {
                        db.SaveFingerprintTemplate(_currentEmployeeId, e.TemplateData);
                    }
                    MessageBox.Show("✅ Huella enrolada exitosamente");
                    this.Close();
                }
                else
                {
                    MessageBox.Show($"❌ Error en enrolamiento: {e.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error guardando template: {ex.Message}");
            }
            finally
            {
                ResetEnrollmentUI();
                _attendanceService.ResumeMonitoring();

                // Desuscribir eventos
                var device = _attendanceService.GetDevice();
                if (device != null)
                {
                    device.OnEnrollmentProgress -= Device_OnEnrollmentProgress;
                    device.OnEnrollmentCompleted -= Device_OnEnrollmentCompleted;
                }
            }
        }

        private void ResetEnrollmentUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ResetEnrollmentUI));
                return;
            }

            progressBar.Visible = false;
            labelProgress.Text = "";
            btnEnrollFingerprint.Enabled = true;
        }

        private void EnrollmentService_EnrollmentProgress(object sender, EnrollmentEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, EnrollmentEventArgs>(EnrollmentService_EnrollmentProgress), sender, e);
                return;
            }

            progressBar.Value = e.Progress;
            labelProgress.Text = e.Message;
        }

        private void EnrollmentService_EnrollmentCompleted(object sender, EnrollmentEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, EnrollmentEventArgs>(EnrollmentService_EnrollmentCompleted), sender, e);
                return;
            }

            progressBar.Visible = false;
            labelProgress.Text = "";

            if (e.Success)
            {
                MessageBox.Show("Huella registrada exitosamente");
                this.Close();
            }
            else
            {
                MessageBox.Show("Error al registrar la huella: " + e.Message);
                btnEnrollFingerprint.Enabled = true;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _enrollmentService?.Dispose();
        }
    }
}