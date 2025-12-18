namespace speechtotext.Exceptions
{
    public class InvalidAudioFileException : Exception
    {
        public InvalidAudioFileException(string message) : base(message)
        {
        }

        public InvalidAudioFileException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
