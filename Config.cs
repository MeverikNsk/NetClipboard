using System.Text.Json.Serialization;

namespace ClipboardMonitor
{
    /// <summary>
    /// Класс для настроек приложения
    /// </summary>
    public class Config
    {
        [JsonPropertyName("output_directory")]
        public string OutputDirectory { get; set; } = "clipboard_output";

        [JsonPropertyName("log_file")]
        public string LogFile { get; set; } = "clipboard_monitor.log";

        [JsonPropertyName("polling_interval")]
        public int PollingInterval { get; set; } = 500; // миллисекунды

        [JsonPropertyName("max_text_length")]
        public int MaxTextLength { get; set; } = 10000;

        [JsonPropertyName("save_images")]
        public bool SaveImages { get; set; } = true;

        [JsonPropertyName("save_files")]
        public bool SaveFiles { get; set; } = true;

        [JsonPropertyName("auto_cleanup_days")]
        public int AutoCleanupDays { get; set; } = 30;

        /// <summary>
        /// Загрузить конфигурацию из файла
        /// </summary>
        public static Config LoadFromFile(string configPath = "config.json")
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<Config>(json);
                    return config ?? new Config();
                }
                else
                {
                    // Создать конфигурацию по умолчанию
                    var defaultConfig = new Config();
                    defaultConfig.SaveToFile(configPath);
                    return defaultConfig;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации: {ex.Message}");
                Console.WriteLine("Используется конфигурация по умолчанию");
                return new Config();
            }
        }

        /// <summary>
        /// Сохранить конфигурацию в файл
        /// </summary>
        public void SaveToFile(string configPath = "config.json")
        {
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения конфигурации: {ex.Message}");
            }
        }

        /// <summary>
        /// Валидация настроек
        /// </summary>
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                Console.WriteLine("Ошибка: не указана папка для сохранения");
                return false;
            }

            if (PollingInterval < 100)
            {
                Console.WriteLine("Предупреждение: слишком частая проверка буфера обмена");
                PollingInterval = 100;
            }

            if (MaxTextLength < 1)
            {
                Console.WriteLine("Предупреждение: неверная длина текста");
                MaxTextLength = 1000;
            }

            return true;
        }
    }
}