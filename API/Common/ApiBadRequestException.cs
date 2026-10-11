namespace RondiTrack.Api.Common.Paging;

// NEW: our own exception for "the client sent something invalid".
// The central error handler turns it into a 400 problem response.
public class ApiBadRequestException : Exception
{
    public ApiBadRequestException(string message) : base(message) { }
}