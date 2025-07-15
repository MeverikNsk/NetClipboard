using System.Text;

namespace ClipboardMonitor
{
    /// <summary>
    /// Класс для ведения логов приложения
    /// </summary>
    public class Logger
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public Logger(string logFilePath)
        {
            _logFilePath = logFilePath;

            // Создать папку для логов если не существует
            string? directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Записать информационное сообщение
        /// </summary>
        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// Записать предупреждение
        /// </summary>
        public void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        /// <summary>
        /// Записать ошибку
        /// </summary>
        public void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        /// <summary>
        /// Записать ошибку с исключением
        /// </summary>
        public void Error(string message, Exception ex)
        {
            WriteLog("ERROR", $"{message}: {ex.Message}");
        }

        /// <summary>
        /// Записать отладочное сообщение
        /// </summary>
        public void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        /// <summary>
        /// Основной метод записи в лог
        /// </summary>
        private void WriteLog(string level, string message)
        {
            try
            {
                lock (_lockObject)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logMessage = $"[{timestamp}] [{level}] {message}";

                    // Вывод в консоль
                    Console.WriteLine(logMessage);

                    // Запись в файл
                    using (var writer = new StreamWriter(_logFilePath, true, Encoding.UTF8))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка записи в лог: {ex.Message}");
            }
        }

        /// <summary>
        /// Очистить старые логи
        /// </summary>
        public void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.CreationTime < DateTime.Now.AddDays(-daysToKeep))
                    {
                        string backupPath = _logFilePath.Replace(".log", $"_backup_{DateTime.Now:yyyyMMdd}.log");
                        File.Move(_logFilePath, backupPath);
                        Info($"Старый лог перемещен в: {backupPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Ошибка очистки старых логов", ex);
            }
        }

        /// <summary>
        /// Получить размер лог-файла
        /// </summary>
        public long GetLogFileSize()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    return new FileInfo(_logFilePath).Length;
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
            return 0;
        }

        /// <summary>
        /// Форматировать размер файла
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}