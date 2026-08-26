namespace Utils
{
    public class ConsoleLogger : ILogger
    {
        public void Log(string text, ELogStatus status = ELogStatus.Text)
        {
            switch (status)
            {
                // text standard
                case ELogStatus.Text:
                    Console.WriteLine($"{text} | {DateTime.UtcNow}");
                    break;

                // Warning yellow color
                case ELogStatus.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{text} | {DateTime.UtcNow}");
                    break;

                // Error red color
                case ELogStatus.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{text} | {DateTime.UtcNow}");
                    break;
            }

            Console.ResetColor(); // Reset console color
        }
    }
}
