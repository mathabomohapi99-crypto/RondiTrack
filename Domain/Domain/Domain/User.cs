using System.Net.Mail;

namespace RondiTrack.Domain;

public sealed class User
{
    public const int MaxNameLength = 100;

    public Guid Id { get; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public DateTime CreatedAtUtc { get; }

    public User(string fullName, string email)
    {
        Id = Guid.NewGuid();
        CreatedAtUtc = DateTime.UtcNow;
        FullName = ValidateName(fullName);
        Email = ValidateEmail(email);
    }

    internal User(Guid id, string fullName, string email) : this(fullName, email)
    {
        Id = id;
    }

    public void UpdateDetails(string fullName, string email)
    {
        var validName = ValidateName(fullName);
        var validEmail = ValidateEmail(email);
        FullName = validName;
        Email = validEmail;
    }

    private static string ValidateName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainValidationException("Full name is required.");

        var trimmed = fullName.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new DomainValidationException($"Full name cannot exceed {MaxNameLength} characters.");

        return trimmed;
    }

    private static string ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainValidationException("Email is required.");

        var trimmed = email.Trim();
        if (!MailAddress.TryCreate(trimmed, out var parsed)
            || parsed.Address != trimmed
            || !parsed.Host.Contains('.'))
            throw new DomainValidationException("Email address is not valid.");

        return trimmed.ToLowerInvariant();
    }
}