using System;
using System.Threading.Tasks;

namespace AttendanceFingerprint.Services
{
    public interface IFingerprintDevice : IDisposable
    {
        string DeviceName { get; }
        Task<bool> InitializeAsync();
        Task<bool> EnrollAsync(int userId, int timeoutMs = 30000);
        Task<int> VerifyAsync(int timeoutMs = 10000);
        Task<bool> CheckDeviceAsync();
        void StartContinuousCapture();

        event EventHandler<FingerprintEventArgs> OnFingerprintCaptured;

        // Nuevos eventos para enrolamiento
        event EventHandler<EnrollmentEventArgs> OnEnrollmentProgress;
        event EventHandler<EnrollmentEventArgs> OnEnrollmentCompleted;
    }

    public class FingerprintEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? UserId { get; set; }
        public string TemplateData { get; set; }

        // Nuevas propiedades para manejo de errores
        public FingerprintErrorType ErrorType { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorDetails { get; set; }
    }

    public class EnrollmentEventArgs : EventArgs
    {
        public int Progress { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }
        public string TemplateData { get; set; }
        public int UserId { get; set; }
    }
}