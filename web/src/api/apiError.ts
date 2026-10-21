// The one error type the whole app uses for failed API calls.
// problem = the problem+json body (read defensively, no hand-written API types).
export class ApiError extends Error {
  readonly status: number;
  readonly problem: Record<string, unknown> | null;
  readonly retryAfter: string | null; // raw Retry-After header text

  constructor(
    status: number,
    problem: Record<string, unknown> | null,
    retryAfter: string | null,
  ) {
    super(
      typeof problem?.title === 'string' ? problem.title : `Request failed with ${status}`,
    );
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
    this.retryAfter = retryAfter;
  }
}

// Turns a failed Response into an ApiError (body may be empty, so we guard it)
export async function apiErrorFromResponse(res: Response): Promise<ApiError> {
  let problem: Record<string, unknown> | null = null;
  try {
    const body: unknown = await res.json();
    if (body && typeof body === 'object') problem = body as Record<string, unknown>;
  } catch {
    // no JSON body, that is fine
  }
  return new ApiError(res.status, problem, res.headers.get('Retry-After'));
}