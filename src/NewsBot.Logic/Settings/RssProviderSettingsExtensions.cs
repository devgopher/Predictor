namespace NewsBot.Logic.Settings;

public static class RssProviderSettingsExtensions
{
    public static string ResolveUserAgent(this RssProviderSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.UserAgentProfile) &&
            settings.UserAgents.TryGetValue(settings.UserAgentProfile, out var profile))
        {
            return profile;
        }

        if (!string.IsNullOrWhiteSpace(settings.UserAgent))
            return settings.UserAgent;

        return settings.UserAgents.GetValueOrDefault("Chrome131")
               ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    }
}
