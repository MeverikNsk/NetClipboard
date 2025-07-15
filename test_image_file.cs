namespace ClipboardMonitor
{
    /// <summary>
    /// Тестовый класс для демонстрации работы с изображениями и файлами
    /// </summary>
    public class ImageFileTest
    {
        public static void TestImageClipboard()
        {
            Console.WriteLine("Тестирование поддержки изображений:");

            // Создаем тестовое изображение
            using (var bitmap = new Bitmap(100, 100))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.FillRectangle(Brushes.Red, 0, 0, 50, 50);
                    graphics.FillRectangle(Brushes.Blue, 50, 50, 50, 50);
                }

                // Помещаем изображение в буфер обмена
                Clipboard.SetImage(bitmap);
                Console.WriteLine("✓ Тестовое изображение 100x100 помещено в буфер обмена");
            }
        }

        public static void TestFileClipboard()
        {
            Console.WriteLine("Тестирование поддержки файлов:");

            // Создаем тестовые файлы
            string[] testFiles = {
                Path.Combine(Path.GetTempPath(), "test1.txt"),
                Path.Combine(Path.GetTempPath(), "test2.txt"),
                Path.Combine(Path.GetTempPath(), "test3.txt")
            };

            foreach (var file in testFiles)
            {
                File.WriteAllText(file, $"Тестовый файл: {Path.GetFileName(file)}\nВремя создания: {DateTime.Now}");
            }

            // Помещаем файлы в буфер обмена
            var fileCollection = new System.Collections.Specialized.StringCollection();
            fileCollection.AddRange(testFiles);
            Clipboard.SetFileDropList(fileCollection);

            Console.WriteLine($"✓ {testFiles.Length} тестовых файлов помещены в буфер обмена");
        }

        public static void TestTextClipboard()
        {
            Console.WriteLine("Тестирование поддержки текста:");

            string testText = "Тестовый текст для Windows Clipboard Monitor\n" +
                             "Поддерживает Unicode: αβγδε 中文 العربية\n" +
                             "Время: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            Clipboard.SetText(testText);
            Console.WriteLine("✓ Тестовый текст помещен в буфер обмена");
        }

        public static void RunAllTests()
        {
            Console.WriteLine("=== ТЕСТИРОВАНИЕ ВСЕХ ТИПОВ ДАННЫХ ===");
            Console.WriteLine();

            TestTextClipboard();
            System.Threading.Thread.Sleep(2000);

            TestImageClipboard();
            System.Threading.Thread.Sleep(2000);

            TestFileClipboard();
            System.Threading.Thread.Sleep(2000);

            Console.WriteLine();
            Console.WriteLine("=== ТЕСТИРОВАНИЕ ЗАВЕРШЕНО ===");
        }
    }
}