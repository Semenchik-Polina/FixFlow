import { useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http as mock, HttpResponse } from 'msw';
import { useEffect } from 'react';
import { createBrowserRouter, useParams } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { http } from '../../api/http';
import { aMembership, givenSignedIn } from '../../test/fixtures';
import { renderWithProviders } from '../../test/renderWithProviders';
import { server } from '../../test/server';
import { OrganizationLayout } from './OrganizationLayout';

// An organization's data as a dummy endpoint returns it: the X-Organization header it got, and its request number
interface Probe {
  org: string | null;
  n: number;
}

// The X-Organization header of each probe request, in order
let requests: (string | null)[];
// What the probe page showed in each organization, in order, and the query client each visit had
let shown: string[];
let visits: { slug: string; client: QueryClient }[];
// Holds the answers to one organization's requests until released
let held: { org: string; until: Promise<void> } | null;
let activeRouter: ReturnType<typeof createBrowserRouter> | undefined;

function ProbePage() {
  const { orgSlug = '' } = useParams();
  const client = useQueryClient();
  const { data } = useQuery({
    queryKey: ['probe'],
    queryFn: ({ signal }) => http<Probe>('/api/v1/probe', { signal }),
  });
  const text = data === undefined ? 'loading' : `${data.org} #${data.n}`;

  useEffect(() => {
    visits.push({ slug: orgSlug, client });
  }, [orgSlug, client]);
  useEffect(() => {
    shown.push(`${orgSlug}: ${text}`);
  }, [orgSlug, text]);

  return <p>{text}</p>;
}

// A browser router, so the URL that http.ts reads the slug from follows navigation, as in the app
function renderAt(path: string) {
  window.history.replaceState(null, '', path);
  const router = createBrowserRouter([
    { path: ':orgSlug', Component: OrganizationLayout, children: [{ index: true, Component: ProbePage }] },
  ]);
  activeRouter = router;
  renderWithProviders(<RouterProvider router={router} />);
  return router;
}

function clientOf(slug: string): QueryClient {
  const visit = visits.find((v) => v.slug === slug);
  if (visit === undefined) {
    throw new Error(`No visit to ${slug}.`);
  }

  return visit.client;
}

beforeEach(() => {
  requests = [];
  shown = [];
  visits = [];
  held = null;
  givenSignedIn(aMembership('acme'), aMembership('brightway'));
  server.use(
    mock.get('*/api/v1/probe', async ({ request }) => {
      const org = request.headers.get('X-Organization');
      requests.push(org);
      const probe: Probe = { org, n: requests.length };
      if (held !== null && held.org === org) {
        await held.until;
      }

      return HttpResponse.json(probe);
    }),
  );
});

afterEach(() => {
  activeRouter?.dispose();
  activeRouter = undefined;
});

describe('OrganizationQueryScope', () => {
  it("starts each organization with an empty cache, so one organization's data never shows in another", async () => {
    const user = userEvent.setup();
    const router = renderAt('/acme');
    expect(await screen.findByText('acme #1')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Acme, switch organization' }));
    await user.click(screen.getByRole('menuitem', { name: /Brightway/ }));
    expect(await screen.findByText('brightway #2')).toBeInTheDocument();

    await act(() => router.navigate('/acme'));
    expect(await screen.findByText('acme #3')).toBeInTheDocument();

    expect(requests).toEqual(['acme', 'brightway', 'acme']);
    expect(shown).toEqual([
      'acme: loading',
      'acme: acme #1',
      'brightway: loading',
      'brightway: brightway #2',
      // Back in acme, nothing of the first visit is left
      'acme: loading',
      'acme: acme #3',
    ]);
  });

  it('drops the answer to a request of the old organization that is still in flight at the switch', async () => {
    const router = renderAt('/acme');
    await screen.findByText('acme #1');
    let release = () => {};
    const until = new Promise<void>((resolve) => (release = resolve));
    held = { org: 'acme', until };
    const acme = clientOf('acme');
    void acme.refetchQueries();
    await waitFor(() => expect(requests).toEqual(['acme', 'acme']));

    await act(() => router.navigate('/brightway'));
    expect(await screen.findByText('brightway #3')).toBeInTheDocument();
    await act(async () => {
      release();
      await until;
      await new Promise((resolve) => setTimeout(resolve, 0));
    });

    // The query hands its abort signal to the request, so unmounting acme's page aborted it: nothing landed, not even
    // in acme's discarded client
    expect(acme.getQueryData(['probe'])).toEqual({ org: 'acme', n: 1 });
    expect(clientOf('brightway').getQueryData(['probe'])).toEqual({ org: 'brightway', n: 3 });
    expect(screen.getByText('brightway #3')).toBeInTheDocument();
    expect(shown.filter((text) => text.startsWith('brightway:'))).toEqual([
      'brightway: loading',
      'brightway: brightway #3',
    ]);
  });

  it("drops the answer to an old organization's refetch that goes out after the URL changed", async () => {
    const router = renderAt('/acme');
    await screen.findByText('acme #1');
    await act(() => router.navigate('/brightway'));
    await screen.findByText('brightway #2');

    // Like a refetch on focus that fires between the URL change and the old page's unmount
    const acme = clientOf('acme');
    await act(() => acme.refetchQueries({ type: 'all' }));

    // http.ts sent the URL's slug, so the answer is brightway's, and it stays in acme's discarded client
    expect(requests).toEqual(['acme', 'brightway', 'brightway']);
    expect(acme.getQueryData(['probe'])).toEqual({ org: 'brightway', n: 3 });
    expect(clientOf('brightway').getQueryData(['probe'])).toEqual({ org: 'brightway', n: 2 });
    expect(screen.getByText('brightway #2')).toBeInTheDocument();
  });
});
