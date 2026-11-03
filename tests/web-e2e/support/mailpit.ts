import { expect, type APIRequestContext } from '@playwright/test';

// Mailpit's HTTP API, as the Compose stack publishes it
export const mailpitUrl = 'http://localhost:8025';

export interface MailpitMessage {
  ID: string;
  Subject: string;
  Text: string;
  HTML: string;
}

interface SearchResult {
  messages: { ID: string }[];
}

/**
 * Reads the mail Mailpit received. Read only by recipient: the workers run in parallel against the one Mailpit, each
 * test with its own addresses, and the Mailpit volume keeps mail from earlier runs.
 */
export class Mailpit {
  private readonly api: APIRequestContext;

  constructor(api: APIRequestContext) {
    this.api = api;
  }

  /** The one message to `recipient`. Waits a few seconds for it, since the API sends some mail after it answers. */
  async messageTo(recipient: string): Promise<MailpitMessage> {
    const search = `api/v1/search?query=${encodeURIComponent(`to:"${recipient}"`)}`;
    let found: SearchResult['messages'] = [];
    await expect
      .poll(
        async () => {
          // Not get(): the poll retries only a failed matcher, and a throw here would end it at Mailpit's first hiccup
          const response = await this.api.get(search).catch(() => undefined);
          if (!response?.ok()) {
            return 0;
          }

          found = ((await response.json()) as SearchResult).messages;
          return found.length;
        },
        { message: `mail to ${recipient}`, timeout: 10_000 },
      )
      .toBeGreaterThan(0);

    expect(found, `exactly one message to ${recipient}`).toHaveLength(1);
    return this.get<MailpitMessage>(`api/v1/message/${found[0].ID}`);
  }

  private async get<T>(path: string): Promise<T> {
    const response = await this.api.get(path);
    expect(response.ok(), `GET ${path} → ${response.status()}`).toBe(true);
    return (await response.json()) as T;
  }
}

/** The one reset link in an email body, checked the way the integration tests' ResetLink checks it. */
export function resetLinkIn(body: string): string {
  const links = [...body.matchAll(/https?:\/\/\S+\/reset-password\S*/g)].map((match) => match[0]);
  expect(links, 'exactly one reset link').toHaveLength(1);

  // The token travels in the fragment, which browsers never send, not in the query, which reaches servers' logs (A11)
  const link = new URL(links[0]);
  expect(link.pathname).toBe('/reset-password');
  expect(link.search).toBe('');
  expect(link.hash).toMatch(/^#token=./);
  return link.href;
}
