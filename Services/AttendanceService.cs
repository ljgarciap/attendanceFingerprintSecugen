using AttendanceFingerprint.Database;
using AttendanceFingerprint.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AttendanceFingerprint.Services
{
    public class AttendanceService : IDisposable
    {
        private readonly SQLiteDatabaseService _database;
        private IFingerprintDevice _fingerprintDevice;
        private bool _isMonitoring;

        public event EventHandler<FingerprintEventArgs> OnFingerprintCaptured;

        public AttendanceService()
        {
            _database = new SQLiteDatabaseService();
        }

        public async Task<bool> InitializeDeviceAsync()
        {
            Console.WriteLine("Buscando dispositivos de huella digital...");

            var deviceType = DeviceManager.DetectConnectedDevice();

            if (deviceType == FingerprintDeviceType.None)
            {
                Console.WriteLine("❌ NO HAY DISPOSITIVOS - NO usar simulador");
                return false; // ← NO usar simulador
            }

            _fingerprintDevice = DeviceManager.CreateDevice(deviceType);

            Console.WriteLine($"Dispositivo detectado: {_fingerprintDevice.DeviceName}");

            if (await _fingerprintDevice.InitializeAsync())
            {
                // Suscribir al evento de huella capturada
                _fingerprintDevice.OnFingerprintCaptured += FingerprintCapturedHandler;

                return true;
            }

            return false;
        }

        private async void FingerprintCapturedHandler(object sender, FingerprintEventArgs e)
        {
            Console.WriteLine($"🔹 Huella capturada - Success: {e.Success}, UserId: {e.UserId}");

            if (e.Success && e.UserId.HasValue)
            {
                await ProcessAttendanceAsync(e.UserId.Value);
            }
            else
            {
                // Mostrar toast de error con información específica
                ShowFingerprintErrorToast(e);
                Console.WriteLine($"❌ Error en captura: {e.Message} - Tipo: {e.ErrorType}");
            }
        }

        private void ShowFingerprintErrorToast(FingerprintEventArgs e)
        {
            try
            {
                string errorDetails = string.Empty;

                if (!string.IsNullOrEmpty(e.ErrorCode))
                {
                    errorDetails += $"[{e.ErrorCode}] ";
                }

                if (!string.IsNullOrEmpty(e.ErrorDetails))
                {
                    errorDetails += e.ErrorDetails;
                }

                Toast.ShowFingerprintError(e.ErrorType, errorDetails);
                // 🔔 Emitir sonido de error
                System.Media.SystemSounds.Hand.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error mostrando toast de error: {ex.Message}");
                // Toast genérico de respaldo
                Toast.ShowError("Error en lectura de huella");
                // 🔔 Emitir sonido de error también aquí
                System.Media.SystemSounds.Hand.Play();
            }
        }

        public void ResumeMonitoring()
        {
            if (_fingerprintDevice != null && !_isMonitoring)
            {
                _isMonitoring = true;
                Console.WriteLine("✅ Monitoreo de huellas reanudado");
            }
        }

        public void PauseMonitoring()
        {
            if (_fingerprintDevice != null && _isMonitoring)
            {
                _isMonitoring = false;
                Console.WriteLine("⏸ Monitoreo de huellas pausado");
            }
        }

        private async Task ProcessAttendanceAsync(int userId)
        {
            try
            {
                Console.WriteLine($"🔹 Procesando asistencia para usuario: {userId}");

                var lastRecord = _database.GetLastAttendanceRecord(userId);

                // Nueva validación: si el último registro fue "Entrada" y es de un día anterior, generamos automáticamente una "Salida" al final de ese día
                if (lastRecord != null &&
                    lastRecord.RecordType == RecordType.Entrance &&
                    lastRecord.RecordDate.Date < DateTime.Now.Date)
                {
                    var autoExit = new AttendanceRecord
                    {
                        EmployeeId = userId,
                        RecordDate = lastRecord.RecordDate.Date.AddHours(23).AddMinutes(59), // salida automática a las 23:59
                        RecordType = RecordType.Exit
                    };

                    _database.SaveAttendanceRecord(autoExit);
                    Console.WriteLine($"⚠️ Salida automática generada para Usuario {userId} en {autoExit.RecordDate}");

                    lastRecord = autoExit;
                }

                var recordType = (lastRecord == null || lastRecord.RecordType == RecordType.Exit)
                    ? RecordType.Entrance
                    : RecordType.Exit;

                var record = new AttendanceRecord
                {
                    EmployeeId = userId,
                    RecordDate = DateTime.Now,
                    RecordType = recordType
                };

                _database.SaveAttendanceRecord(record);
                Console.WriteLine($"✅ Registro guardado: Usuario {userId}, Tipo: {recordType}");

                ShowNotification(userId, recordType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando asistencia: {ex.Message}");
            }
        }

        private void ShowNotification(int userId, RecordType recordType)
        {
            try
            {
                var employee = _database.GetEmployee(userId);
                string employeeName = employee != null ? $"{employee.FirstName} {employee.LastName}" : $"Usuario #{userId}";

                string message = recordType == RecordType.Entrance
                    ? $"✅ Entrada registrada: {DateTime.Now:HH:mm:ss}"
                    : $"🚪 Salida registrada: {DateTime.Now:HH:mm:ss}";

                // Mostrar notificación toast
                Toast.Show($"{message} - {employeeName}",
                          recordType == RecordType.Entrance ? ToastType.Success : ToastType.Info);

                System.Media.SystemSounds.Beep.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error mostrando notificación: {ex.Message}");
            }
        }

        public async Task<bool> EnrollEmployeeAsync(Employee employee)
        {
            try
            {
                using (var dbService = new SQLiteDatabaseService())
                {
                    // Verificar si ya existe
                    var existingEmployees = dbService.GetEmployees();
                    var existing = existingEmployees.FirstOrDefault(e =>
                        e.Identification == employee.Identification);

                    if (existing != null)
                    {
                        Console.WriteLine($"ℹ️ Empleado ya existe: {employee.Identification}");
                        return true;
                    }

                    int employeeId = dbService.SaveEmployee(employee);
                    return employeeId > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error enrolando empleado: {ex.Message}");
                return false;
            }
        }

        public IFingerprintDevice GetDevice() => _fingerprintDevice;

        public async Task<bool> RecordAttendanceAsync(int userId)
        {
            try
            {
                Console.WriteLine($"🔹 Registrando asistencia para usuario: {userId}");

                // Usa la lógica que YA tienes en ProcessAttendanceAsync
                var lastRecord = _database.GetLastAttendanceRecord(userId);

                // Nueva validación: si el último registro fue "Entrada" y es de un día anterior, generamos automáticamente una "Salida" al final de ese día
                if (lastRecord != null &&
                    lastRecord.RecordType == RecordType.Entrance &&
                    lastRecord.RecordDate.Date < DateTime.Now.Date)
                {
                    var autoExit = new AttendanceRecord
                    {
                        EmployeeId = userId,
                        RecordDate = lastRecord.RecordDate.Date.AddHours(23).AddMinutes(59), // salida automática a las 23:59
                        RecordType = RecordType.Exit
                    };

                    _database.SaveAttendanceRecord(autoExit);
                    Console.WriteLine($"⚠️ Salida automática generada para Usuario {userId} en {autoExit.RecordDate}");

                    lastRecord = autoExit;
                }

                var recordType = (lastRecord == null || lastRecord.RecordType == RecordType.Exit)
                    ? RecordType.Entrance
                    : RecordType.Exit;

                var record = new AttendanceRecord
                {
                    EmployeeId = userId,
                    RecordDate = DateTime.Now,
                    RecordType = recordType
                };

                _database.SaveAttendanceRecord(record);
                Console.WriteLine($"✅ Registro guardado: Usuario {userId}, Tipo: {recordType}");

                ShowNotification(userId, recordType);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error registrando asistencia: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_fingerprintDevice != null)
            {
                _fingerprintDevice.OnFingerprintCaptured -= FingerprintCapturedHandler;
                _fingerprintDevice.Dispose();
            }
            Console.WriteLine("Servicio de asistencia liberado");
        }
    }
}