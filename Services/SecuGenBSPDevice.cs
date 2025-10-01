using AttendanceFingerprint.Database;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace AttendanceFingerprint.Services
{
    public class SecuGenBSPDevice : IFingerprintDevice
    {
        private dynamic _secuBSP;
        private bool _isInitialized = false;
        private Type _bspErrorType;
        private Type _firPurposeType;
        private bool _isCapturing = false;
        private Assembly _assembly;

        public string DeviceName => "SecuGen Hamster Pro 20 (BSP)";

        public event EventHandler<FingerprintEventArgs> OnFingerprintCaptured;
        public event EventHandler<EnrollmentEventArgs> OnEnrollmentProgress;
        public event EventHandler<EnrollmentEventArgs> OnEnrollmentCompleted;

        public async Task<bool> InitializeAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("🔌 Inicializando SecuGen BSP...");

                    string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SecuBSPMx.NET.dll");
                    Console.WriteLine($"Buscando DLL en: {dllPath}");

                    if (!File.Exists(dllPath))
                    {
                        Console.WriteLine("❌ SecuBSPMx.NET.dll no encontrada");
                        return false;
                    }

                    // Cargar assembly
                    _assembly = Assembly.LoadFrom(dllPath);
                    Console.WriteLine($"✅ Assembly cargada: {_assembly.FullName}");

                    // NAMESPACE CORRECTO: SecuBSPPro (no SecuBSPSDK)
                    Type secuBSPType = _assembly.GetType("SecuGen.SecuBSPPro.Windows.SecuBSPMx");

                    if (secuBSPType == null)
                    {
                        Console.WriteLine("❌ No se encontró SecuGen.SecuBSPPro.Windows.SecuBSPMx");
                        return false;
                    }

                    Console.WriteLine($"✅ Tipo encontrado: {secuBSPType.FullName}");

                    // Crear instancia
                    _secuBSP = Activator.CreateInstance(secuBSPType);
                    Console.WriteLine("✅ Instancia de SecuBSPMx creada");

                    // Cargar tipos de enumeración
                    _bspErrorType = _assembly.GetType("SecuGen.SecuBSPPro.Windows.BSPError");
                    _firPurposeType = _assembly.GetType("SecuGen.SecuBSPPro.Windows.FIRPurpose");

                    if (_bspErrorType == null || _firPurposeType == null)
                    {
                        Console.WriteLine("❌ No se encontraron tipos BSPError o FIRPurpose");
                        return false;
                    }

                    Console.WriteLine("✅ Tipos de enumeración cargados");

                    // Configurar dispositivo - usar valor numérico directamente
                    try
                    {
                        // AUTO_DETECT normalmente es 0x00FF (255)
                        _secuBSP.DeviceID = 0x00FF;
                        Console.WriteLine("✅ DeviceID configurado: AUTO_DETECT (0x00FF)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error configurando DeviceID: {ex.Message}");
                        // Intentar con valor por defecto
                        try
                        {
                            _secuBSP.DeviceID = 0;
                            Console.WriteLine("✅ DeviceID configurado: 0 (default)");
                        }
                        catch { }
                    }

                    // Abrir dispositivo
                    dynamic error = _secuBSP.OpenDevice();
                    dynamic errorNone = Enum.Parse(_bspErrorType, "ERROR_NONE");

                    if (error.Equals(errorNone))
                    {
                        _isInitialized = true;
                        Console.WriteLine("✅ SecuGen BSP inicializado correctamente");

                        // DESACTIVAR INTERFAZ GRÁFICA DE SECUGEN - ENFOQUE MÁS AGRESIVO
                        try
                        {
                            // Intentar múltiples métodos para desactivar UI
                            Type uiModeType = _assembly.GetType("SecuGen.SecuBSPPro.Windows.UIMode");
                            if (uiModeType != null)
                            {
                                try
                                {
                                    dynamic noUI = Enum.Parse(uiModeType, "NO_UI");
                                    _secuBSP.UIMode = noUI;
                                    Console.WriteLine("✅ UI desactivada via UIMode = NO_UI");
                                }
                                catch { }

                                // Método 2: Propiedades directas
                                _secuBSP.ShowUI = false;
                                _secuBSP.EnableUI = false;

                                // Método 3: Configuración de captura silenciosa
                                _secuBSP.BeepOnCapture = false;
                                _secuBSP.LEDOnCapture = false;
                                catch { }
                            }

                            // Intentar configuraciones adicionales
                            try
                            {
                                _secuBSP.SetTemplateFormat(1); // Formato ANSI
                                Console.WriteLine("✅ Template format configurado");
                            }
                            catch { }
                        }
                        catch (Exception uiEx)
                        {
                            Console.WriteLine($"⚠️ No se pudo desactivar UI completamente: {uiEx.Message}");
                        }

                        // Obtener información del dispositivo
                        try
                        {
                            int width = _secuBSP.ImageWidth;
                            int height = _secuBSP.ImageHeight;
                            Console.WriteLine($"📐 Dimensiones imagen: {width}x{height}");
                        }
                        catch { }

                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Error abriendo dispositivo SecuGen: {error}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Excepción inicializando SecuGen BSP: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    return false;
                }
            });
        }

        public async Task<bool> EnrollAsync(int userId, int timeoutMs = 30000)
        {
            if (!_isInitialized)
            {
                OnEnrollmentCompleted?.Invoke(this, new EnrollmentEventArgs
                {
                    Success = false,
                    UserId = userId,
                    Message = "Dispositivo no inicializado"
                });
                return false;
            }

            return await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine($"🔹 Iniciando enrolamiento SecuGen para usuario {userId}");

                    OnEnrollmentProgress?.Invoke(this, new EnrollmentEventArgs
                    {
                        Progress = 10,
                        UserId = userId,
                        Message = "Coloque el dedo en el lector..."
                    });

                    // Configurar propósito de captura
                    dynamic enrollPurpose = Enum.Parse(_firPurposeType, "ENROLL");
                    dynamic errorNone = Enum.Parse(_bspErrorType, "ERROR_NONE");

                    // Capturar para enrolamiento
                    dynamic error = _secuBSP.Capture(enrollPurpose);

                    if (!error.Equals(errorNone))
                    {
                        OnEnrollmentCompleted?.Invoke(this, new EnrollmentEventArgs
                        {
                            Success = false,
                            UserId = userId,
                            Message = $"Error en captura: {error}"
                        });
                        return false;
                    }

                    OnEnrollmentProgress?.Invoke(this, new EnrollmentEventArgs
                    {
                        Progress = 50,
                        UserId = userId,
                        Message = "Procesando huella..."
                    });

                    // Obtener datos de template
                    string templateData = _secuBSP.FIRTextData;

                    if (string.IsNullOrEmpty(templateData))
                    {
                        OnEnrollmentCompleted?.Invoke(this, new EnrollmentEventArgs
                        {
                            Success = false,
                            UserId = userId,
                            Message = "No se pudo obtener template"
                        });
                        return false;
                    }

                    OnEnrollmentProgress?.Invoke(this, new EnrollmentEventArgs
                    {
                        Progress = 80,
                        UserId = userId,
                        Message = "Guardando en base de datos..."
                    });

                    // Guardar en base de datos
                    using (var db = new SQLiteDatabaseService())
                    {
                        db.SaveFingerprintTemplate(userId, templateData);
                    }

                    OnEnrollmentCompleted?.Invoke(this, new EnrollmentEventArgs
                    {
                        Success = true,
                        UserId = userId,
                        TemplateData = templateData,
                        Message = "Enrolamiento completado exitosamente"
                    });

                    Console.WriteLine($"✅ Enrolamiento completado para usuario {userId}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error en enrolamiento: {ex.Message}");
                    OnEnrollmentCompleted?.Invoke(this, new EnrollmentEventArgs
                    {
                        Success = false,
                        UserId = userId,
                        Message = $"Excepción: {ex.Message}"
                    });
                    return false;
                }
            });
        }

        public async Task<int> VerifyAsync(int timeoutMs = 10000)
        {
            if (!_isInitialized)
            {
                OnFingerprintCaptured?.Invoke(this, new FingerprintEventArgs
                {
                    Success = false,
                    Message = "Dispositivo no inicializado",
                    ErrorType = FingerprintErrorType.DeviceError
                });
                return -1;
            }

            return await Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("🔍 Iniciando verificación SecuGen...");

                    // Configurar propósito de verificación
                    dynamic verifyPurpose = Enum.Parse(_firPurposeType, "VERIFY");
                    dynamic errorNone = Enum.Parse(_bspErrorType, "ERROR_NONE");

                    // Capturar huella
                    Console.WriteLine("📸 Capturando huella...");
                    dynamic captureError = _secuBSP.Capture(verifyPurpose);

                    if (!captureError.Equals(errorNone))
                    {
                        Console.WriteLine($"❌ Error en captura: {captureError}");
                        OnFingerprintCaptured?.Invoke(this, new FingerprintEventArgs
                        {
                            Success = false,
                            Message = $"Error en captura: {captureError}",
                            ErrorType = FingerprintErrorType.DeviceError
                        });
                        return -1;
                    }

                    // Obtener template capturado
                    string capturedTemplate = _secuBSP.FIRTextData;
                    Console.WriteLine($"📝 Template capturado: {!string.IsNullOrEmpty(capturedTemplate)}");

                    if (string.IsNullOrEmpty(capturedTemplate))
                    {
                        Console.WriteLine("❌ No se pudo obtener template de la captura");
                        OnFingerprintCaptured?.Invoke(this, new FingerprintEventArgs
                        {
                            Success = false,
                            Message = "No se pudo obtener template",
                            ErrorType = FingerprintErrorType.LowQuality
                        });
                        return -1;
                    }

                    // Comparar con templates almacenados
                    using (var db = new SQLiteDatabaseService())
                    {
                        var employees = db.GetEmployees();
                        Console.WriteLine($"👥 Empleados a comparar: {employees?.Count ?? 0}");

                        if (employees == null || employees.Count == 0)
                        {
                            OnFingerprintCaptured?.Invoke(this, new FingerprintEventArgs
                            {
                                Success = false,
                                Message = "No hay empleados registrados",
                                ErrorType = FingerprintErrorType.NotEnrolled
                            });
                            return -2;
                        }

                        foreach (var employee in employees)
                        {
                            string storedTemplate = db.GetFingerprintTemplate(employee.Id);
                            Console.WriteLine($"🔍 Comparando con empleado {employee.Id}: {!string.IsNullOrEmpty(storedTemplate)}");

                            if (string.IsNullOrEmpty(storedTemplate))
                                continue;

                            try
                            {
                                // Comparar templates - USAR MÉTODO DIRECTO
                                Console.WriteLine($"🔄 Verificando match...");
                                dynamic matchError = _secuBSP.VerifyMatch(capturedTemplate, storedTemplate);

                                if (matchError.Equals(errorNone))
                                {
                                    // Verificar si coincide - USAR PROPIEDAD DIRECTA
                                    bool isMatched = _secuBSP.IsMatched;
                                    Console.WriteLine($"🎯 Resultado match: {isMatched}");

                                    if (isMatched)
                                    {
                                        Console.WriteLine($"✅ HUella VERIFICADA: {employee.FirstName} {employee.LastName}");
                                        OnFingerprintCaptured?.Invoke(this, new FingerprintEventArgs
                                        {
                                            Success = true,
                                            UserId = employee.Id,
                                            Message = $"Huella verificada: {employee.FirstName} {employee.LastName}"
                                        });
                                        return employee.Id;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"❌ Error en VerifyMatch: {matchError}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Error comparando con empleado {employee.Id}: {ex.Message}");
                            }
                        }
                    }

                    // No se encontró coincidencia
                    Console.WriteLine("❌ Ninguna coincidencia encontrada");
                    OnFingerprintCaptured?.Invoke(this, new FingerprintEventArgs
                    {
                        Success = false,
                        Message = "Huella no reconocida",
                        ErrorType = FingerprintErrorType.NoMatch
                    });
                    return -1;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error en verificación: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    OnFingerprintCaptured?.Invoke(this, new FingerprintEventArgs
                    {
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        ErrorType = FingerprintErrorType.DeviceError
                    });
                    return -1;
                }
            });
        }

        public Task<bool> CheckDeviceAsync()
        {
            return Task.FromResult(_isInitialized);
        }

        public void StartContinuousCapture()
        {
            if (!_isInitialized || _isCapturing) return;

            _isCapturing = true;
            Console.WriteLine("▶️ SecuGen: Captura continua iniciada - MODO DEBUG");

            Task.Run(async () =>
            {
                int attempt = 0;
                while (_isCapturing && _isInitialized && attempt < 10) // Máximo 10 intentos para debug
                {
                    attempt++;
                    Console.WriteLine($"🔄 Intento #{attempt} de verificación...");

                    try
                    {
                        await VerifyAsync(1000);
                        await Task.Delay(2000); // 2 segundos entre intentos
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error en intento #{attempt}: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }

                Console.WriteLine($"⏹️ SecuGen: Captura continua finalizada después de {attempt} intentos");
                _isCapturing = false;
            });
        }

        public void StopContinuousCapture()
        {
            _isCapturing = false;
            Console.WriteLine("⏹️ SecuGen: Captura continua detenida");
        }

        public void Dispose()
        {
            try
            {
                StopContinuousCapture();

                if (_isInitialized && _secuBSP != null)
                {
                    _secuBSP.CloseDevice();
                    Console.WriteLine("✅ Dispositivo SecuGen cerrado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cerrar dispositivo: {ex.Message}");
            }
            finally
            {
                _isInitialized = false;
            }
        }
    }
}