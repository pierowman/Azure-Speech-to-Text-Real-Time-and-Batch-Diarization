namespace speechtotext.Exceptions
{
    public class TranscriptionException : Exception
    {
        public TranscriptionException(string message) : base(message)
        {
        }

        public TranscriptionException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
