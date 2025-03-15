namespace DevHabit.Api.Settings;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption:Key";
    public required string Key { get; init; }
}
