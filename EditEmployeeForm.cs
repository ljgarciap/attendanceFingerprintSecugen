using AttendanceFingerprint.Database;
using AttendanceFingerprint.Models;
using System;
using System.Linq;
using System.Windows.Forms;

namespace AttendanceFingerprint
{
    public partial class EditEmployeeForm : Form
    {
        private readonly SQLiteDatabaseService _dbService;
        private readonly Employee _employee;
        private readonly bool _isEditMode;

        private TextBox txtFirstName;
        private TextBox txtLastName;
        private TextBox txtIdentification;
        private CheckBox chkIsActive;
        private Button btnSave;
        private Button btnCancel;
        private Button btnDelete;
        private Label label1;
        private Label label2;
        private Label label3;

        public EditEmployeeForm(Employee employee = null)
        {
            InitializeComponent();
            _dbService = new SQLiteDatabaseService();
            _employee = employee;
            _isEditMode = employee != null;

            if (_isEditMode)
            {
                LoadEmployeeData();
                this.Text = "Editar Empleado";
                btnDelete.Visible = true;
            }
            else
            {
                this.Text = "Nuevo Empleado";
                btnDelete.Visible = false;
            }
        }

        private void InitializeComponent()
        {
            this.txtFirstName = new TextBox();
            this.txtLastName = new TextBox();
            this.txtIdentification = new TextBox();
            this.chkIsActive = new CheckBox();
            this.btnSave = new Button();
            this.btnCancel = new Button();
            this.btnDelete = new Button();
            this.label1 = new Label();
            this.label2 = new Label();
            this.label3 = new Label();

            // txtFirstName
            this.txtFirstName.Location = new System.Drawing.Point(120, 20);
            this.txtFirstName.Size = new System.Drawing.Size(200, 23);
            this.txtFirstName.Name = "txtFirstName";

            // txtLastName
            this.txtLastName.Location = new System.Drawing.Point(120, 50);
            this.txtLastName.Size = new System.Drawing.Size(200, 23);
            this.txtLastName.Name = "txtLastName";

            // txtIdentification
            this.txtIdentification.Location = new System.Drawing.Point(120, 80);
            this.txtIdentification.Size = new System.Drawing.Size(200, 23);
            this.txtIdentification.Name = "txtIdentification";

            // chkIsActive
            this.chkIsActive.Location = new System.Drawing.Point(120, 110);
            this.chkIsActive.Size = new System.Drawing.Size(100, 23);
            this.chkIsActive.Text = "Activo";
            this.chkIsActive.Checked = true;

            // btnSave
            this.btnSave.Location = new System.Drawing.Point(120, 140);
            this.btnSave.Size = new System.Drawing.Size(80, 30);
            this.btnSave.Text = "Guardar";
            this.btnSave.Click += new EventHandler(this.btnSave_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(210, 140);
            this.btnCancel.Size = new System.Drawing.Size(80, 30);
            this.btnCancel.Text = "Cancelar";
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // btnDelete
            this.btnDelete.Location = new System.Drawing.Point(300, 140);
            this.btnDelete.Size = new System.Drawing.Size(80, 30);
            this.btnDelete.Text = "Eliminar";
            this.btnDelete.BackColor = System.Drawing.Color.LightCoral;
            this.btnDelete.Click += new EventHandler(this.btnDelete_Click);

            // Labels
            this.label1.Location = new System.Drawing.Point(20, 23);
            this.label1.Text = "Nombre:";
            this.label1.Size = new System.Drawing.Size(80, 15);

            this.label2.Location = new System.Drawing.Point(20, 53);
            this.label2.Text = "Apellido:";
            this.label2.Size = new System.Drawing.Size(80, 15);

            this.label3.Location = new System.Drawing.Point(20, 83);
            this.label3.Text = "Identificación:";
            this.label3.Size = new System.Drawing.Size(80, 15);

            // Form
            this.ClientSize = new System.Drawing.Size(400, 190);
            this.Controls.AddRange(new Control[] {
                this.txtFirstName, this.txtLastName, this.txtIdentification, this.chkIsActive,
                this.btnSave, this.btnCancel, this.btnDelete,
                this.label1, this.label2, this.label3
            });
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
        }

        private void LoadEmployeeData()
        {
            txtFirstName.Text = _employee.FirstName;
            txtLastName.Text = _employee.LastName;
            txtIdentification.Text = _employee.Identification;
            chkIsActive.Checked = _employee.IsActive;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirstName.Text) ||
                string.IsNullOrWhiteSpace(txtLastName.Text) ||
                string.IsNullOrWhiteSpace(txtIdentification.Text))
            {
                Toast.Show("Por favor complete todos los campos", ToastType.Warning);
                return;
            }

            try
            {
                var employee = new Employee
                {
                    FirstName = txtFirstName.Text.Trim(),
                    LastName = txtLastName.Text.Trim(),
                    Identification = txtIdentification.Text.Trim(),
                    IsActive = chkIsActive.Checked
                };

                using (var dbService = new SQLiteDatabaseService())
                {
                    if (_isEditMode)
                    {
                        // MODO EDICIÓN: Verificar si la identificación cambió y si ya existe
                        employee.Id = _employee.Id;

                        if (employee.Identification != _employee.Identification)
                        {
                            // La identificación fue modificada, verificar que no exista
                            var existing = dbService.GetEmployees()
                                .FirstOrDefault(emp =>
                                    emp.Identification == employee.Identification &&
                                    emp.Id != employee.Id);

                            if (existing != null)
                            {
                                Toast.Show("Ya existe otro empleado con esta identificación", ToastType.Warning);
                                return;
                            }
                        }

                        bool success = dbService.UpdateEmployee(employee);
                        if (success)
                        {
                            Toast.Show("Empleado actualizado exitosamente", ToastType.Success);
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        else
                        {
                            Toast.Show("No se pudo actualizar el empleado", ToastType.Error);
                        }
                    }
                    else
                    {
                        // MODO NUEVO: Verificar que no exista la identificación
                        var existing = dbService.GetEmployees()
                            .FirstOrDefault(emp => emp.Identification == employee.Identification);

                        if (existing != null)
                        {
                            Toast.Show("Ya existe un empleado con esta identificación", ToastType.Warning);
                            return;
                        }

                        int newId = dbService.SaveEmployee(employee);
                        if (newId > 0)
                        {
                            Toast.Show("Empleado creado exitosamente", ToastType.Success);
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        else
                        {
                            Toast.Show("No se pudo crear el empleado", ToastType.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Toast.Show($"Error: {ex.Message}", ToastType.Error);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("¿Está seguro de eliminar este empleado? Se eliminarán también sus registros de asistencia y huellas.",
                "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    _dbService.DeleteEmployee(_employee.Id);
                    MessageBox.Show("✅ Empleado eliminado exitosamente");
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Error al eliminar: {ex.Message}");
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}