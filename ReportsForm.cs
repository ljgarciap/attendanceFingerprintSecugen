using AttendanceFingerprint.Database;
using AttendanceFingerprint.Models;
using AttendanceFingerprint.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AttendanceFingerprint
{
    public partial class ReportsForm : Form
    {
        private readonly AttendanceService _attendanceService;

        private DataGridView dgvReport;
        private DateTimePicker dtpStartDate;
        private DateTimePicker dtpEndDate;
        private Button btnLoad;
        private Label label1;
        private Label label2;
        private Button btnExportExcel;
        private Button btnExportCsv;

        public ReportsForm(AttendanceService attendanceService)
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

            _attendanceService = attendanceService;
            InitializeComponent();
            LoadReport(dtpStartDate.Value, dtpEndDate.Value);
        }

        private void InitializeComponent()
        {
            this.dgvReport = new DataGridView();
            this.dtpStartDate = new DateTimePicker();
            this.dtpEndDate = new DateTimePicker();
            this.btnLoad = new Button();
            this.label1 = new Label();
            this.label2 = new Label();
            this.btnExportExcel = new Button();
            this.btnExportCsv = new Button();

            // Configurar controles de fecha
            this.dtpStartDate.Location = new System.Drawing.Point(100, 20);
            this.dtpStartDate.Size = new System.Drawing.Size(120, 23);
            this.dtpStartDate.Value = DateTime.Today.AddDays(-7);
            this.dtpStartDate.Format = DateTimePickerFormat.Short;

            this.dtpEndDate.Location = new System.Drawing.Point(280, 20);
            this.dtpEndDate.Size = new System.Drawing.Size(120, 23);
            this.dtpEndDate.Value = DateTime.Today;
            this.dtpEndDate.Format = DateTimePickerFormat.Short;

            // Botones
            this.btnLoad.Location = new System.Drawing.Point(410, 20);
            this.btnLoad.Size = new System.Drawing.Size(75, 23);
            this.btnLoad.Text = "Cargar";
            this.btnLoad.Click += new EventHandler(this.btnLoad_Click);

            this.btnExportExcel.Location = new System.Drawing.Point(490, 20);
            this.btnExportExcel.Size = new System.Drawing.Size(100, 23);
            this.btnExportExcel.Text = "Exportar PDF";
            this.btnExportExcel.BackColor = System.Drawing.Color.LightGreen;
            this.btnExportExcel.Click += new EventHandler(this.btnExportExcel_Click);

            this.btnExportCsv.Location = new System.Drawing.Point(595, 20);
            this.btnExportCsv.Size = new System.Drawing.Size(80, 23);
            this.btnExportCsv.Text = "Exportar CSV";
            this.btnExportCsv.Click += new EventHandler(this.btnExportCsv_Click);

            // Labels
            this.label1.Location = new System.Drawing.Point(20, 23);
            this.label1.Text = "Desde:";
            this.label1.Size = new System.Drawing.Size(70, 15);

            this.label2.Location = new System.Drawing.Point(230, 23);
            this.label2.Text = "Hasta:";
            this.label2.Size = new System.Drawing.Size(70, 15);

            // DataGridView
            this.dgvReport.Location = new System.Drawing.Point(20, 60);
            this.dgvReport.Size = new System.Drawing.Size(750, 300);
            this.dgvReport.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Configurar columnas
            this.dgvReport.Columns.Add("EmployeeId", "ID Empleado");
            this.dgvReport.Columns.Add("Identification", "Identificación");
            this.dgvReport.Columns.Add("EmployeeName", "Empleado");
            this.dgvReport.Columns.Add("RecordDate", "Fecha/Hora");
            this.dgvReport.Columns.Add("RecordType", "Tipo");

            this.ClientSize = new System.Drawing.Size(790, 380);
            this.Controls.AddRange(new Control[] {
                this.dtpStartDate, this.dtpEndDate, this.btnLoad,
                this.btnExportExcel, this.btnExportCsv,
                this.label1, this.label2, this.dgvReport
            });
            this.Text = "Reportes de Asistencia";
        }

        private void LoadReport(DateTime startDate, DateTime endDate)
        {
            dgvReport.Rows.Clear();

            try
            {
                var dbService = new SQLiteDatabaseService();

                // Obtener registros del rango de fechas
                var allRecords = new List<AttendanceRecord>();

                // Si el método no soporta rango, iterar día por día
                for (DateTime date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
                {
                    var dailyRecords = dbService.GetAttendanceRecords(date);
                    allRecords.AddRange(dailyRecords);
                }

                foreach (var record in allRecords.OrderBy(r => r.RecordDate))
                {
                    DateTime displayTime = record.RecordDate;
                    if (displayTime.Kind == DateTimeKind.Utc)
                    {
                        displayTime = displayTime.ToLocalTime();
                    }

                    dgvReport.Rows.Add(
                        record.EmployeeId,
                        record.Identification,
                        record.EmployeeName,
                        displayTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        record.RecordType.ToString()
                    );
                }

                this.Text = $"Reportes de Asistencia ({startDate:yyyy-MM-dd} al {endDate:yyyy-MM-dd}) - {allRecords.Count} registros";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando reporte: {ex.Message}");
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            LoadReport(dtpStartDate.Value, dtpEndDate.Value);
        }

        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Archivos PDF|*.pdf";
            saveFileDialog.Title = "Exportar a PDF";
            saveFileDialog.FileName = $"Reporte_Asistencia_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                ExportToPdf(saveFileDialog.FileName);
            }
        }

        private void ExportToPdf(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                {
                    using (Document document = new Document(PageSize.A4.Rotate(), 10f, 10f, 10f, 10f))
                    {
                        PdfWriter writer = PdfWriter.GetInstance(document, fs);
                        document.Open();

                        // Título
                        Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
                        Paragraph title = new Paragraph("REPORTE DE ASISTENCIA", titleFont);
                        title.Alignment = Element.ALIGN_CENTER;
                        title.SpacingAfter = 10f;
                        document.Add(title);

                        // Subtítulo - Período
                        Font subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 12, BaseColor.BLACK);
                        Paragraph subtitle = new Paragraph($"Período: {dtpStartDate.Value:dd/MM/yyyy} al {dtpEndDate.Value:dd/MM/yyyy}", subtitleFont);
                        subtitle.Alignment = Element.ALIGN_CENTER;
                        subtitle.SpacingAfter = 20f;
                        document.Add(subtitle);

                        // Fecha de generación
                        Font dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.DARK_GRAY);
                        Paragraph date = new Paragraph($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", dateFont);
                        date.Alignment = Element.ALIGN_CENTER;
                        date.SpacingAfter = 20f;
                        document.Add(date);

                        // Crear tabla
                        PdfPTable table = new PdfPTable(5);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 1f, 1.5f, 2f, 2f, 1f });

                        // Encabezados de tabla
                        Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                        string[] headers = { "ID Empleado", "Identificación", "Empleado", "Fecha/Hora", "Tipo" };

                        foreach (string header in headers)
                        {
                            PdfPCell cell = new PdfPCell(new Phrase(header, headerFont));
                            cell.BackgroundColor = new BaseColor(79, 129, 189); // Azul corporativo
                            cell.HorizontalAlignment = Element.ALIGN_CENTER;
                            cell.Padding = 5f;
                            table.AddCell(cell);
                        }

                        // Datos de la tabla
                        Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);

                        foreach (DataGridViewRow row in dgvReport.Rows)
                        {
                            if (!row.IsNewRow)
                            {
                                for (int col = 0; col < row.Cells.Count; col++)
                                {
                                    string cellValue = row.Cells[col].Value?.ToString() ?? "";
                                    PdfPCell cell = new PdfPCell(new Phrase(cellValue, dataFont));
                                    cell.Padding = 4f;

                                    // Alternar colores de fila para mejor lectura
                                    if (row.Index % 2 == 0)
                                        cell.BackgroundColor = new BaseColor(240, 240, 240);

                                    table.AddCell(cell);
                                }
                            }
                        }

                        document.Add(table);

                        // Pie de página con total de registros
                        Paragraph footer = new Paragraph($"Total de registros: {dgvReport.Rows.Count - 1}", dateFont);
                        footer.Alignment = Element.ALIGN_RIGHT;
                        footer.SpacingBefore = 20f;
                        document.Add(footer);
                    }
                }

                MessageBox.Show("Reporte exportado a PDF exitosamente", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Preguntar si abrir el PDF
                if (MessageBox.Show("¿Desea abrir el reporte PDF ahora?", "Abrir PDF",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start(filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar PDF: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExportCsv_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Archivos CSV|*.csv";
            saveFileDialog.Title = "Exportar a CSV";
            saveFileDialog.FileName = $"Reporte_Asistencia_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                ExportToCsv(saveFileDialog.FileName);
            }
        }

        private void ExportToCsv(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("ID Empleado,Identificación,Empleado,Fecha/Hora,Tipo");

                    foreach (DataGridViewRow row in dgvReport.Rows)
                    {
                        if (!row.IsNewRow)
                        {
                            writer.WriteLine($"\"{row.Cells[0].Value}\",\"{row.Cells[1].Value}\",\"{row.Cells[2].Value}\",\"{row.Cells[3].Value}\",\"{row.Cells[4].Value}\"");
                        }
                    }
                }

                MessageBox.Show("Reporte exportado a CSV exitosamente", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar CSV: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}