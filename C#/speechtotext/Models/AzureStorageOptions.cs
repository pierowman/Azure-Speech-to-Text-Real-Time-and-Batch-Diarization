namespace speechtotext.Models
{
    /// <summary>
    /// Configuration options for Azure Blob Storage used in batch transcription
    /// Uses Azure AD authentication instead of connection strings for enhanced security
    /// </summary>
    public class AzureStorageOptions
    {
        /// <summary>
        /// Azure Storage Account Name (e.g., "mystorageaccount")
        /// </summary>
        public string StorageAccountName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the blob container to store audio files
        /// Default: audio-uploads
        /// </summary>
        public string ContainerName { get; set; } = "audio-uploads";

        /// <summary>
        /// Azure AD Tenant ID for authentication
        /// Required for Service Principal authentication
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Azure AD Client ID (Application ID) from App Registration
        /// Required for Service Principal authentication
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Azure AD Client Secret from App Registration
        /// Required for Service Principal authentication
        /// Leave empty to use DefaultAzureCredential (Managed Identity, Azure CLI, etc.)
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Enable or disable blob storage functionality
        /// Set to false to use placeholder mode (for testing without Azure Storage)
        /// Default: false
        /// </summary>
        public bool EnableBlobStorage { get; set; } = false;

        /// <summary>
        /// Use Managed Identity for authentication (recommended for Azure-hosted apps)
        /// When true, ClientId, ClientSecret, and TenantId are optional
        /// Default: false
        /// </summary>
        public bool UseManagedIdentity { get; set; } = false;

        /// <summary>
        /// Check if blob storage is properly configured and enabled
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                if (!EnableBlobStorage || string.IsNullOrWhiteSpace(StorageAccountName))
                    return false;

                // If using Managed Identity, no other credentials needed
                if (UseManagedIdentity)
                    return true;

                // Otherwise, require Service Principal credentials
                return !string.IsNullOrWhiteSpace(TenantId) &&
                       !string.IsNullOrWhiteSpace(ClientId) &&
                       !string.IsNullOrWhiteSpace(ClientSecret);
            }
        }

        /// <summary>
        /// Get the blob service endpoint URL
        /// </summary>
        public string BlobServiceEndpoint => $"https://{StorageAccountName}.blob.core.windows.net";
    }
}
