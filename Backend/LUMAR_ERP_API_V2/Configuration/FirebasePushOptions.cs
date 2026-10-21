namespace LUMAR_ERP_API_V2.Configuration;

public sealed class FirebasePushOptions
{
    public const string SectionName = "FirebasePush";

    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountJsonPath { get; set; } = string.Empty;
    public string OAuthTokenUrl { get; set; } = "https://oauth2.googleapis.com/token";
    public string FcmSendUrl { get; set; } = "https://fcm.googleapis.com/v1";
    public int TimeoutSeconds { get; set; } = 30;
}
