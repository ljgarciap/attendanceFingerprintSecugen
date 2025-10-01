using AttendanceFingerprint.Database;
using AttendanceFingerprint.Models;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AttendanceFingerprint.Services
{
    public class EnrollmentService : IDisposable
    {
        private readonly SQLiteDatabaseService _database;
        private IFingerprintDevice _fingerprintDevice;
        private bool _isEnrolling = false;

        // Eventos para notificar progreso
        public event EventHandler<EnrollmentEventArgs> EnrollmentProgress;
        public event EventHandler<EnrollmentEventArgs> EnrollmentCompleted;

        public EnrollmentService()
        {
            _database = new SQLiteDatabaseService();
        }

        public async Task<bool> StartEnrollmentAsync(int userId, int timeoutMs = 60000)
        {
            try
            {
                if (_isEnrolling)
                    return false;

                _isEnrolling = true;

                var deviceType = DeviceManager.DetectConnectedDevice();
                if (deviceType == FingerprintDeviceType.None)
                {
                    MessageBox.Show("No se detectó ningún lector de huellas");
                    return false;
                }

                _fingerprintDevice = DeviceManager.CreateDevice(deviceType);

                if (!await _fingerprintDevice.InitializeAsync())
                {
                    MessageBox.Show("No se pudo inicializar el dispositivo");
                    return false;
                }

                // Suscribirse a eventos
                _fingerprintDevice.OnEnrollmentProgress += HandleEnrollmentProgress;
                _fingerprintDevice.OnEnrollmentCompleted += HandleEnrollmentCompleted;

                return await _fingerprintDevice.EnrollAsync(userId, timeoutMs);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
                _isEnrolling = false;
                return false;
            }
        }

        private void HandleEnrollmentProgress(object sender, EnrollmentEventArgs e)
        {
            Console.WriteLine($"📊 Progreso: {e.Progress}% - {e.Message}");
            // Notificar a los suscriptores
            EnrollmentProgress?.Invoke(this, e);
        }

        private void HandleEnrollmentCompleted(object sender, EnrollmentEventArgs e)
        {
            _isEnrolling = false;

            if (e.Success)
            {
                Console.WriteLine($"✅ Enrolamiento completado para usuario {e.UserId}");
                Console.WriteLine($"📋 Template: {e.TemplateData?.Substring(0, Math.Min(50, e.TemplateData.Length))}...");
            }
            else
            {
                Console.WriteLine($"❌ Enrolamiento fallido: {e.Message}");
            }

            // Notificar a los suscriptores
            EnrollmentCompleted?.Invoke(this, e);

            // Limpiar eventos
            if (_fingerprintDevice != null)
            {
                _fingerprintDevice.OnEnrollmentProgress -= HandleEnrollmentProgress;
                _fingerprintDevice.OnEnrollmentCompleted -= HandleEnrollmentCompleted;
                _fingerprintDevice.Dispose();
            }
        }

        public void Dispose()
        {
            _fingerprintDevice?.Dispose();
            _database?.Dispose();
        }
    }
}