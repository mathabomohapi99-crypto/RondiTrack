namespace RondiTrack.Domain;

public abstract class DomainException(string message) : Exception(message);

public sealed class DomainValidationException(string message) : DomainException(message);

public sealed class DomainConflictException(string message) : DomainException(message);