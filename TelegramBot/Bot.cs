using Managers;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils;

namespace TelegramBot
{
    public class Bot
    {
        private readonly TgHost _host;
        private readonly TelegramDownloadService _telegramDownloadService;
        private readonly ILogger _logger;

        public Bot(
            TgHost host,
            TelegramDownloadService telegramDownloadService,
            ILogger logger)
        {
            // Dependencies
            _host = host;
            _telegramDownloadService = telegramDownloadService;
            _logger = logger;

            // Events
            _host.OnMessage += OnMessage;
            _host.OnCallback += OnCallback;
        }

        public async Task Init()
        {
            await _telegramDownloadService.Init(); // Init download service
            await _host.Init(); // Start the bot
        }

        // Delegates
        private async void OnMessage(ITelegramBotClient client, Update update)
        {
            if (update?.Message is not { } message) return;
            if (update?.Message.Chat.Id is not { } chatId) return;

            // User language
            var lang = message.From?.LanguageCode switch
            {
                "en" => ELanguage.En,
                "ru" => ELanguage.Ru,
                _ => ELanguage.En
            };

            // Delete user message
            await MessageService.Remove(client, message.Chat.Id, message, _logger);

            // Default commands
            if (await DispachCommands(client, chatId, message, lang)) return;

            // Handle logic
            await HandleFlowAsync(client, chatId, message, lang);
        }

        private async void OnCallback(ITelegramBotClient client, CallbackQuery cb)
        {
            if (cb?.Message is not { } message) return;
            if (message.Chat?.Id is not { } chatId) return;
            if (message.Caption is not { } caption) return;

            // User language
            var lang = message.From?.LanguageCode switch
            {
                "en" => ELanguage.En,
                "ru" => ELanguage.Ru,
                _ => ELanguage.En
            };

            // Answer for UI
            try
            {
                await client.AnswerCallbackQuery(cb.Id, "✨");
            }
            catch (Exception ex)
            {
                _logger.Log(ex.Message, ELogStatus.Warning);
            }

            // Parsing url 
            string key = "\nLINK: ";
            var prefix = caption.IndexOf(key);

            if (prefix >= 0)
            {
                var videoUrl = caption[(prefix + key.Length)..]; // URL from caption

                switch (cb?.Data)
                {
                    // Download video
                    case "action:video":

                        await _telegramDownloadService.DownloadSendVideoProcess(
                            client, 
                            chatId, 
                            videoUrl,
                            _host.Me.Username!,
                            lang);
                        break;

                    // Download audio
                    case "action:audio":
                        await _telegramDownloadService.DownloadSendAudioProcess(
                            client, 
                            chatId, 
                            videoUrl,
                            _host.Me.Username!,
                            lang);
                        break;


                    // Deleting info message
                    case "action:cancel":
                        await MessageService.Remove(client, chatId, cb.Message, _logger);
                        break;
                }
            }
        }


        // METHODS

        private async Task HandleFlowAsync(
            ITelegramBotClient client,
            ChatId chatId,
            Message message,
            ELanguage language)
        {
            // Checking message a link
            if (string.IsNullOrEmpty(message?.Text) || !message.Text.Contains("http") || !message.Text.Contains("https"))
            {
                await MessageService.Send(client, chatId, new Message
                { Text = ReplyReadService.GetReply("NotALink", language) }, _logger);

                return;
            }

            // Sending searching message
            var searchMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("Search", language) }, _logger);

            if (searchMessage is not null)
            {
                // Sending load menu
                var loadResult = await _telegramDownloadService.SendLoadingMenuProcess(
                    client, chatId, message.Text, language);

                // If problems
                if (loadResult == ELoadingStatus.NotValidLink)
                    // Bot answer to not valid link
                    await MessageService.Send(client, chatId, new Message
                    { Text = ReplyReadService.GetReply("NotValidLink", language) }, _logger);

                // Delete searching message
                await MessageService.Remove(client, chatId, searchMessage, _logger);
            }
        }

        private async Task<bool> DispachCommands(
            ITelegramBotClient client,
            ChatId chatId,
            Message message,
            ELanguage language)
        {
            if (message.Text is not string text) return false;

            if (text == "/start")
                await MessageService.Send(client, chatId, new Message
                { Text = ReplyReadService.GetReply("Greeting", language) }, _logger);
            else
                return false; // If is not a command

            return true; // If text is a command
        }
    };
}