namespace Utils
{
    public enum EDownloadType : byte
    {
        Thumbnail,
        VideoBest,
        VideoMerged,
        Audio,
    }

    public enum ELoadingStatus : byte
    {
        Successfully,
        Error,
        BiggerThanLimit,
        NotValidLink,
    }

    public enum EMessageType : byte
    {
        Animation,
        Audio,
        Document,
        Message,
        Photo,
        Sticker,
        Video,
        Voice,
    }

    public enum ELogStatus : byte
    {
        Text,
        Warning,
        Error,
    }

    public enum ELanguage : byte
    {
        En,
        Ru, 
    }
}
