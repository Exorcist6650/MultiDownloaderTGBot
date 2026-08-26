using System.Text.Json;

namespace Utils
{
    public static class ReplyReadService
    {
        private const string PATH_TO_JSON = "resources/LanguageMessages.json";
        private static readonly string _jsonText = File.ReadAllText(PATH_TO_JSON);
        public static string Language { get; set; } = "ENG";

        public static string GetReply(string key, ELanguage language)
        {
            using var document = JsonDocument.Parse(_jsonText);
            var root = document.RootElement;

            string langCode = language switch
            {
                ELanguage.En => "en",
                ELanguage.Ru => "ru",
                _ => throw new ArgumentException("Unknown language", nameof(language))
            };

            // Get all replies by language
            if (!root.TryGetProperty(langCode, out var lang)) 
                throw new NullReferenceException("Language not found in file");

            // Get reply
            if (!lang.TryGetProperty(key, out var reply)) 
                throw new NullReferenceException("Reply not found");

            return reply.ToString();
        }
    }
}
