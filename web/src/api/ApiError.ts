/** Validation messages by field, keyed by the camelCase JSON path (`email`, `address.street`, `tags[0]`). */
export type FieldErrors = Readonly<Record<string, readonly string[]>>;

interface Problem {
  type?: string | undefined;
  title?: string | undefined;
  detail?: string | undefined;
  traceId?: string | undefined;
  fieldErrors?: FieldErrors | undefined;
}

/**
 * An error response from the API. The API answers errors with RFC 9457 Problem Details (ARCHITECTURE §15); when the
 * body isn't one (e.g. a proxy error page), only the status is known.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly type: string | undefined;
  readonly title: string | undefined;
  readonly detail: string | undefined;
  readonly traceId: string | undefined;
  /** Field-level validation errors, ready for React Hook Form's `setError`; empty when there are none. */
  readonly fieldErrors: FieldErrors;

  constructor(status: number, problem: Problem = {}) {
    super(problem.detail ?? problem.title ?? `The API answered ${status}.`);
    this.name = 'ApiError';
    this.status = status;
    this.type = problem.type;
    this.title = problem.title;
    this.detail = problem.detail;
    this.traceId = problem.traceId;
    this.fieldErrors = problem.fieldErrors ?? {};
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    return new ApiError(response.status, await readProblem(response));
  }
}

async function readProblem(response: Response): Promise<Problem> {
  // application/problem+json, or plain application/json
  if (!/[/+]json\b/.test(response.headers.get('Content-Type') ?? '')) {
    return {};
  }

  let body: unknown;
  try {
    body = await response.json();
  } catch {
    return {};
  }

  if (!isRecord(body)) {
    return {};
  }

  return {
    type: stringOrUndefined(body['type']),
    title: stringOrUndefined(body['title']),
    detail: stringOrUndefined(body['detail']),
    traceId: stringOrUndefined(body['traceId']),
    fieldErrors: fieldErrorsOf(body['errors']),
  };
}

function fieldErrorsOf(value: unknown): FieldErrors {
  if (!isRecord(value)) {
    return {};
  }

  const fieldErrors: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      fieldErrors[field] = messages.filter((message): message is string => typeof message === 'string');
    }
  }

  return fieldErrors;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function stringOrUndefined(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}
