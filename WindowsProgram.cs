namespace ClipboardMonitor
{
    /// <summary>
    /// Главный класс Windows-приложения для мониторинга буфера обмена
    /// </summary>
    internal class WindowsProgram
    {
        private static WindowsClipboardMonitor? _monitor;
        private static readonly object _lock = new object();

        /// <summary>
        /// Точка входа в приложение
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // Проверяем, что мы работаем на Windows
                if (!OperatingSystem.IsWindows())
                {
                    Console.WriteLine("❌ Данная версия приложения предназначена только для Windows!");
                    Console.WriteLine("   Для других платформ используйте кроссплатформенную версию.");
                    Environment.Exit(1);
                    return;
                }

                // Настраиваем кодировку консоли для правильного отображения русских символов
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.InputEncoding = System.Text.Encoding.UTF8;

                // Настраиваем Windows Forms для работы с хуками
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Обработчики для корректного завершения работы
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                Console.CancelKeyPress += OnCancelKeyPress;

                // Создаем и запускаем монитор
                lock (_lock)
                {
                    _monitor = new WindowsClipboardMonitor();
                    _monitor.StartMonitoring();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Критическая ошибка приложения: {ex.Message}");
                Console.WriteLine($"   Детали: {ex.StackTrace}");

                // Записываем ошибку в лог, если возможно
                try
                {
                    var logger = new Logger("error.log");
                    logger.Error("Критическая ошибка приложения", ex);
                }
                catch
                {
                    // Игнорируем ошибки логирования при критических сбоях
                }

                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Обработчик завершения процесса
        /// </summary>
        private static void OnProcessExit(object? sender, EventArgs e)
        {
            Console.WriteLine("\n⏹️  Завершение работы приложения...");
            StopMonitoring();
        }

        /// <summary>
        /// Обработчик Ctrl+C
        /// </summary>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Console.WriteLine("\n⏹️  Получен сигнал завершения...");
            StopMonitoring();
        }

        /// <summary>
        /// Остановить мониторинг
        /// </summary>
        private static void StopMonitoring()
        {
            lock (_lock)
            {
                if (_monitor != null)
                {
                    try
                    {
                        _monitor.StopMonitoring();
                        _monitor.Dispose();
                        _monitor = null;

                        Console.WriteLine("✅ Мониторинг буфера обмена остановлен");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Ошибка остановки мониторинга: {ex.Message}");
                    }
                }
            }

            // Даем время на завершение операций
            Thread.Sleep(500);
            Environment.Exit(0);
        }
    }
}