using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TelegramBot
{
    static class JSONReader
    {
        private const string PATH_TO_JSON = "resources/LanguageMessages.json";
        private static readonly string _json = File.ReadAllText(PATH_TO_JSON);
        public static string Language { get; set; } = "RU";

        public static string? getValue(string key)
        {
            using var document = JsonDocument.Parse(_json);
            var root = document.RootElement;
            
            if (root.TryGetProperty(Language, out var lang))
            {
                if (lang.TryGetProperty(key, out var elem))
                {
                    string value = elem.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                    else
                        return null;
            }
            }

            return null;
        }
    }
}
