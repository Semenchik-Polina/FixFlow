import type { FieldPath, FieldValues, UseFormSetError } from 'react-hook-form';
import { ApiError } from '../../api/ApiError';
import { strings } from '../strings';

/**
 * Shows why a submit failed: each field error from the API on its field, focusing the first in form order, and every
 * other reason as the form's `root` error, which the form shows above its fields.
 * @param fields The form's fields by the API's field name, in form order: `{ name: 'organizationName' }`.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: Readonly<Record<string, FieldPath<T>>>,
): void {
  const setRootError = (message: string) => setError('root', { type: 'server', message });

  // fetch rejects with a TypeError when the request never got an answer
  if (!(error instanceof ApiError)) {
    setRootError(strings.errors.unreachable);
    return;
  }

  if (error.status === 429) {
    setRootError(strings.errors.tooManyAttempts);
    return;
  }

  if (error.status >= 500) {
    setRootError(strings.errors.serverError);
    return;
  }

  let focused = false;
  for (const [apiField, field] of Object.entries(fields)) {
    const message = error.fieldErrors[apiField]?.[0];
    if (message !== undefined) {
      setError(field, { type: 'server', message }, { shouldFocus: !focused });
      focused = true;
    }
  }

  const others = Object.entries(error.fieldErrors)
    .filter(([apiField]) => !(apiField in fields))
    .flatMap(([, messages]) => messages.slice(0, 1));
  if (others.length > 0) {
    setRootError(others.join(' '));
  } else if (!focused) {
    setRootError(error.detail ?? strings.errors.serverError);
  }
}
