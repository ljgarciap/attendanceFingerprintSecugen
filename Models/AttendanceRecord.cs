using System;

namespace AttendanceFingerprint.Models
{
    public class AttendanceRecord
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime RecordDate { get; set; } = DateTime.Now;
        public RecordType RecordType { get; set; }

        // Propiedades extendidas para reportes (solo lectura)
        public string EmployeeName { get; set; }
        public string Identification { get; set; }
    }

    public enum RecordType
    {
        Entrance,
        Exit
    }
}