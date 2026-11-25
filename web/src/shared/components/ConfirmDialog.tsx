import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import { useState, type ReactNode } from 'react';
import { ApiError } from '../../api/ApiError';
import { strings } from '../strings';

interface ConfirmDialogProps {
  title: string;
  message: ReactNode;
  /** The action's name, as on the button that opened the dialog: "Archive property". */
  confirmLabel: string;
  /** Runs the action. The dialog stays open, showing why, when it rejects; on success the caller closes it. */
  onConfirm: () => Promise<unknown>;
  onClose: () => void;
}

/**
 * Asks before an action that can't be undone. Open while mounted: callers render it only when it should show, so it
 * starts without an error each time.
 */
export function ConfirmDialog({ title, message, confirmLabel, onConfirm, onClose }: ConfirmDialogProps) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const confirm = async () => {
    setPending(true);
    setError(null);
    try {
      await onConfirm();
    } catch (reason) {
      setError(errorMessage(reason));
    } finally {
      setPending(false);
    }
  };

  return (
    <Dialog open onClose={pending ? undefined : onClose} fullWidth maxWidth="xs">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent sx={{ display: 'grid', gap: 2 }}>
        {error !== null && <Alert severity="error">{error}</Alert>}
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={pending}>
          {strings.common.cancel}
        </Button>
        <Button variant="contained" color="error" onClick={() => void confirm()} disabled={pending}>
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/** Why the action failed, as `applyServerErrors` words a form's: the API's own explanation when it gave one. */
function errorMessage(reason: unknown): string {
  if (!(reason instanceof ApiError)) {
    return strings.errors.unreachable;
  }

  return reason.status >= 500 ? strings.errors.serverError : (reason.detail ?? strings.errors.serverError);
}
