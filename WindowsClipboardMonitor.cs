using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

namespace ClipboardMonitor
{
    /// <summary>
    /// Windows-специфичный монитор буфера обмена с использованием хуков
    /// </summary>
    public class WindowsClipboardMonitor : Form
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int WM_DRAWCLIPBOARD = 0x0308;
        private const int WM_CHANGECBCHAIN = 0x030D;

        private Config _config;
        private Logger _logger;
        private IntPtr _nextClipboardViewer;
        private bool _isRunning;
        private int _clipboardUpdateCount;
        private DateTime _lastUpdateTime;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalSize(IntPtr hMem);

        [DllImport("shell32.dll")]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, uint cch);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern uint EnumClipboardFormats(uint format);

        private const uint CF_TEXT = 1;
        private const uint CF_BITMAP = 2;
        private const uint CF_UNICODETEXT = 13;
        private const uint CF_HDROP = 15;
        private const uint CF_DIB = 8;

        public WindowsClipboardMonitor()
        {
            _config = new Config();
            _logger = new Logger(_config.LogFile);
            _clipboardUpdateCount = 0;
            _lastUpdateTime = DateTime.Now;

            // Создаем невидимое окно для получения сообщений
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;
            this.CreateHandle();

            // Создаем выходные папки
            CreateOutputDirectories();

            _logger.Info("Windows Clipboard Monitor инициализирован");
        }

        /// <summary>
        /// Создать выходные папки
        /// </summary>
        private void CreateOutputDirectories()
        {
            try
            {
                string baseDir = Path.GetFullPath(_config.OutputDirectory);
                Directory.CreateDirectory(baseDir);
                Directory.CreateDirectory(Path.Combine(baseDir, "text"));
                Directory.CreateDirectory(Path.Combine(baseDir, "images"));
                Directory.CreateDirectory(Path.Combine(baseDir, "files"));

                _logger.Info($"Выходные папки созданы: {baseDir}");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка создания выходных папок", ex);
            }
        }

        /// <summary>
        /// Запустить мониторинг буфера обмена
        /// </summary>
        public void StartMonitoring()
        {
            try
            {
                // Регистрируем слушатель буфера обмена (Windows Vista+)
                if (!AddClipboardFormatListener(this.Handle))
                {
                    _logger.Warning("Не удалось зарегистрировать слушатель буфера обмена, используем fallback");
                    // Fallback для старых версий Windows
                    _nextClipboardViewer = SetClipboardViewer(this.Handle);
                }

                _isRunning = true;
                _logger.Info("Мониторинг буфера обмена запущен");

                ShowWelcomeMessage();

                // Запускаем цикл обработки сообщений
                Application.Run(this);
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка запуска мониторинга", ex);
                throw;
            }
        }

        /// <summary>
        /// Остановить мониторинг
        /// </summary>
        public void StopMonitoring()
        {
            try
            {
                _isRunning = false;

                // Отменяем регистрацию слушателя
                RemoveClipboardFormatListener(this.Handle);

                if (_nextClipboardViewer != IntPtr.Zero)
                {
                    ChangeClipboardChain(this.Handle, _nextClipboardViewer);
                }

                _logger.Info("Мониторинг буфера обмена остановлен");
                Application.Exit();
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка остановки мониторинга", ex);
            }
        }

        /// <summary>
        /// Показать приветственное сообщение
        /// </summary>
        private void ShowWelcomeMessage()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            WINDOWS CLIPBOARD MONITOR С ХУКАМИ               ║");
            Console.WriteLine("║                     Версия 2.0                              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("┌─ КОНФИГУРАЦИЯ ──────────────────────────────────────────────┐");
            Console.WriteLine($"│ Папка для сохранения: {_config.OutputDirectory.PadRight(37)} │");
            Console.WriteLine($"│ Файл логов: {_config.LogFile.PadRight(45)} │");
            Console.WriteLine($"│ Макс. длина текста: {_config.MaxTextLength} символов{new string(' ', 20)} │");
            Console.WriteLine($"│ Сохранение изображений: {(_config.SaveImages ? "Вкл" : "Выкл").PadRight(37)} │");
            Console.WriteLine($"│ Сохранение файлов: {(_config.SaveFiles ? "Вкл" : "Выкл").PadRight(40)} │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("┌─ УПРАВЛЕНИЕ ────────────────────────────────────────────────┐");
            Console.WriteLine("│ Ctrl+C - Завершить мониторинг                              │");
            Console.WriteLine("│ Изменения буфера обмена отслеживаются мгновенно            │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("🚀 Мониторинг запущен! Изменения буфера обмена отслеживаются через системные хуки...");
            Console.WriteLine();

            // Обработчик для Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n⏹️  Остановка мониторинга...");
                StopMonitoring();
            };
        }

        /// <summary>
        /// Обработка сообщений Windows
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            try
            {
                switch (m.Msg)
                {
                    case WM_CLIPBOARDUPDATE:
                        // Современный способ (Windows Vista+)
                        OnClipboardChanged();
                        break;

                    case WM_DRAWCLIPBOARD:
                        // Старый способ (Windows XP и ранее)
                        OnClipboardChanged();
                        if (_nextClipboardViewer != IntPtr.Zero)
                        {
                            SendMessage(_nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                        }
                        break;

                    case WM_CHANGECBCHAIN:
                        if (m.WParam == _nextClipboardViewer)
                        {
                            _nextClipboardViewer = m.LParam;
                        }
                        else if (_nextClipboardViewer != IntPtr.Zero)
                        {
                            SendMessage(_nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка обработки сообщения Windows", ex);
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// Обработчик изменения буфера обмена
        /// </summary>
        private void OnClipboardChanged()
        {
            if (!_isRunning) return;

            try
            {
                _clipboardUpdateCount++;
                _lastUpdateTime = DateTime.Now;

                var clipboardData = GetClipboardContent();
                if (clipboardData != null)
                {
                    SaveClipboardContent(clipboardData);

                    // Показать информацию о сохраненном элементе
                    string info = clipboardData.Type switch
                    {
                        ClipboardDataType.Text => $"{clipboardData.OriginalLength} символов",
                        ClipboardDataType.Image => $"{clipboardData.OriginalLength} байт",
                        ClipboardDataType.Files => $"{clipboardData.OriginalLength} файлов",
                        _ => "неизвестный тип"
                    };
                    Console.WriteLine($"✓ Сохранено: {clipboardData.Type} - {info} (Всего: {_clipboardUpdateCount})");
                    _logger.Info($"Сохранен элемент {clipboardData.Type}: {Path.GetFileName(clipboardData.FilePath)}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка обработки изменения буфера обмена", ex);
            }
        }

        /// <summary>
        /// Получить содержимое буфера обмена
        /// </summary>
        private ClipboardData? GetClipboardContent()
        {
            try
            {
                if (!OpenClipboard(this.Handle))
                {
                    _logger.Warning("Не удалось открыть буфер обмена");
                    return null;
                }

                try
                {
                    // Проверяем текст Unicode
                    if (IsClipboardFormatAvailable(CF_UNICODETEXT))
                    {
                        IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                        if (hData != IntPtr.Zero)
                        {
                            IntPtr pData = GlobalLock(hData);
                            if (pData != IntPtr.Zero)
                            {
                                try
                                {
                                    string text = Marshal.PtrToStringUni(pData);
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        return ClipboardData.CreateTextData(text, _config.MaxTextLength);
                                    }
                                }
                                finally
                                {
                                    GlobalUnlock(hData);
                                }
                            }
                        }
                    }

                    // Проверяем обычный текст
                    if (IsClipboardFormatAvailable(CF_TEXT))
                    {
                        IntPtr hData = GetClipboardData(CF_TEXT);
                        if (hData != IntPtr.Zero)
                        {
                            IntPtr pData = GlobalLock(hData);
                            if (pData != IntPtr.Zero)
                            {
                                try
                                {
                                    string text = Marshal.PtrToStringAnsi(pData);
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        return ClipboardData.CreateTextData(text, _config.MaxTextLength);
                                    }
                                }
                                finally
                                {
                                    GlobalUnlock(hData);
                                }
                            }
                        }
                    }

                    // Проверяем файлы (CF_HDROP)
                    if (_config.SaveFiles && IsClipboardFormatAvailable(CF_HDROP))
                    {
                        var fileData = GetClipboardFiles();
                        if (fileData != null)
                        {
                            return fileData;
                        }
                    }

                    // Проверяем изображения (CF_DIB)
                    if (_config.SaveImages && IsClipboardFormatAvailable(CF_DIB))
                    {
                        var imageData = GetClipboardImage();
                        if (imageData != null)
                        {
                            return imageData;
                        }
                    }

                    // Проверяем bitmap изображения (CF_BITMAP)
                    if (_config.SaveImages && IsClipboardFormatAvailable(CF_BITMAP))
                    {
                        var bitmapData = GetClipboardBitmap();
                        if (bitmapData != null)
                        {
                            return bitmapData;
                        }
                    }

                    return null;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка получения содержимого буфера обмена", ex);
                return null;
            }
        }

        /// <summary>
        /// Получить файлы из буфера обмена
        /// </summary>
        private ClipboardData? GetClipboardFiles()
        {
            try
            {
                IntPtr hData = GetClipboardData(CF_HDROP);
                if (hData == IntPtr.Zero)
                    return null;

                IntPtr pData = GlobalLock(hData);
                if (pData == IntPtr.Zero)
                    return null;

                try
                {
                    var files = new List<string>();
                    uint fileCount = DragQueryFile(hData, 0xFFFFFFFF, null, 0);

                    for (uint i = 0; i < fileCount; i++)
                    {
                        var fileName = new StringBuilder(260);
                        if (DragQueryFile(hData, i, fileName, 260) > 0)
                        {
                            files.Add(fileName.ToString());
                        }
                    }

                    if (files.Count > 0)
                    {
                        var fileList = string.Join(Environment.NewLine, files);
                        return ClipboardData.CreateFileData(fileList, files.Count);
                    }
                }
                finally
                {
                    GlobalUnlock(hData);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка получения файлов из буфера обмена", ex);
            }

            return null;
        }

        /// <summary>
        /// Получить изображение из буфера обмена (CF_DIB)
        /// </summary>
        private ClipboardData? GetClipboardImage()
        {
            try
            {
                IntPtr hData = GetClipboardData(CF_DIB);
                if (hData == IntPtr.Zero)
                    return null;

                IntPtr pData = GlobalLock(hData);
                if (pData == IntPtr.Zero)
                    return null;

                try
                {
                    IntPtr size = GlobalSize(hData);
                    if (size == IntPtr.Zero)
                        return null;

                    // Создаем описание изображения
                    var imageInfo = $"DIB изображение ({size.ToInt32()} байт)";
                    return ClipboardData.CreateImageData(imageInfo, size.ToInt32());
                }
                finally
                {
                    GlobalUnlock(hData);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка получения DIB изображения из буфера обмена", ex);
            }

            return null;
        }

        /// <summary>
        /// Получить bitmap изображение из буфера обмена (CF_BITMAP)
        /// </summary>
        private ClipboardData? GetClipboardBitmap()
        {
            try
            {
                IntPtr hBitmap = GetClipboardData(CF_BITMAP);
                if (hBitmap == IntPtr.Zero)
                    return null;

                // Создаем описание bitmap
                var bitmapInfo = "Bitmap изображение";
                return ClipboardData.CreateImageData(bitmapInfo, 0);
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка получения bitmap изображения из буфера обмена", ex);
            }

            return null;
        }

        /// <summary>
        /// Сохранить содержимое буфера обмена
        /// </summary>
        private void SaveClipboardContent(ClipboardData data)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string subDir = data.Type switch
                {
                    ClipboardDataType.Text => "text",
                    ClipboardDataType.Image => "images",
                    ClipboardDataType.Files => "files",
                    _ => "unknown"
                };

                string fileName = data.GetFileName();
                string jsonFileName = data.GetMetadataFileName();

                string typeDir = Path.Combine(_config.OutputDirectory, subDir);
                string filePath = Path.Combine(typeDir, fileName);
                string jsonPath = Path.Combine(typeDir, jsonFileName);

                // Создаем папку если не существует
                Directory.CreateDirectory(typeDir);

                // Обновляем путь к файлу
                data.FilePath = filePath;

                // Сохраняем основной файл
                var content = new StringBuilder();
                content.AppendLine($"Время: {data.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                content.AppendLine($"Платформа: Windows");
                content.AppendLine($"Тип: {data.Type}");

                switch (data.Type)
                {
                    case ClipboardDataType.Text:
                        content.AppendLine($"Длина: {data.OriginalLength} символов");
                        break;
                    case ClipboardDataType.Image:
                        content.AppendLine($"Размер: {data.OriginalLength} байт");
                        break;
                    case ClipboardDataType.Files:
                        content.AppendLine($"Количество файлов: {data.OriginalLength}");
                        break;
                }

                content.AppendLine("Режим: Реальный мониторинг (хуки)");
                content.AppendLine(new string('-', 50));
                content.AppendLine(data.Content);

                File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);

                // Сохраняем JSON метаданные
                File.WriteAllText(jsonPath, data.ToJson(), Encoding.UTF8);

                _logger.Info($"Содержимое буфера обмена сохранено: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка сохранения содержимого буфера обмена", ex);
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopMonitoring();
            }
            base.Dispose(disposing);
        }
    }
}