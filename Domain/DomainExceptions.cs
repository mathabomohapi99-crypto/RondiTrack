namespace RondiTrack.Domain;

public abstract class DomainException(string message) : Exception(message);

public sealed class DomainValidationException(string message) : DomainException(message);

public sealed class DomainConflictException(string message) : DomainException(message);

public sealed class DomainNotFoundException(string message) : DomainException(message);

public sealed class DomainReferenceException(string message) : DomainException(message);

// NEW 9.1: an error with a short code the frontend can switch on, e.g. "invite_expired" or "stale_preview"
public sealed class CodedDomainException(int status, string code, string message) : DomainException(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}