namespace DevHabit.Api.Settings;

public sealed class GitHubAutomationOptions
{
    public const string SectionName = "GitHubAutomation";

    public required int ScanIntervalInMinutes { get; init; }
}
