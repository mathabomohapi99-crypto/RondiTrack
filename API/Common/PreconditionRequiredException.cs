namespace RondiTrack.Common;

// NEW: thrown when an update arrives without the If-Match header. Becomes a 428 problem response.
public sealed class PreconditionRequiredException(string message) : Exception(message);