using System.Text.Json.Serialization;
using System.Text;

namespace ClipboardMonitor
{
    /// <summary>
    /// Типы данных буфера обмена
    /// </summary>
    public enum ClipboardDataType
    {
        Text,
        Image,
        Files,
        Unknown
    }

    /// <summary>
    /// Класс для представления данных буфера обмена
    /// </summary>
    public class ClipboardData
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("type")]
        public ClipboardDataType Type { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();

        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }

        [JsonPropertyName("original_length")]
        public int OriginalLength { get; set; }

        [JsonPropertyName("truncated")]
        public bool Truncated { get; set; }

        /// <summary>
        /// Создать объект для текстовых данных
        /// </summary>
        public static ClipboardData CreateTextData(string text, int maxLength = 10000)
        {
            var data = new ClipboardData
            {
                Timestamp = DateTime.Now,
                Type = ClipboardDataType.Text,
                OriginalLength = text.Length,
                Truncated = text.Length > maxLength
            };

            if (data.Truncated)
            {
                data.Content = text.Substring(0, maxLength) + "\n... [ОБРЕЗАНО]";
            }
            else
            {
                data.Content = text;
            }

            data.Metadata["encoding"] = "UTF-8";
            data.Metadata["line_count"] = text.Split('\n').Length;
            data.Metadata["word_count"] = text.Split(new char[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries).Length;

            return data;
        }

        /// <summary>
        /// Создать объект для данных изображения (из строки описания)
        /// </summary>
        public static ClipboardData CreateImageData(string imageInfo, int sizeBytes = 0)
        {
            var data = new ClipboardData
            {
                Timestamp = DateTime.Now,
                Type = ClipboardDataType.Image,
                Content = imageInfo,
                OriginalLength = sizeBytes
            };

            data.Metadata["size_bytes"] = sizeBytes;
            data.Metadata["format"] = "Native Windows Format";

            return data;
        }

        /// <summary>
        /// Создать объект для данных изображения (из байтов)
        /// </summary>
        public static ClipboardData CreateImageData(byte[] imageData, int width = 0, int height = 0)
        {
            var data = new ClipboardData
            {
                Timestamp = DateTime.Now,
                Type = ClipboardDataType.Image
            };

            data.Metadata["width"] = width;
            data.Metadata["height"] = height;
            data.Metadata["pixel_format"] = "PNG";
            data.Metadata["dpi_x"] = 96;
            data.Metadata["dpi_y"] = 96;

            // Конвертировать изображение в Base64 для JSON
            data.Content = Convert.ToBase64String(imageData);
            data.Metadata["format"] = "PNG";
            data.Metadata["size_bytes"] = imageData.Length;

            return data;
        }

        /// <summary>
        /// Создать объект для файловых данных (из строки списка файлов)
        /// </summary>
        public static ClipboardData CreateFileData(string fileList, int fileCount)
        {
            var data = new ClipboardData
            {
                Timestamp = DateTime.Now,
                Type = ClipboardDataType.Files,
                Content = fileList,
                OriginalLength = fileCount
            };

            data.Metadata["file_count"] = fileCount;
            data.Metadata["format"] = "File Drop";

            return data;
        }

        /// <summary>
        /// Создать объект для файловых данных (из массива путей)
        /// </summary>
        public static ClipboardData CreateFilesData(string[] filePaths)
        {
            var data = new ClipboardData
            {
                Timestamp = DateTime.Now,
                Type = ClipboardDataType.Files,
                Content = string.Join("\n", filePaths)
            };

            data.Metadata["file_count"] = filePaths.Length;
            data.Metadata["files"] = filePaths;

            // Подсчитать общий размер файлов
            long totalSize = 0;
            int existingFiles = 0;

            foreach (string filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        totalSize += fileInfo.Length;
                        existingFiles++;
                    }
                }
                catch
                {
                    // Игнорируем ошибки доступа к файлам
                }
            }

            data.Metadata["total_size_bytes"] = totalSize;
            data.Metadata["existing_files"] = existingFiles;
            data.Metadata["total_size_formatted"] = Logger.FormatFileSize(totalSize);

            return data;
        }

        /// <summary>
        /// Получить имя файла для сохранения
        /// </summary>
        public string GetFileName()
        {
            string timestamp = Timestamp.ToString("yyyyMMdd_HHmmss_fff");
            string prefix = Type switch
            {
                ClipboardDataType.Text => "clipboard_text",
                ClipboardDataType.Image => "clipboard_image",
                ClipboardDataType.Files => "clipboard_files",
                _ => "clipboard_unknown"
            };

            string extension = Type switch
            {
                ClipboardDataType.Text => "txt",
                ClipboardDataType.Image => "png",
                ClipboardDataType.Files => "json",
                _ => "txt"
            };

            return $"{prefix}_{timestamp}.{extension}";
        }

        /// <summary>
        /// Получить имя файла метаданных
        /// </summary>
        public string GetMetadataFileName()
        {
            string timestamp = Timestamp.ToString("yyyyMMdd_HHmmss_fff");
            string prefix = Type switch
            {
                ClipboardDataType.Text => "clipboard_text",
                ClipboardDataType.Image => "clipboard_image",
                ClipboardDataType.Files => "clipboard_files",
                _ => "clipboard_unknown"
            };

            return $"{prefix}_{timestamp}.json";
        }

        /// <summary>
        /// Получить хеш содержимого для определения изменений
        /// </summary>
        public string GetContentHash()
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(Content));
                return Convert.ToHexString(hashBytes);
            }
        }

        /// <summary>
        /// Преобразовать в JSON
        /// </summary>
        public string ToJson(bool indented = true)
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = indented,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return System.Text.Json.JsonSerializer.Serialize(this, options);
        }

        /// <summary>
        /// Создать из JSON
        /// </summary>
        public static ClipboardData? FromJson(string json)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<ClipboardData>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}