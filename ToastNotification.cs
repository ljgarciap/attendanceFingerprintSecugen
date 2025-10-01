using System;
using System.Drawing;
using System.Linq;
using System.Timers;
using System.Windows.Forms;

namespace AttendanceFingerprint
{
    public class ToastNotification : Form
    {
        private System.Timers.Timer _timer;
        private Label _messageLabel;

        public ToastNotification(string message, ToastType type = ToastType.Info, int duration = 3000)
        {
            InitializeComponent();
            SetMessage(message, type);
            SetDuration(duration);
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;

            // Configurar tamaño y apariencia
            this.Size = new Size(300, 60);
            this.BackColor = Color.FromArgb(50, 50, 50);
            this.Opacity = 0.9;

            // Label para el mensaje
            _messageLabel = new Label();
            _messageLabel.UseCompatibleTextRendering = true;
            _messageLabel.Dock = DockStyle.Fill;
            _messageLabel.ForeColor = Color.White;
            _messageLabel.TextAlign = ContentAlignment.MiddleCenter;
            _messageLabel.Font = new Font("Segoe UI", 10, FontStyle.Regular);

            this.Controls.Add(_messageLabel);
        }

        public void SetMessage(string message, ToastType type)
        {
            _messageLabel.Text = message;

            // Cambiar color según el tipo
            switch (type)
            {
                case ToastType.Success:
                    this.BackColor = Color.FromArgb(76, 175, 80); // Verde
                    break;
                case ToastType.Error:
                    this.BackColor = Color.FromArgb(244, 67, 54); // Rojo
                    break;
                case ToastType.Warning:
                    this.BackColor = Color.FromArgb(255, 193, 7); // Amarillo
                    _messageLabel.ForeColor = Color.Black;
                    break;
                case ToastType.Info:
                default:
                    this.BackColor = Color.FromArgb(33, 150, 243); // Azul
                    break;
            }
        }

        public void SetDuration(int milliseconds)
        {
            _timer = new System.Timers.Timer(milliseconds);
            _timer.Elapsed += (s, e) => {
                _timer.Stop();
                this.Invoke(new Action(() => this.Close()));
            };
            _timer.AutoReset = false;
        }

        public new void Show()
        {
            // Posicionar en la esquina inferior derecha
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(
                screen.Right - this.Width - 10,
                screen.Bottom - this.Height - 10
            );

            base.Show();
            _timer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _timer?.Dispose();
            base.OnFormClosing(e);
        }

        // Para evitar que se active con clicks
        protected override bool ShowWithoutActivation => true;
        private const int WS_EX_TOPMOST = 0x00000008;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= WS_EX_TOPMOST;
                return createParams;
            }
        }
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    // Clase estática para facilitar el uso
    // Al final de tu archivo, donde está la clase Toast, agrega esto:
    public enum FingerprintErrorType
    {
        NoMatch,
        DeviceError,
        Timeout,
        NotEnrolled,
        LowQuality,
        Generic
    }

    public static class Toast
    {
        // Método existente que ya tienes
        public static void Show(string message, ToastType type = ToastType.Info, int duration = 3000)
        {
            var toast = new ToastNotification(message, type, duration);
            toast.Show();
        }

        // Nuevos métodos específicos para errores de huella
        public static void ShowError(string message, int duration = 4000)
        {
            Show(message, ToastType.Error, duration);
        }

        public static void ShowFingerprintError(FingerprintErrorType errorType, string additionalInfo = "")
        {
            string message = GetFingerprintErrorMessage(errorType, additionalInfo);
            if (string.IsNullOrWhiteSpace(message))
                message = "❌ Error desconocido de huella"; // fallback
            ShowError(message, 4500);
        }

        private static string GetFingerprintErrorMessage(FingerprintErrorType errorType, string additionalInfo)
        {
            string baseMessage;
            switch (errorType)
            {
                case FingerprintErrorType.NoMatch:
                    baseMessage = "❌ Huella no reconocida";
                    break;
                case FingerprintErrorType.DeviceError:
                    baseMessage = "⚠️ Error del dispositivo";
                    break;
                case FingerprintErrorType.Timeout:
                    baseMessage = "⏰ Tiempo de lectura agotado";
                    break;
                case FingerprintErrorType.NotEnrolled:
                    baseMessage = "📝 Huella no registrada";
                    break;
                case FingerprintErrorType.LowQuality:
                    baseMessage = "📸 Calidad de imagen insuficiente";
                    break;
                case FingerprintErrorType.Generic:
                    baseMessage = "❌ Error de lectura";
                    break;
                default:
                    baseMessage = "❌ Error desconocido";
                    break;
            }

            // Mensajes más específicos para "no registrado"
            if (errorType == FingerprintErrorType.NotEnrolled)
            {
                if (!string.IsNullOrEmpty(additionalInfo) &&
                    additionalInfo.IndexOf("NO_EMPLOYEES", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseMessage = "👥 No hay empleados registrados";
                }
                else if (!string.IsNullOrEmpty(additionalInfo) &&
                         additionalInfo.IndexOf("NOT_ENROLLED", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseMessage = "📝 Huella no registrada";
                }
            }

            return string.IsNullOrEmpty(additionalInfo)
                ? baseMessage
                : $"{baseMessage}: {additionalInfo}";
        }
    }
}