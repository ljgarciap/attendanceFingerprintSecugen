using AttendanceFingerprint.Database;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DPFP.Verification;
using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DPFP_Capture = DPFP.Capture;

namespace AttendanceFingerprint.Services
{
    public class DigitalPersonaDevice : IFingerprintDevice, DPFP_Capture.EventHandler
    {
        private DPFP_Capture.Capture _capturer;
        private Enrollment _enroller;                                // <- acceso controlado
        private Verification _verificator;
        private TaskCompletionSource<int> _verifyTcs;
        private int _currentEnrollmentUserId;
        private bool _isCapturing = false;

        // Hilos / sincronización
        private Thread _captureThread;
        private SynchronizationContext _uiCtx;
        private SynchronizationContext _capCtx;
        private readonly ManualResetEventSlim _started = new ManualResetEventSlim(false);

        // Control de sesión de enrolamiento para evitar carreras
        private volatile bool _enrolling = false;
        private int _enrollSessionId = 0;                            // cambia por sesión
        private readonly object _enrollStateLock = new object();     // para lecturas/mutaciones compuestas

        public string DeviceName => "DigitalPersona U.are.U";

        public event EventHandler<FingerprintEventArgs> OnFingerprintCaptured;
        public event EventHandler<EnrollmentEventArgs> OnEnrollmentProgress;
        public event EventHandler<EnrollmentEventArgs> OnEnrollmentCompleted;

