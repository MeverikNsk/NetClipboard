namespace NetClipboard
{
    using System.Collections.Specialized;
    using System.IO;
    using System.Windows;    
    using System.Windows.Media.Imaging;

    public class Program
    {
        private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);
        private static bool _isExiting = false;
        private static readonly object _exitLock = new object();

        // Переменная для хранения хеша последнего уникального содержимого
        private static string? _lastClipboardHash = null;

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Clipboard Monitor starting...");
            Console.WriteLine("Monitoring text, images, and file drops.");
            Console.WriteLine("Saved files will appear in the application's directory.");

            // 1. Регистрируем обработчики завершения
            // Для Ctrl+C
            Console.CancelKeyPress += OnCancelKeyPress;
            // Для закрытия окна через "крестик"
            ShutdownHandler.Shutdown += OnShutdown;
            ShutdownHandler.Register();
            // Событие завершения процесса .NET
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            
            // Подписываемся на событие изменения буфера обмена
            ClipboardListener.ClipboardUpdated += OnClipboardChanged;

            // Запускаем прослушивание
            ClipboardListener.Start();

            Console.WriteLine("Monitoring is active. Press [Ctrl-C] to exit.");

            // Поток блокируется здесь до тех пор, пока не будет вызван _exitEvent.Set()
            _exitEvent.WaitOne();            
        }

        // Единый метод для выполнения очистки.
        // Он защищен от многократного выполнения.
        private static void PerformCleanup()
        {
            lock (_exitLock)
            {
                if (_isExiting) return;
                _isExiting = true;
            }

            // Останавливаем прослушивание перед выходом            
            Console.WriteLine("Shutdown initiated. Stopping listener...");           
            ClipboardListener.Stop();
            Console.WriteLine("Clipboard Monitor stopped.");

            // Посылаем сигнал основному потоку для завершения WaitOne()
            _exitEvent.Set();
        }

        // Обработчик события Console.CancelKeyPress (Ctrl+C)
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {            
            e.Cancel = true;
            PerformCleanup();
        }

        // Обработчик события из нашего ShutdownHandler (закрытие окна)
        private static void OnShutdown()
        {
            PerformCleanup();
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            PerformCleanup();
        }

        private static void OnClipboardChanged(object? sender, EventArgs e)
        {
            // Обертываем в try-catch, так как буфер обмена может быть занят другим процессом
            try
            {
                string? newHash = null;
                object? clipboardContent = null;

                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();

                    if (!string.IsNullOrEmpty(text))
                    {
                        newHash = ClipboardHasher.GetHash(text);
                        clipboardContent = text;
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    var image = Clipboard.GetImage();

                    if (image != null)
                    {
                        newHash = ClipboardHasher.GetHash(image);
                        clipboardContent = image;
                    }
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var fileList = Clipboard.GetFileDropList();

                    if (fileList != null && fileList.Count > 0)
                    {
                        newHash = ClipboardHasher.GetHash(fileList);
                        clipboardContent = fileList;
                    }
                }
                
                if (newHash == null || newHash == _lastClipboardHash)
                {                    
                    return;
                }

                _lastClipboardHash = newHash;

                // Вызываем соответствующий обработчик с уже полученными данными
                switch (clipboardContent)
                {
                    case string text:
                        HandleText(text);
                        break;
                    case BitmapSource image:
                        HandleImage(image);
                        break;
                    case StringCollection fileList:
                        HandleFileDrop(fileList);
                        break;
                }

            }
            catch (Exception ex)
            {
                // COMException может возникнуть, если буфер занят
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error accessing clipboard: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void HandleText(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[TEXT] Copied: \"{text.Substring(0, Math.Min(50, text.Length))}...\"");
            Console.ResetColor();

            string fileName = $"clipboard_{GetTimestamp()}.txt";
            File.WriteAllText(fileName, text);
            Console.WriteLine($" -> Saved to {fileName}");
        }

        private static void HandleImage(BitmapSource imageSource)
        {
            if (imageSource == null) return;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[IMAGE] Copied: {imageSource.Width}x{imageSource.Height} pixels");
            Console.ResetColor();

            string fileName = $"clipboard_{GetTimestamp()}.png";

            // Для сохранения BitmapSource (из WPF) нам нужно его конвертировать
            using (var fileStream = new FileStream(fileName, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(imageSource));
                encoder.Save(fileStream);
            }

            Console.WriteLine($" -> Saved to {fileName}");
        }


        private static void HandleFileDrop(StringCollection fileList)
        {
            if (fileList == null || fileList.Count == 0) return;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[FILES] Copied {fileList.Count} file(s)/folder(s):");
            foreach (var file in fileList)
            {
                Console.WriteLine($"  - {file}");
            }
            Console.ResetColor();

            string fileName = $"clipboard_{GetTimestamp()}_files.txt";
            var list = new List<string>();
            foreach (var fileItem in fileList)
            {
                if (!string.IsNullOrWhiteSpace(fileItem))
                {
                    list.Add(fileItem);
                }
            }
            File.WriteAllLines(fileName, list);
            
            Console.WriteLine($" -> Saved file list to {fileName}");
        }

        private static string GetTimestamp()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        }
    }
}