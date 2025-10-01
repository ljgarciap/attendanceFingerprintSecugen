using System;
using System.Management;
using System.Linq;
using System.Collections.Generic;

namespace AttendanceFingerprint.Services
{
    public static class DeviceManager
    {
        public static FingerprintDeviceType DetectConnectedDevice()
        {
            try
            {
                Console.WriteLine("=== INICIANDO DETECCIÓN DE DISPOSITIVOS ===");

                bool hasDigitalPersona = CheckForDigitalPersona();
                bool hasPersona4500 = CheckForPersona4500();
                bool hasSecureGen = CheckForSecureGen();

                Console.WriteLine($"Resultados - DigitalPersona: {hasDigitalPersona}, Persona4500: {hasPersona4500}, SecureGen: {hasSecureGen}");

                if (hasSecureGen)
                {
                    Console.WriteLine("🎯 SELECCIONANDO SECUGEN");
                    return FingerprintDeviceType.SecureGenHamster20;
                }
                if (hasDigitalPersona)
                {
                    Console.WriteLine("🎯 SELECCIONANDO DIGITALPERSONA");
                    return FingerprintDeviceType.DigitalPersonaUareU;
                }
                if (hasPersona4500)
                {
                    Console.WriteLine("🎯 SELECCIONANDO PERSONA4500");
                    return FingerprintDeviceType.Persona4500;
                }

                Console.WriteLine("❌ NO HAY DISPOSITIVOS DETECTADOS");
                return FingerprintDeviceType.None;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error en detección: {ex.Message}");
                return FingerprintDeviceType.None;
            }
        }

        private static bool CheckForPersona4500()
        {
            try
            {
                // Múltiples formas de detectar el Persona 4500
                string[] queries = {
                    "SELECT * FROM Win32_PnPEntity WHERE Description LIKE '%Persona%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%Persona%'",
                    "SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_08FF%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE '%Crossmatch%'",
                    "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Biometric'"
                };

                foreach (var query in queries)
                {
                    using (var searcher = new ManagementObjectSearcher(query))
                    {
                        if (searcher.Get().Count > 0)
                        {
                            Console.WriteLine($"✅ Detectado con query: {query}");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error buscando Persona: {ex.Message}");
                return false;
            }
        }

        private static bool CheckForDigitalPersona()
        {
            try
            {
                string[] queries = {
            "SELECT * FROM Win32_PnPEntity WHERE Description LIKE '%U.are.U%'",
            "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%U.are.U%'",
            "SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_05BA%'",
            "SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE '%DigitalPersona%'",
            "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Biometric' AND Description LIKE '%Fingerprint%'"
        };

                foreach (var query in queries)
                {
                    using (var searcher = new ManagementObjectSearcher(query))
                    {
                        if (searcher.Get().Count > 0)
                        {
                            Console.WriteLine($"✅ Detectado DigitalPersona con query: {query}");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error buscando DigitalPersona: {ex.Message}");
                return false;
            }
        }

        private static bool CheckForSecureGen()
        {
            try
            {
                string[] queries = {
                    "SELECT * FROM Win32_PnPEntity WHERE Description LIKE '%SecuGen%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%SecuGen%'",
                    "SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_0C5A%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE '%SecuGen%'",
                    "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Biometric' AND Description LIKE '%Hamster%'"
                };

                foreach (var query in queries)
                {
                    using (var searcher = new ManagementObjectSearcher(query))
                    {
                        if (searcher.Get().Count > 0)
                            Console.WriteLine($"✅ Detectado SecuGen con query: {query}");
                            return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error buscando SecuGen: {ex.Message}");
                return false;
            }
        }

        // Método para debugging: listar todos los dispositivos USB
        private static void ListAllUSBDevices()
        {
            try
            {
                Console.WriteLine("📋 Listando dispositivos USB disponibles:");

                string query = "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Biometric' OR Description LIKE '%finger%' OR Description LIKE '%huella%' OR Name LIKE '%finger%' OR Name LIKE '%huella%'";

                using (var searcher = new ManagementObjectSearcher(query))
                {
                    int count = 0;
                    foreach (ManagementObject device in searcher.Get())
                    {
                        count++;
                        string name = device["Name"]?.ToString() ?? "Sin nombre";
                        string description = device["Description"]?.ToString() ?? "Sin descripción";
                        string deviceId = device["DeviceID"]?.ToString() ?? "Sin ID";

                        Console.WriteLine($"   {count}. {name}");
                        Console.WriteLine($"      Descripción: {description}");
                        Console.WriteLine($"      DeviceID: {deviceId}");
                        Console.WriteLine($"      ---");
                    }

                    if (count == 0)
                    {
                        Console.WriteLine("   ℹ️ No se encontraron dispositivos biométricos");

                        // Listar todos los dispositivos USB genéricos
                        ListAllUSBDevicesGeneric();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error listando dispositivos: {ex.Message}");
            }
        }

        private static void ListAllUSBDevicesGeneric()
        {
            try
            {
                Console.WriteLine("🔌 Listando dispositivos USB genéricos:");

                string query = "SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%USB%'";

                using (var searcher = new ManagementObjectSearcher(query))
                {
                    int count = 0;
                    foreach (ManagementObject device in searcher.Get().OfType<ManagementObject>().Take(10)) // Solo primeros 10
                    {
                        count++;
                        string name = device["Name"]?.ToString() ?? "Sin nombre";
                        string description = device["Description"]?.ToString() ?? "Sin descripción";

                        Console.WriteLine($"   {count}. {name} - {description}");
                    }

                    if (count == 0)
                        Console.WriteLine("   ℹ️ No se encontraron dispositivos USB");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error listando USB genéricos: {ex.Message}");
            }
        }

        public static IFingerprintDevice CreateDevice(FingerprintDeviceType deviceType)
        {
            switch (deviceType)
            {
                case FingerprintDeviceType.DigitalPersonaUareU:
                case FingerprintDeviceType.Persona4500:
                    Console.WriteLine("📱 Instanciando DigitalPersonaDevice");
                    return new DigitalPersonaDevice();

                case FingerprintDeviceType.SecuGenHamster20:
                case FingerprintDeviceType.SecureGenHamster20:
                    Console.WriteLine("📱 Instanciando SecuGenBSPDevice");
                    return new SecuGenBSPDevice();

                case FingerprintDeviceType.None:
                    throw new NotSupportedException("No se puede crear dispositivo de tipo 'None'");

                default:
                    // Lanza una excepción para indicar que el tipo de dispositivo no es compatible o es desconocido.
                    throw new NotSupportedException($"Tipo de dispositivo de huella digital no soportado: {deviceType}");
            }
        }
    }

    public enum FingerprintDeviceType
    {
        None,
        DigitalPersonaUareU,
        Persona4500,
        SecuGenHamster20,
        SecureGenHamster20 // Alias para evitar confusión
    }
}