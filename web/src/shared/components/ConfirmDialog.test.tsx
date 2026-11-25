import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/ApiError';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ConfirmDialog } from './ConfirmDialog';

function renderDialog(onConfirm: () => Promise<unknown>) {
  const onClose = vi.fn();
  renderWithProviders(
    <ConfirmDialog
      title="Archive Riverside Apartments?"
      message="Archived properties leave every list."
      confirmLabel="Archive property"
      onConfirm={onConfirm}
      onClose={onClose}
    />,
  );
  return { onClose };
}

describe('ConfirmDialog', () => {
  it('runs the action, with both buttons disabled until it ends', async () => {
    const user = userEvent.setup();
    let finish = () => {};
    const onConfirm = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)));
    renderDialog(onConfirm);

    expect(screen.getByRole('dialog', { name: 'Archive Riverside Apartments?' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Archive property' }));

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: 'Archive property' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    finish();
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Cancel' })).toBeEnabled());
  });

  it.each([
    [
      "the API's explanation",
      new ApiError(422, { detail: 'This property is already archived.' }),
      'This property is already archived.',
    ],
    ['a server error', new ApiError(500), 'Something went wrong on our side. Try again in a moment.'],
    ['no answer', new TypeError('Failed to fetch'), "FixFlow can't be reached. Check your connection, then try again."],
  ])('stays open on failure, showing %s', async (_, error, message) => {
    const user = userEvent.setup();
    const { onClose } = renderDialog(() => Promise.reject(error));

    await user.click(screen.getByRole('button', { name: 'Archive property' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(screen.getByRole('button', { name: 'Archive property' })).toBeEnabled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('closes on Cancel without running the action', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn(() => Promise.resolve());
    const { onClose } = renderDialog(onConfirm);

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
