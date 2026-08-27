namespace Utils
{
    public interface ILogger
    {
        public void Log(string text, ELogStatus status = ELogStatus.Text);
    }
}
