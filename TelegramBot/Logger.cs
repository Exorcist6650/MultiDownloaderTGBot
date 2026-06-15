using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot
{
    public enum LogStatus : byte
    {
        Message, 
        Warning,
        Error,
    }

    public class ConsoleLogger
    {
        static public void Log(string message, LogStatus status = LogStatus.Message)
        {
            Console.WriteLine($"{DateTime.UtcNow} | {message} | {status}");
        }
    }

    public class TelegramLogger
    {
        /// <summary>
        /// Log in telegram chat
        /// </summary>
        /// <param name="message"></param>
        /// <param name="client"></param>
        /// <param name="chatId"></param>
        /// <param name="status"></param>
        /// <returns>
        /// Return a bot message reference
        /// </returns>
        public async Task<Message?> Send(string message, ITelegramBotClient client, ChatId chatId, LogStatus status = LogStatus.Message)
        {
            try
            {
                var bot_message = await client.SendMessage(chatId, status == LogStatus.Message ? message
                    : $"{DateTime.UtcNow} | {message} | {status}");
                return bot_message;
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                Console.WriteLine($"Exception: {ex.Message}", LogStatus.Error);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}", LogStatus.Error);
                return null;
            }

        }

        public async Task Delete(Message? message, ITelegramBotClient client, ChatId chatId)
        {
            // Deleting message for user
            try
            {
                if (message != null)
                    await client.DeleteMessage(chatId, message.Id);
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                ConsoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
            }
            catch (Exception ex)
            {
                ConsoleLogger.Log($"Excertion: {ex.Message}", LogStatus.Error);
            }
        }
    }
}
