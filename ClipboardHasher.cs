namespace NetClipboard
{
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Windows.Media.Imaging;
    using System.Collections.Specialized;

    public static class ClipboardHasher
    {
        // Рассчитывает хеш для массива байт
        private static string CalculateHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(data);
                // Преобразуем массив байт в строку шестнадцатеричного формата
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        // Получает байты и вычисляет хеш для текста
        public static string GetHash(string text)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(text);
            return CalculateHash(dataBytes);
        }

        // Получает байты и вычисляет хеш для изображения
        public static string? GetHash(BitmapSource imageSource)
        {
            // Чтобы получить стабильный хеш, мы должны сериализовать изображение
            // в стандартизированный формат, например, PNG, в поток в памяти.
            using (var stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(imageSource));
                encoder.Save(stream);
                byte[] dataBytes = stream.ToArray();
                return CalculateHash(dataBytes);
            }
        }

        // Получает байты и вычисляет хеш для списка файлов
        public static string GetHash(StringCollection fileList)
        {
            // Важно: сортируем список файлов, чтобы порядок выделения
            // не влиял на хеш-сумму.
            var sortedPaths = fileList.Cast<string>().OrderBy(f => f);
            string combinedPaths = string.Join(Environment.NewLine, sortedPaths);
            byte[] dataBytes = Encoding.UTF8.GetBytes(combinedPaths);
            return CalculateHash(dataBytes);
        }
    }
}
