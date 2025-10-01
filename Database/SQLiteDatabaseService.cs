using AttendanceFingerprint.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace AttendanceFingerprint.Database
{
    public class SQLiteDatabaseService : IDisposable
    {
        private SQLiteConnection _connection;
        private string _dbPath;

        public SQLiteDatabaseService()
        {
            _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                  "SistemaAsistencia", "attendance.db");

            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath));

            _connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
            _connection.Open();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            string employeesTable = @"CREATE TABLE IF NOT EXISTS Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                Identification TEXT UNIQUE NOT NULL,
                IsActive INTEGER DEFAULT 1,
                CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
            )";

            string attendanceTable = @"CREATE TABLE IF NOT EXISTS AttendanceRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                RecordDate DATETIME NOT NULL,
                RecordType INTEGER NOT NULL
            )";

            // Nueva tabla para templates de huellas
            string fingerprintTemplatesTable = @"CREATE TABLE IF NOT EXISTS FingerprintTemplates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                TemplateData TEXT NOT NULL,
                CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (EmployeeId) REFERENCES Employees (Id)
            )";

            string usersTable = @"CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT UNIQUE NOT NULL,
            PasswordHash TEXT NOT NULL,
            IsAdmin INTEGER DEFAULT 0,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
        )";

            ExecuteNonQuery(usersTable);
            ExecuteNonQuery(employeesTable);
            ExecuteNonQuery(attendanceTable);
            ExecuteNonQuery(fingerprintTemplatesTable);

            // Crear usuario admin por defecto (usuario: admin, contraseña: admin123)
            string checkAdminSql = "SELECT COUNT(*) FROM Users WHERE Username = 'admin'";
            using (var command = new SQLiteCommand(checkAdminSql, _connection))
            {
                int count = Convert.ToInt32(command.ExecuteScalar());
                if (count == 0)
                {
                    string defaultPassword = HashPassword("admin123");
                    string insertAdminSql = @"
            INSERT INTO Users (Username, PasswordHash, IsAdmin) 
            VALUES ('admin', @PasswordHash, 1)";

                    ExecuteNonQuery(insertAdminSql,
                        new SQLiteParameter("@PasswordHash", defaultPassword));
                }
            }
        }

        private void ExecuteNonQuery(string sql)
        {
            using (var command = new SQLiteCommand(sql, _connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private void ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using (var command = new SQLiteCommand(sql, _connection))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                command.ExecuteNonQuery();
            }
        }

        public string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public bool VerifyPassword(string username, string password)
        {
            string storedHash = GetPasswordHash(username);
            if (string.IsNullOrEmpty(storedHash))
                return false;

            string inputHash = HashPassword(password);
            return storedHash.Equals(inputHash);
        }

        private string GetPasswordHash(string username)
        {
            string sql = "SELECT PasswordHash FROM Users WHERE Username = @Username";

            using (var command = new SQLiteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@Username", username);
                var result = command.ExecuteScalar();
                return result?.ToString();
            }
        }

        public bool IsUserAdmin(string username)
        {
            string sql = "SELECT IsAdmin FROM Users WHERE Username = @Username";

            using (var command = new SQLiteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@Username", username);
                var result = command.ExecuteScalar();
                return result != null && Convert.ToBoolean(result);
            }
        }

        public bool CreateUser(string username, string password, bool isAdmin = false)
        {
            try
            {
                string passwordHash = HashPassword(password);
                string sql = @"
            INSERT INTO Users (Username, PasswordHash, IsAdmin) 
            VALUES (@Username, @PasswordHash, @IsAdmin)";

                ExecuteNonQuery(sql,
                    new SQLiteParameter("@Username", username),
                    new SQLiteParameter("@PasswordHash", passwordHash),
                    new SQLiteParameter("@IsAdmin", isAdmin));

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ChangePassword(string username, string newPassword)
        {
            try
            {
                string passwordHash = HashPassword(newPassword);
                string sql = "UPDATE Users SET PasswordHash = @PasswordHash WHERE Username = @Username";

                ExecuteNonQuery(sql,
                    new SQLiteParameter("@PasswordHash", passwordHash),
                    new SQLiteParameter("@Username", username));

                return true;
            }
            catch
            {
                return false;
            }
        }
        public int SaveEmployee(Employee employee)
        {
            try
            {
                Console.WriteLine($"💾 Intentando guardar empleado: {employee.FirstName} {employee.LastName}");

                string sql = @"INSERT INTO Employees (FirstName, LastName, Identification) 
                      VALUES (@FirstName, @LastName, @Identification);
                      SELECT last_insert_rowid();";

                using (var command = new SQLiteCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("@FirstName", employee.FirstName);
                    command.Parameters.AddWithValue("@LastName", employee.LastName);
                    command.Parameters.AddWithValue("@Identification", employee.Identification);

                    int newId = Convert.ToInt32(command.ExecuteScalar());
                    Console.WriteLine($"✅ Empleado guardado EXITOSAMENTE: ID={newId}, Nombre={employee.FirstName} {employee.LastName}");

                    // Verificar que realmente se guardó
                    VerifyEmployeeSaved(newId);

                    return newId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error guardando empleado: {ex.Message}");
                throw;
            }
        }

        private void VerifyEmployeeSaved(int employeeId)
        {
            try
            {
                string verifySql = "SELECT COUNT(*) FROM Employees WHERE Id = @Id";
                using (var command = new SQLiteCommand(verifySql, _connection))
                {
                    command.Parameters.AddWithValue("@Id", employeeId);
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    Console.WriteLine($"🔍 Verificación: {count} empleado(s) con ID {employeeId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en verificación: {ex.Message}");
            }
        }

        public void SaveAttendanceRecord(AttendanceRecord record)
        {
            string sql = @"INSERT INTO AttendanceRecords (EmployeeId, RecordDate, RecordType) 
                  VALUES (@EmployeeId, @RecordDate, @RecordType)";

            using (var command = new SQLiteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", record.EmployeeId);
                // Usar hora local explícitamente
                command.Parameters.AddWithValue("@RecordDate", record.RecordDate);
                command.Parameters.AddWithValue("@RecordType", (int)record.RecordType);
                command.ExecuteNonQuery();
            }
        }

        public Employee GetEmployee(int employeeId)
        {
            string sql = "SELECT * FROM Employees WHERE Id = @Id";

            using (var command = new SQLiteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@Id", employeeId);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Employee
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            FirstName = reader["FirstName"].ToString(),
                            LastName = reader["LastName"].ToString(),
                            Identification = reader["Identification"].ToString(),
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                        };
                    }
                }
            }
            return null;
        }

        public List<Employee> GetEmployees()
        {
            var employees = new List<Employee>();
            string sql = "SELECT * FROM Employees WHERE IsActive = 1 ORDER BY FirstName, LastName";

            using (var command = new SQLiteCommand(sql, _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    employees.Add(new Employee
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        FirstName = reader["FirstName"].ToString(),
                        LastName = reader["LastName"].ToString(),
                        Identification = reader["Identification"].ToString(),
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                    });
                }
            }
            return employees;
        }

        public AttendanceRecord GetLastAttendanceRecord(int employeeId)
        {
            string sql = @"SELECT * FROM AttendanceRecords 
                          WHERE EmployeeId = @EmployeeId 
                          ORDER BY RecordDate DESC 
                          LIMIT 1";

            using (var command = new SQLiteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@EmployeeId", employeeId);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new AttendanceRecord
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            EmployeeId = Convert.ToInt32(reader["EmployeeId"]),
                            RecordDate = Convert.ToDateTime(reader["RecordDate"]),
                            RecordType = (RecordType)Convert.ToInt32(reader["RecordType"])
                        };
                    }
                }
            }
            return null;
        }

        public List<AttendanceRecord> GetAttendanceRecords(DateTime startDate, DateTime endDate)
        {
            var records = new List<AttendanceRecord>();

            string sql = @"SELECT a.*, e.FirstName, e.LastName, e.Identification 
                  FROM AttendanceRecords a
                  INNER JOIN Employees e ON a.EmployeeId = e.Id
                  WHERE DATE(a.RecordDate) BETWEEN DATE(@StartDate) AND DATE(@EndDate)
                  ORDER BY a.RecordDate";

            using (var command = new SQLiteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@StartDate", startDate.Date);
                command.Parameters.AddWithValue("@EndDate", endDate.Date);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var record = new AttendanceRecord
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            EmployeeId = Convert.ToInt32(reader["EmployeeId"]),
                            RecordDate = Convert.ToDateTime(reader["RecordDate"]),
                            RecordType = (RecordType)Convert.ToInt32(reader["RecordType"]),
                            EmployeeName = $"{reader["FirstName"]} {reader["LastName"]}",
                            Identification = reader["Identification"].ToString()
                        };

                        records.Add(record);
                    }
                }
            }
            return records;
        }

        // Mantener el método original para compatibilidad
        public List<AttendanceRecord> GetAttendanceRecords(DateTime date)
        {
            return GetAttendanceRecords(date, date);
        }

        public void SaveFingerprintTemplate(int employeeId, string templateData)
        {
            try
            {
                // Eliminar templates existentes para este empleado
                string deleteSql = "DELETE FROM FingerprintTemplates WHERE EmployeeId = @EmployeeId";
                ExecuteNonQuery(deleteSql, new SQLiteParameter("@EmployeeId", employeeId));

                // Insertar nuevo template
                string insertSql = @"
                    INSERT INTO FingerprintTemplates (EmployeeId, TemplateData)
                    VALUES (@EmployeeId, @TemplateData)";

                ExecuteNonQuery(insertSql,
                    new SQLiteParameter("@EmployeeId", employeeId),
                    new SQLiteParameter("@TemplateData", templateData));

                Console.WriteLine($"✅ Template guardado para empleado {employeeId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error guardando template: {ex.Message}");
                throw;
            }
        }

        public string GetFingerprintTemplate(int employeeId)
        {
            try
            {
                string sql = "SELECT TemplateData FROM FingerprintTemplates WHERE EmployeeId = @EmployeeId ORDER BY CreatedDate DESC LIMIT 1";

                using (var command = new SQLiteCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("@EmployeeId", employeeId);
                    var result = command.ExecuteScalar();
                    return result?.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo template: {ex.Message}");
                return null;
            }
        }
        public bool UpdateEmployee(Employee employee)
        {
            try
            {
                Console.WriteLine($"💾 Actualizando empleado: {employee.FirstName} {employee.LastName}");

                string sql = @"UPDATE Employees 
                      SET FirstName = @FirstName, 
                          LastName = @LastName, 
                          Identification = @Identification,
                          IsActive = @IsActive
                      WHERE Id = @Id";

                using (var command = new SQLiteCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("@FirstName", employee.FirstName);
                    command.Parameters.AddWithValue("@LastName", employee.LastName);
                    command.Parameters.AddWithValue("@Identification", employee.Identification);
                    command.Parameters.AddWithValue("@IsActive", employee.IsActive);
                    command.Parameters.AddWithValue("@Id", employee.Id);

                    int rowsAffected = command.ExecuteNonQuery();
                    bool success = rowsAffected > 0;

                    Console.WriteLine(success ?
                        $"✅ Empleado actualizado: ID={employee.Id}" :
                        "❌ No se encontró el empleado para actualizar");

                    return success;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error actualizando empleado: {ex.Message}");
                throw;
            }
        }

        public bool DeleteEmployee(int employeeId)
        {
            try
            {
                Console.WriteLine($"🗑️ Eliminando empleado: ID={employeeId}");

                // Primero eliminar templates de huella asociados
                string deleteTemplatesSql = "DELETE FROM FingerprintTemplates WHERE EmployeeId = @EmployeeId";
                ExecuteNonQuery(deleteTemplatesSql, new SQLiteParameter("@EmployeeId", employeeId));

                // Luego eliminar registros de asistencia
                string deleteAttendanceSql = "DELETE FROM AttendanceRecords WHERE EmployeeId = @EmployeeId";
                ExecuteNonQuery(deleteAttendanceSql, new SQLiteParameter("@EmployeeId", employeeId));

                // Finalmente eliminar el empleado
                string deleteEmployeeSql = "DELETE FROM Employees WHERE Id = @Id";
                using (var command = new SQLiteCommand(deleteEmployeeSql, _connection))
                {
                    command.Parameters.AddWithValue("@Id", employeeId);
                    int rowsAffected = command.ExecuteNonQuery();
                    bool success = rowsAffected > 0;

                    Console.WriteLine(success ?
                        $"✅ Empleado eliminado: ID={employeeId}" :
                        "❌ No se encontró el empleado para eliminar");

                    return success;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error eliminando empleado: {ex.Message}");
                throw;
            }
        }
        public void CheckDatabaseState()
        {
            try
            {
                // Verificar si la tabla Employees existe y tiene datos
                string checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='Employees'";
                using (var command = new SQLiteCommand(checkTableSql, _connection))
                {
                    var result = command.ExecuteScalar();
                    if (result == null)
                    {
                        Console.WriteLine("❌ La tabla Employees NO existe");
                        return;
                    }
                }

                // Contar empleados
                string countSql = "SELECT COUNT(*) FROM Employees";
                using (var command = new SQLiteCommand(countSql, _connection))
                {
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    Console.WriteLine($"📊 Empleados en la base de datos: {count}");
                }

                // Mostrar todos los empleados
                string selectSql = "SELECT * FROM Employees";
                using (var command = new SQLiteCommand(selectSql, _connection))
                using (var reader = command.ExecuteReader())
                {
                    Console.WriteLine("👥 Lista de empleados:");
                    while (reader.Read())
                    {
                        Console.WriteLine($"   - ID: {reader["Id"]}, Nombre: {reader["FirstName"]} {reader["LastName"]}, Identificación: {reader["Identification"]}");
                    }
                }

                // Verificar templates
                string templateCountSql = "SELECT COUNT(*) FROM FingerprintTemplates";
                using (var command = new SQLiteCommand(templateCountSql, _connection))
                {
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    Console.WriteLine($"📋 Templates de huellas en la base de datos: {count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error verificando base de datos: {ex.Message}");
            }
        }

        public SQLiteConnection GetConnection()
        {
            return _connection;
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}