        public Task<bool> InitializeAsync()
        {
            try
            {
                if (_capturer != null && _isCapturing)
                {
                    Console.WriteLine("⚠️ Inicialización: el capturador ya estaba activo.");
                    return Task.FromResult(true);
                }

                if (_uiCtx == null)
                    _uiCtx = SynchronizationContext.Current;

                _captureThread = new Thread(() =>
                {
                    try
                    {
                        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                        _capCtx = SynchronizationContext.Current;

                        _capturer = new DPFP.Capture.Capture(DPFP.Capture.Priority.Low);
                        _capturer.EventHandler = this;

                        _verificator = new Verification();

                        _capturer.StartCapture();
                        _isCapturing = true;

                        Console.WriteLine("✅ DigitalPersona inicializado y captura iniciada (hilo de fondo)");
                        _started.Set();

                        Application.Run();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error en hilo de captura: {ex.Message}");
                        _started.Set();
                    }
                })
                {
                    IsBackground = true,
                    Name = "DPFP-Capture-Thread"
                };
                _captureThread.SetApartmentState(ApartmentState.STA);
                _captureThread.Start();

                _started.Wait(TimeSpan.FromSeconds(3));
                return Task.FromResult(_isCapturing);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error inicializando DigitalPersona: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> CheckDeviceAsync()
        {
            return Task.FromResult(_capturer != null && _isCapturing);
        }

        // ---- Enrolamiento NO bloqueante: inicia sesión y retorna. Todo llega por eventos. ----
        public Task<bool> EnrollAsync(int userId, int timeoutMs = 30000)
        {
            try
            {
                Console.WriteLine($"🔹 Iniciando enrolamiento para usuario {userId}");

                // Nueva sesión: invalida callbacks viejos
                int session = Interlocked.Increment(ref _enrollSessionId);

                lock (_enrollStateLock)
                {
                    _enroller = new Enrollment();
                    _currentEnrollmentUserId = userId;
                    _enrolling = true;
                }

                // Aviso inicial (0%)
                RaiseEnrollmentProgress(new EnrollmentEventArgs
                {
                    Progress = 0,
                    UserId = userId,
                    Message = "Iniciando enrolamiento... coloque el dedo"
                });

                // Timeout “suave”: si pasa el tiempo y no terminó, cancelar sesión
                Task.Run(async () =>
                {
                    await Task.Delay(timeoutMs);
                    // Cancelar solo si sigue activa esta misma sesión
                    if (_enrolling && session == _enrollSessionId)
                    {
                        Console.WriteLine("⌛ Enrolamiento: timeout alcanzado");
                        lock (_enrollStateLock)
                        {
                            _enroller = null;
                            _enrolling = false;
                        }
                        RaiseEnrollmentCompleted(new EnrollmentEventArgs
                        {
                            Success = false,
                            UserId = userId,
                            Message = "Enrolamiento falló por timeout"
                        });
                    }
                });

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error iniciando enrolamiento: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public async Task<int> VerifyAsync(int timeoutMs = 15000)
        {
            Console.WriteLine("🔎 Verificación: coloque un dedo…");

            if (_verifyTcs != null && !_verifyTcs.Task.IsCompleted)
            {
                Console.WriteLine("⚠️ Ya hay una verificación en curso, cancelando la anterior.");
                _verifyTcs.TrySetResult(-1);
            }

            _verifyTcs = new TaskCompletionSource<int>();
            var timeoutTask = Task.Delay(timeoutMs);

            var first = await Task.WhenAny(_verifyTcs.Task, timeoutTask);
            if (first == timeoutTask)
            {
                Console.WriteLine("⌛ Verificación: tiempo agotado sin muestra");
                _verifyTcs = null;
                return -1;
            }

            int matchedUser = await _verifyTcs.Task;
            _verifyTcs = null;
            return matchedUser;
        }

        // --- DPFP EventHandler (se ejecuta en hilo de captura) ---
        public void OnComplete(object Capture, string ReaderSerialNumber, Sample sample)
        {
            Console.WriteLine("👉 Huella capturada");
            Task.Run(() => ProcessSample(sample));
        }

        public void OnFingerTouch(object Capture, string ReaderSerialNumber) => Console.WriteLine("👆 Dedo detectado");
        public void OnFingerGone(object Capture, string ReaderSerialNumber) => Console.WriteLine("✋ Dedo retirado");
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) => Console.WriteLine("🔌 Lector conectado");
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) => Console.WriteLine("❌ Lector desconectado");
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP_Capture.CaptureFeedback feedback)
            => Console.WriteLine($"📊 Calidad de muestra: {feedback}");

        private void ProcessSample(Sample sample)
        {
            try
            {
                Console.WriteLine("👉 Procesando muestra de huella");

                // Copiamos flags/refs al inicio para evitar carreras
                bool enrolling = _enrolling;
                int session = _enrollSessionId;
                var enroller = System.Threading.Volatile.Read(ref _enroller); // snapshot local

                if (enrolling && enroller != null)
                {
                    Console.WriteLine("🔹 Modo: ENROLAMIENTO");

                    var features = ExtractFeatures(sample, DataPurpose.Enrollment);
                    if (features == null)
                    {
                        RaiseEnrollmentProgress(new EnrollmentEventArgs
                        {
                            Progress = 0,
                            UserId = _currentEnrollmentUserId,
                            Message = "Muestra de baja calidad. Intente nuevamente."
                        });
                        return;
                    }

                    // Usamos SOLO la copia local 'enroller'
                    enroller.AddFeatures(features);

                    int remaining = (int)enroller.FeaturesNeeded; // no NRE porque usamos la copia
                    int progress = Math.Max(0, 100 - (remaining * 25));
                    RaiseEnrollmentProgress(new EnrollmentEventArgs
                    {
                        Progress = progress,
                        UserId = _currentEnrollmentUserId,
                        Message = $"Faltan muestras: {remaining}"
                    });

                    // Revisamos estado SOLO con la copia local
                    if (enroller.TemplateStatus == Enrollment.Status.Ready)
                    {
                        // Cerramos la sesión actual de forma atómica: ignorar muestras tardías
                        if (session == _enrollSessionId) // misma sesión
                        {
                            var template = enroller.Template; // aún válido sobre la copia
                            string b64 = Convert.ToBase64String(template.Bytes);
                            SaveTemplate(_currentEnrollmentUserId, template);

                            // Limpieza atómica
                            lock (_enrollStateLock)
                            {
                                // Evita que otro callback use la instancia
                                Interlocked.Exchange(ref _enroller, null);
                                _enrolling = false;
                            }

                            RaiseEnrollmentCompleted(new EnrollmentEventArgs
                            {
                                Success = true,
                                UserId = _currentEnrollmentUserId,
                                TemplateData = b64,
                                Message = "Enrolamiento completado"
                            });

                            Console.WriteLine("✅ Enrolamiento completado - Limpiando enroller");
                            _currentEnrollmentUserId = 0;
                        }
                        return;
                    }
                    else if (enroller.TemplateStatus == Enrollment.Status.Failed)
                    {
                        if (session == _enrollSessionId)
                        {
                            lock (_enrollStateLock)
                            {
                                Interlocked.Exchange(ref _enroller, null);
                                _enrolling = false;
                            }

                            RaiseEnrollmentCompleted(new EnrollmentEventArgs
                            {
                                Success = false,
                                UserId = _currentEnrollmentUserId,
                                Message = "Enrolamiento fallido"
                            });

                            Console.WriteLine("❌ Enrolamiento fallido - Limpiando enroller");
                            _currentEnrollmentUserId = 0;
                        }
                        return;
                    }

                    // Aún no listo, esperamos siguientes muestras
                    return;
                }

                // ---- VERIFICACIÓN ----
                Console.WriteLine("🔹 Modo: VERIFICACIÓN");
                var verifyFeatures = ExtractFeatures(sample, DataPurpose.Verification);
                if (verifyFeatures != null)
                {
                    int matchedUser = MatchFingerprint(verifyFeatures);
                    Console.WriteLine($"🔍 Resultado verificación: {matchedUser}");

                    if (matchedUser > 0)
                    {
                        RaiseFingerprintCaptured(new FingerprintEventArgs
                        {
                            Success = true,
                            UserId = matchedUser,
                            Message = $"Huella verificada: Usuario {matchedUser}"
                        });
                    }
                    else if (matchedUser == -1)
                    {
                        RaiseFingerprintCaptured(new FingerprintEventArgs
                        {
                            Success = false,
                            ErrorType = FingerprintErrorType.NoMatch,
                            Message = "Huella no reconocida en el sistema",
                            ErrorCode = "DP_NO_MATCH",
                            ErrorDetails = "Esta huella no está asociada a ningún empleado"
                        });
                    }
                    else if (matchedUser == -2)
                    {
                        RaiseFingerprintCaptured(new FingerprintEventArgs
                        {
                            Success = false,
                            ErrorType = FingerprintErrorType.NotEnrolled,
                            Message = "No hay empleados registrados",
                            ErrorCode = "DP_NO_EMPLOYEES",
                            ErrorDetails = "Registre empleados antes de usar el sistema"
                        });
                    }
                    else
                    {
                        RaiseFingerprintCaptured(new FingerprintEventArgs
                        {
                            Success = false,
                            ErrorType = FingerprintErrorType.DeviceError,
                            Message = "Error en la verificación",
                            ErrorCode = "DP_VERIFICATION_ERROR",
                            ErrorDetails = $"Código de error: {matchedUser}"
                        });
                    }

                    if (_verifyTcs != null && !_verifyTcs.Task.IsCompleted)
                        _verifyTcs.TrySetResult(matchedUser);
                }
                else
                {
                    RaiseFingerprintCaptured(new FingerprintEventArgs
                    {
                        Success = false,
                        ErrorType = FingerprintErrorType.LowQuality,
                        Message = "Calidad de imagen insuficiente para verificación",
                        ErrorCode = "DP_LOW_QUALITY"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando muestra: {ex.Message}");
                SafeRestartCapture();

                RaiseFingerprintCaptured(new FingerprintEventArgs
                {
                    Success = false,
                    ErrorType = FingerprintErrorType.DeviceError,
                    Message = $"Error del dispositivo: {ex.Message}",
                    ErrorCode = "DP_DEVICE_ERROR",
                    ErrorDetails = ex.GetType().Name
                });
            }
        }

        private FeatureSet ExtractFeatures(Sample sample, DataPurpose purpose)
        {
            try
            {
                var extractor = new FeatureExtraction();
                CaptureFeedback feedback = CaptureFeedback.None;
                FeatureSet features = new FeatureSet();

                extractor.CreateFeatureSet(sample, purpose, ref feedback, ref features);

                return feedback == CaptureFeedback.Good ? features : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error extrayendo características: {ex.Message}");
                return null;
            }
        }

        private int MatchFingerprint(FeatureSet probeFeatures)
        {
            try
            {
                Console.WriteLine("🔍 Buscando coincidencia de huella...");

                using (var dbService = new SQLiteDatabaseService())
                {
                    var employees = dbService.GetEmployees();
                    if (employees == null || employees.Count == 0)
                    {
                        Console.WriteLine("❌ No hay empleados registrados en el sistema");
                        return -2;
                    }

                    foreach (var employee in employees)
                    {
                        string templateData = dbService.GetFingerprintTemplate(employee.Id);
                        if (!string.IsNullOrEmpty(templateData))
                        {
                            byte[] templateBytes = Convert.FromBase64String(templateData);
                            using (var ms = new MemoryStream(templateBytes))
                            {
                                var storedTemplate = new Template(ms);
                                var result = new Verification.Result();
                                _verificator.Verify(probeFeatures, storedTemplate, ref result);

                                if (result.Verified)
                                {
                                    Console.WriteLine($"✅ Coincidencia encontrada: Usuario {employee.Id}");
                                    return employee.Id;
                                }
                            }
                        }
                    }
                }
                Console.WriteLine("❌ No se encontró coincidencia");
                return -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en verificación: {ex.Message}");
                return -1;
            }
        }

        private void SaveTemplate(int userId, Template template)
        {
            try
            {
                using (var dbService = new SQLiteDatabaseService())
                {
                    string templateData = Convert.ToBase64String(template.Bytes);
                    dbService.SaveFingerprintTemplate(userId, templateData);
                    Console.WriteLine($"✅ Template guardado para usuario {userId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error guardando template: {ex.Message}");
            }
        }

        private void SafeRestartCapture()
        {
            try
            {
                if (_capCtx != null)
                {
                    _capCtx.Post(_ =>
                    {
                        try
                        {
                            if (_capturer != null && _isCapturing)
                            {
                                _capturer.StopCapture();
                                Thread.Sleep(100);
                                _capturer.StartCapture();
                                Console.WriteLine("✅ Captura reiniciada exitosamente");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error reiniciando captura: {ex.Message}");
                        }
                    }, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en SafeRestartCapture: {ex.Message}");
            }
        }

        public void StartContinuousCapture()
        {
            try
            {
                if (_capCtx != null)
                {
                    _capCtx.Post(_ =>
                    {
                        try
                        {
                            if (_capturer != null && !_isCapturing)
                            {
                                _capturer.StartCapture();
                                _isCapturing = true;
                                Console.WriteLine("✅ Captura continua DigitalPersona iniciada");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error iniciando captura: {ex.Message}");
                        }
                    }, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en StartContinuousCapture: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                // Cancelar enrolamiento en curso (si lo hay)
                lock (_enrollStateLock)
                {
                    _enroller = null;
                    _enrolling = false;
                }

                if (_capCtx != null)
                {
                    _capCtx.Post(_ =>
                    {
                        try
                        {
                            if (_capturer != null)
                            {
                                if (_isCapturing)
                                {
                                    _capturer.StopCapture();
                                    _isCapturing = false;
                                }
                                _capturer.Dispose();
                                _capturer = null;
                            }
                        }
                        catch { }

                        try
                        {
                            Application.ExitThread();
                        }
                        catch { }
                    }, null);
                }

                if (_captureThread != null && _captureThread.IsAlive)
                {
                    _captureThread.Join(1000);
                }
            }
            catch { }
        }

        // ---- helpers para disparar eventos SIEMPRE en el hilo de UI ----
        private void RaiseFingerprintCaptured(FingerprintEventArgs args)
        {
            var handler = OnFingerprintCaptured;
            if (handler == null) return;

            if (_uiCtx != null)
                _uiCtx.Post(_ => handler(this, args), null);
            else
                handler(this, args);
        }

        private void RaiseEnrollmentProgress(EnrollmentEventArgs args)
        {
            var handler = OnEnrollmentProgress;
            if (handler == null) return;

            if (_uiCtx != null)
                _uiCtx.Post(_ => handler(this, args), null);
            else
                handler(this, args);
        }

        private void RaiseEnrollmentCompleted(EnrollmentEventArgs args)
        {
            var handler = OnEnrollmentCompleted;
            if (handler == null) return;

            if (_uiCtx != null)
                _uiCtx.Post(_ => handler(this, args), null);
            else
                handler(this, args);
        }
    }
}
