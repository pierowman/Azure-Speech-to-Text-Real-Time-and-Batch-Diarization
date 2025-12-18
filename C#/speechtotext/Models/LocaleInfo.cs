namespace speechtotext.Models
{
    /// <summary>
    /// Represents locale information from Azure Speech Service
    /// </summary>
    public class LocaleInfo
    {
        /// <summary>
        /// Locale code (e.g., "en-US", "es-ES")
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Display name from Azure (e.g., "English (United States)")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Formatted display name (same as Name, no emojis)
        /// </summary>
        public string FormattedName => Name;
    }
}
