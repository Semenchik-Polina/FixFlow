import { zodResolver } from '@hookform/resolvers/zod';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import { useForm } from 'react-hook-form';
import { Link as RouterLink, useLocation, useNavigate } from 'react-router';
import { z } from 'zod/mini';
import { ApiError } from '../../../api/ApiError';
import { useResetPassword } from '../../../api/generated';
import { GlobalPageLayout } from '../../../shared/components/GlobalPageLayout';
import { applyServerErrors } from '../../../shared/forms/applyServerErrors';
import { PasswordField } from '../../../shared/forms/PasswordField';
import { rules } from '../../../shared/forms/rules';
import { globalPaths } from '../../../shared/routing/paths';
import { authStrings } from '../strings';
import type { LoginPageState } from './LoginPage';

const schema = z.object({ newPassword: rules.newPassword, confirmPassword: z.string() }).check(
  z.refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    error: authStrings.resetPassword.mismatch,
  }),
);

// The root error that only a new link can fix
const INVALID_LINK = 'invalidLink';

/**
 * Where the reset email's link leads. The code travels in the fragment (`#token=...`), which never reaches a server
 * log (A11).
 */
export function ResetPasswordPage() {
  const navigate = useNavigate();
  const token = new URLSearchParams(useLocation().hash.slice(1)).get('token');
  const resetPassword = useResetPassword();
  const {
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { newPassword: '', confirmPassword: '' } });

  const onSubmit = handleSubmit(async ({ newPassword }) => {
    try {
      await resetPassword.mutateAsync({ data: { token: token ?? '', newPassword } });
    } catch (error) {
      if (error instanceof ApiError && error.status === 422) {
        // Invalid, expired or used: the API says so in a fixed detail
        setError('root', { type: INVALID_LINK, message: error.detail ?? authStrings.resetPassword.incomplete });
      } else if (error instanceof ApiError && error.status === 409) {
        // Another save to the account came first; the same link works again
        setError('root', { type: 'server', message: authStrings.resetPassword.conflict });
      } else {
        applyServerErrors(error, setError, { newPassword: 'newPassword' });
      }

      return;
    }

    const state: LoginPageState = { passwordChanged: true };
    await navigate(globalPaths.login, { replace: true, state });
  });

  if (token === null || token === '') {
    return (
      <GlobalPageLayout title={authStrings.resetPassword.incompleteTitle}>
        <Typography sx={{ mb: 4 }}>{authStrings.resetPassword.incomplete}</Typography>
        <Button variant="contained" component={RouterLink} to={globalPaths.forgotPassword}>
          {authStrings.resetPassword.requestNewLink}
        </Button>
      </GlobalPageLayout>
    );
  }

  return (
    <GlobalPageLayout title={authStrings.resetPassword.title}>
      <Box component="form" noValidate onSubmit={(event) => void onSubmit(event)} sx={{ display: 'grid', gap: 2.5 }}>
        {errors.root?.message !== undefined && (
          <Alert
            severity="error"
            action={
              errors.root.type === INVALID_LINK && (
                <Link component={RouterLink} to={globalPaths.forgotPassword} sx={{ whiteSpace: 'nowrap' }}>
                  {authStrings.resetPassword.requestNewLink}
                </Link>
              )
            }
          >
            {errors.root.message}
          </Alert>
        )}
        <PasswordField
          control={control}
          name="newPassword"
          label={authStrings.fields.newPassword}
          helperText={authStrings.passwordHint}
          autoComplete="new-password"
          autoFocus
        />
        <PasswordField
          control={control}
          name="confirmPassword"
          label={authStrings.fields.confirmPassword}
          autoComplete="new-password"
        />
        <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>
          {authStrings.resetPassword.submit}
        </Button>
      </Box>
    </GlobalPageLayout>
  );
}
