namespace NetClipboard
{
    using System.Runtime.InteropServices;

    public static class ShutdownHandler
    {
        // Объявляем делегат для нашего обработчика
        private delegate bool ConsoleCtrlDelegate(CtrlTypes ctrlType);

        // Импортируем функцию Win32 API
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handler, bool add);

        // Перечисление типов событий, которые может получить обработчик
        private enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        // Событие, которое будет вызвано при получении сигнала о завершении
        public static event Action? Shutdown;

        // Статическая ссылка на делегат, чтобы сборщик мусора его не удалил
        private static readonly ConsoleCtrlDelegate _handler = new ConsoleCtrlDelegate(ConsoleCtrlCheck);

        public static void Register()
        {
            // Регистрируем наш обработчик
            SetConsoleCtrlHandler(_handler, true);
        }

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            // Мы обрабатываем только события закрытия, лог-оффа и шатдауна.
            // Ctrl+C обрабатывается отдельно через Console.CancelKeyPress для простоты.
            switch (ctrlType)
            {
                case CtrlTypes.CTRL_CLOSE_EVENT:
                case CtrlTypes.CTRL_LOGOFF_EVENT:
                case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                    Console.WriteLine("[System] Console window is closing, performing cleanup...");
                    // Вызываем наше событие, на которое подпишется основной код
                    Shutdown?.Invoke();
                    // Даем приложению немного времени на завершение, прежде чем ОС его принудительно закроет
                    Thread.Sleep(5000);
                    return true; // Возвращаем true, чтобы показать, что мы обработали событие
            }
            return false; // Возвращаем false для всех остальных событий (например, Ctrl+C), чтобы их обработали другие обработчики
        }
    }
}
