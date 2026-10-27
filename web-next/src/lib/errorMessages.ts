// Friendly messages for the person. If your 7.3 file has different wording, paste that wording here.
export function messageFor(status: number): string {
  switch (status) {
    case 400:
      return 'Something in that request was not right. Please check it and try again.';
    case 401:
      return 'Your session has ended. Please sign in again.';
    case 403:
      return 'You are not allowed to do that.';
    case 404:
      return "We couldn't find that.";
    case 429:
      return 'Too many attempts. Please wait a bit and try again.';
    default:
      return 'Something went wrong on our side. Please try again.';
  }
}

// Message for a failed sign-in (a 401 here means wrong email or password, not an ended session).
export function loginMessageFor(status: number): string {
  return status === 401 ? 'Your email or password is wrong.' : messageFor(status);
}

// Adds the reference number so support can find the request in the API log.
export function withReference(message: string, correlationId?: string): string {
  return correlationId ? `${message} (Reference: ${correlationId})` : message;
}