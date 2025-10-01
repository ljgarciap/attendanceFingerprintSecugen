using System;
using System.Windows.Forms;
using AttendanceFingerprint.Database;
using System.Linq;

namespace AttendanceFingerprint
{
    public partial class ManageEmployeesForm : Form
    {
        private DataGridView dgvEmployees;
        private Button btnNew;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnClose;

        public ManageEmployeesForm()
        {
            // Verificar permisos
            using (var dbService = new SQLiteDatabaseService())
            {
                // Aquí deberías pasar el username del usuario logueado
                // Puedes guardarlo en una variable estática o en Form1
                if (!dbService.IsUserAdmin("admin")) // Temporal - mejorar esto
                {
                    MessageBox.Show("No tiene permisos para acceder a reportes", "Acceso denegado",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Close();
                    return;
                }
            }

            InitializeComponent();
            LoadEmployees();
        }

        private void InitializeComponent()
        {
            this.dgvEmployees = new DataGridView();
            this.btnNew = new Button();
            this.btnEdit = new Button();
            this.btnDelete = new Button();
            this.btnClose = new Button();

            // dgvEmployees
            this.dgvEmployees.Location = new System.Drawing.Point(20, 20);
            this.dgvEmployees.Size = new System.Drawing.Size(600, 300);
            this.dgvEmployees.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvEmployees.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvEmployees.ReadOnly = true;

            // Configurar columnas
            this.dgvEmployees.Columns.Add("Id", "ID");
            this.dgvEmployees.Columns.Add("FirstName", "Nombre");
            this.dgvEmployees.Columns.Add("LastName", "Apellido");
            this.dgvEmployees.Columns.Add("Identification", "Identificación");
            this.dgvEmployees.Columns.Add("IsActive", "Activo");

            // Botones
            this.btnNew.Location = new System.Drawing.Point(20, 330);
            this.btnNew.Size = new System.Drawing.Size(100, 30);
            this.btnNew.Text = "Nuevo";
            this.btnNew.Click += new EventHandler(this.btnNew_Click);

            this.btnEdit.Location = new System.Drawing.Point(130, 330);
            this.btnEdit.Size = new System.Drawing.Size(100, 30);
            this.btnEdit.Text = "Editar";
            this.btnEdit.Click += new EventHandler(this.btnEdit_Click);

            this.btnDelete.Location = new System.Drawing.Point(240, 330);
            this.btnDelete.Size = new System.Drawing.Size(100, 30);
            this.btnDelete.Text = "Eliminar";
            this.btnDelete.Click += new EventHandler(this.btnDelete_Click);

            this.btnClose.Location = new System.Drawing.Point(520, 330);
            this.btnClose.Size = new System.Drawing.Size(100, 30);
            this.btnClose.Text = "Cerrar";
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // Form
            this.ClientSize = new System.Drawing.Size(640, 380);
            this.Controls.AddRange(new Control[] {
                this.dgvEmployees, this.btnNew, this.btnEdit, this.btnDelete, this.btnClose
            });
            this.Text = "Gestión de Empleados";
        }

        private void LoadEmployees()
        {
            dgvEmployees.Rows.Clear();

            using (var dbService = new SQLiteDatabaseService())
            {
                var employees = dbService.GetEmployees();

                foreach (var employee in employees)
                {
                    dgvEmployees.Rows.Add(
                        employee.Id,
                        employee.FirstName,
                        employee.LastName,
                        employee.Identification,
                        employee.IsActive ? "Sí" : "No"
                    );
                }
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            var editForm = new EditEmployeeForm();
            if (editForm.ShowDialog() == DialogResult.OK)
            {
                LoadEmployees(); // Recargar lista
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleccione un empleado para editar");
                return;
            }

            int employeeId = Convert.ToInt32(dgvEmployees.SelectedRows[0].Cells["Id"].Value);

            using (var dbService = new SQLiteDatabaseService())
            {
                var employee = dbService.GetEmployee(employeeId);
                if (employee != null)
                {
                    var editForm = new EditEmployeeForm(employee);
                    editForm.StartPosition = FormStartPosition.CenterParent;

                    // SOLAMENTE esto - ShowDialog ya maneja el modal correctamente
                    if (editForm.ShowDialog(this) == DialogResult.OK)
                    {
                        LoadEmployees();
                    }
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleccione un empleado para eliminar");
                return;
            }

            int employeeId = Convert.ToInt32(dgvEmployees.SelectedRows[0].Cells["Id"].Value);
            string employeeName = dgvEmployees.SelectedRows[0].Cells["FirstName"].Value + " " +
                                 dgvEmployees.SelectedRows[0].Cells["LastName"].Value;

            var result = MessageBox.Show($"¿Está seguro de eliminar al empleado: {employeeName}?",
                "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                using (var dbService = new SQLiteDatabaseService())
                {
                    try
                    {
                        dbService.DeleteEmployee(employeeId);
                        MessageBox.Show("✅ Empleado eliminado exitosamente");
                        LoadEmployees(); // Recargar lista
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"❌ Error al eliminar: {ex.Message}");
                    }
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}