import { HttpErrorResponse } from '@angular/common/http';

/**
 * The API returns RFC 9457 problem+json for every failure, including model validation
 * (where ASP.NET puts the per-field messages under `errors`). This turns any of those
 * shapes into one sentence a pharmacist can act on, rather than "Http failure response".
 */
export function describeApiError(error: unknown, fallback = 'Something went wrong.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  if (error.status === 0) {
    return 'Cannot reach the API. Is it running on the address in proxy.conf.json?';
  }

  const problem = error.error as
    | { title?: string; detail?: string; errors?: Record<string, string[]> }
    | string
    | null;

  if (typeof problem === 'string' && problem.trim().length > 0) {
    return problem;
  }

  if (problem && typeof problem === 'object') {
    const fieldMessages = Object.values(problem.errors ?? {}).flat();
    if (fieldMessages.length > 0) {
      return fieldMessages.join(' ');
    }

    const parts = [problem.title, problem.detail].filter(
      (part): part is string => typeof part === 'string' && part.length > 0,
    );

    if (parts.length > 0) {
      return parts.join(' ');
    }
  }

  return `${fallback} (HTTP ${error.status})`;
}
