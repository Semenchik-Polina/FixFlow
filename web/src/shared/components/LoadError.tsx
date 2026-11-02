import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import { ApiError } from '../../api/ApiError';
import { strings } from '../strings';

interface LoadErrorProps {
  error: unknown;
  onRetry: () => unknown;
}

/** Data a page needs didn't load: why, in a sentence, and a way to try again. */
export function LoadError({ error, onRetry }: LoadErrorProps) {
  // The session ended: http.ts is already loading the login page
  if (error instanceof ApiError && error.status === 401) {
    return null;
  }

  return (
    <Alert
      severity="error"
      action={
        <Button color="inherit" size="small" onClick={() => void onRetry()}>
          {strings.common.retry}
        </Button>
      }
    >
      {error instanceof ApiError ? strings.errors.serverError : strings.errors.unreachable}
    </Alert>
  );
}
