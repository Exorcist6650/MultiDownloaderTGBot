using TelegramBot;
using Managers;
using Utils;

namespace MyApp
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

            var bot = new Bot
            (
                host, 
                new DownloadManager(logger),
                logger
            );

            await bot.Init(); // Bot recieving start


            Console.ReadLine();
        }
        
    }
}