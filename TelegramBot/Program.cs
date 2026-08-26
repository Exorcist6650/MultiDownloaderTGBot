using Managers;
using Services;
using Utils;

namespace TelegramBot
{
    internal class Program
    {
        
        static async Task Main(string[] args)
        {
            // Get bot token from environment
            if (Environment.GetEnvironmentVariable("BOT_TOKEN", EnvironmentVariableTarget.User) is not { } token)
                throw new InvalidOperationException("Environment variable \"BOT_TOKEN\" not found");

            var host = new TgHost(token, new ConsoleLogger());
            var logger = new ConsoleLogger();
            var downloadManager = new DownloadManager(logger);
            var telegramDownloadService = new TelegramDownloadService( 
                downloadManager, logger);

            var bot = new Bot
            (
                host,
                telegramDownloadService,
                logger
            );

            await bot.Init(); // Start bot


            Console.ReadLine();
        }
    }
}