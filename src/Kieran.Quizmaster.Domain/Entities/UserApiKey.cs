namespace Kieran.Quizmaster.Domain.Entities;

/// <summary>
/// One row per (user, provider). Holds the user-supplied AI provider API
/// key encrypted at rest with ASP.NET Core Data Protection. Persistence
/// always sees ciphertext; only <c>UserApiKeyService</c> handles the
/// plaintext at the application boundary.
/// </summary>
public class UserApiKey
{
    public Guid   Id            { get; set; }
    public Guid   UserId        { get; set; }
    public User   User          { get; set; } = null!;

    /// <summary>Matches <c>AiProviderKind.Name</c> (e.g. "OpenAI", "Anthropic").</summary>
    public string Provider      { get; set; } = string.Empty;

    /// <summary>DataProtection-encrypted blob of the user's API key.</summary>
    public string EncryptedKey  { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
