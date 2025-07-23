namespace NetClipboard
{
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    using Application = Application;

    public static class ClipboardListener
    {
        public static event EventHandler? ClipboardUpdated;

        private static NotificationForm? _form;
        private static Thread? _thread;

        public static void Start()
        {
            // Запускаем прослушивание в отдельном потоке,
            // чтобы не блокировать основной поток консоли.
            _thread = new Thread(() =>
            {
                // Создаем скрытую форму для получения сообщений Windows
                _form = new NotificationForm();
                Application.Run(_form);
            })
            {
                // Поток должен быть STA для работы с буфером обмена
                IsBackground = true
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public static void Stop()
        {
            try
            {
                if (_form != null)
                {
                    // Завершаем цикл сообщений и закрываем форму
                    _form.Invoke(new Action(() => _form.Close()));
                    _thread?.Join(); // Ожидаем завершения потока
                }
            }
            catch
            {
                // Nothing
            }
        }

        // Внутренний класс, представляющий собой скрытое окно для перехвата сообщений
        private class NotificationForm : Form
        {
            private const int WM_CLIPBOARDUPDATE = 0x031D;
            private const int WM_DRAWCLIPBOARD = 0x0308;
            private const int WM_CHANGECBCHAIN = 0x030D;

            private const int GWL_EXSTYLE = -20;
            private const int WS_EX_APPWINDOW = 0x00040000;
            private const int WS_EX_TOOLWINDOW = 0x00000080;

            private IntPtr _nextClipboardViewer;

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool AddClipboardFormatListener(IntPtr hwnd);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("user32.dll")]
            static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            public NotificationForm()
            {
                // Скрываем форму, чтобы она не была видна пользователю
                Visible = false;
                ShowInTaskbar = false;
                Load += (s, e) =>
                {
                    if (!AddClipboardFormatListener(Handle))
                    {
                        // Fallback для старых версий Windows
                        _nextClipboardViewer = SetClipboardViewer(this.Handle);
                    }

                    HideWindowFromTaskbarAndTaskManager(this.Handle);
                };
                FormClosed += (s, e) =>
                {
                    RemoveClipboardFormatListener(Handle);

                    if (_nextClipboardViewer != IntPtr.Zero)
                    {
                        ChangeClipboardChain(this.Handle, _nextClipboardViewer);
                    }
                };
                WindowState = FormWindowState.Minimized;
            }

            private void HideWindowFromTaskbarAndTaskManager(IntPtr hWnd)
            {
                // Получаем текущие стили окна
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

                // Устанавливаем новые стили
                exStyle = (exStyle & ~WS_EX_APPWINDOW) | WS_EX_TOOLWINDOW;
                SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
            }

            // Переопределяем оконную процедуру для обработки сообщений
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
                catch
                {
                    // Nothing
                }

                base.WndProc(ref m);
            }

            private void OnClipboardChanged()
            {
                // Содержимое буфера обмена изменилось, вызываем наше событие
                ClipboardUpdated?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
