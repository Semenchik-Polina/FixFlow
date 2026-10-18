import { http as mock, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { server } from '../test/server';
import { ApiError } from './ApiError';
import { http } from './http';
import { redirectToLogin } from './navigation';

vi.mock('./navigation', () => ({ redirectToLogin: vi.fn() }));

// A dummy API path: MSW answers it, no backend serves it. Each test sets the browser's page URL first, because
// that, and nothing else, decides the organization header.
const PROBE = '/api/v1/probe';

function openPage(path: string) {
  window.history.replaceState(null, '', path);
}

function answerWithOrganizationHeader() {
  server.use(
    mock.get(PROBE, ({ request }) => HttpResponse.json({ organization: request.headers.get('X-Organization') })),
  );
}

function answer(status: number, body: string, contentType: string) {
  server.use(mock.get(PROBE, () => new HttpResponse(body, { status, headers: { 'Content-Type': contentType } })));
}

async function rejectionOf(promise: Promise<unknown>): Promise<ApiError> {
  const error = await promise.then(
    () => {
      throw new Error('Expected the call to fail.');
    },
    (reason: unknown) => reason,
  );
  expect(error).toBeInstanceOf(ApiError);
  return error as ApiError;
}

describe('http: organization header', () => {
  it('sends the org slug from the page URL', async () => {
    openPage('/acme/requests');
    answerWithOrganizationHeader();

    await expect(http(PROBE)).resolves.toEqual({ organization: 'acme' });
  });

  it('sends no org header on a global page', async () => {
    openPage('/login');
    answerWithOrganizationHeader();

    await expect(http(PROBE)).resolves.toEqual({ organization: null });
  });

  it('replaces an org header passed by the caller with the page slug', async () => {
    openPage('/acme/requests');
    answerWithOrganizationHeader();

    await expect(http(PROBE, { headers: { 'x-organization': 'evil' } })).resolves.toEqual({ organization: 'acme' });
  });

  it('drops an org header passed by the caller on a global page', async () => {
    openPage('/login');
    answerWithOrganizationHeader();

    await expect(http(PROBE, { headers: { 'X-Organization': 'evil' } })).resolves.toEqual({ organization: null });
  });
});

describe('http: requests and responses', () => {
  it('sends the session cookie', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch');
    answer(200, '{}', 'application/json');

    await http(PROBE);

    expect(fetchSpy).toHaveBeenCalledWith(expect.any(URL), expect.objectContaining({ credentials: 'include' }));
  });

  it('returns the JSON body', async () => {
    answer(200, '{"id":"0199b1c2-0000-7000-8000-000000000001"}', 'application/json');

    await expect(http(PROBE)).resolves.toEqual({ id: '0199b1c2-0000-7000-8000-000000000001' });
  });

  it('returns undefined for 204 No Content', async () => {
    server.use(mock.get(PROBE, () => new HttpResponse(null, { status: 204 })));

    await expect(http(PROBE)).resolves.toBeUndefined();
  });
});

describe('http: errors', () => {
  it('turns validation Problem Details into an ApiError with field errors', async () => {
    answer(
      400,
      JSON.stringify({
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { email: ['Email is required.'], 'address.street': ['Street is too long.'] },
        traceId: '00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01',
      }),
      'application/problem+json',
    );

    const error = await rejectionOf(http(PROBE));

    expect(error).toMatchObject({
      status: 400,
      title: 'One or more validation errors occurred.',
      traceId: '00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01',
      fieldErrors: { email: ['Email is required.'], 'address.street': ['Street is too long.'] },
    });
  });

  it('keeps the detail of a business rule violation', async () => {
    answer(
      422,
      JSON.stringify({ status: 422, title: 'Unprocessable Entity', detail: 'The work order is already closed.' }),
      'application/problem+json',
    );

    const error = await rejectionOf(http(PROBE));

    expect(error.status).toBe(422);
    expect(error.detail).toBe('The work order is already closed.');
    expect(error.message).toBe('The work order is already closed.');
    expect(error.fieldErrors).toEqual({});
  });

  it('keeps the status of an error response that is not Problem Details', async () => {
    answer(502, '<html><body>Bad Gateway</body></html>', 'text/html');

    const error = await rejectionOf(http(PROBE));

    expect(error.status).toBe(502);
    expect(error.title).toBeUndefined();
    expect(error.fieldErrors).toEqual({});
  });

  it('sends the browser to the login page on 401', async () => {
    openPage('/acme/requests');
    answer(401, JSON.stringify({ status: 401, title: 'Unauthorized' }), 'application/problem+json');

    const error = await rejectionOf(http(PROBE));

    expect(error.status).toBe(401);
    expect(redirectToLogin).toHaveBeenCalledOnce();
  });

  it('stays on the login page on 401 there', async () => {
    openPage('/login');
    answer(401, JSON.stringify({ status: 401, title: 'Unauthorized' }), 'application/problem+json');

    const error = await rejectionOf(http(PROBE));

    expect(error.status).toBe(401);
    expect(redirectToLogin).not.toHaveBeenCalled();
  });

  it('does not leave the page on 403', async () => {
    openPage('/acme/requests');
    answer(403, JSON.stringify({ status: 403, title: 'Forbidden' }), 'application/problem+json');

    await rejectionOf(http(PROBE));

    expect(redirectToLogin).not.toHaveBeenCalled();
  });
});
