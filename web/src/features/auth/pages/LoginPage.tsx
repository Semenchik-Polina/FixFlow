import { zodResolver } from '@hookform/resolvers/zod';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { Controller, useForm } from 'react-hook-form';
import { Link as RouterLink, useLocation, useNavigate } from 'react-router';
import { z } from 'zod/mini';
import { getGetMeQueryKey, useLogIn } from '../../../api/generated';
import { GlobalPageLayout } from '../../../shared/components/GlobalPageLayout';
import { applyServerErrors } from '../../../shared/forms/applyServerErrors';
import { FormTextField } from '../../../shared/forms/FormTextField';
import { PasswordField } from '../../../shared/forms/PasswordField';
import { rules } from '../../../shared/forms/rules';
import { globalPaths } from '../../../shared/routing/paths';
import { useGlobalQueryClient } from '../../../shared/session/globalQueryClient';
import { authStrings } from '../strings';

const schema = z.object({
  email: rules.email,
  password: rules.currentPassword,
  rememberMe: z.boolean(),
});

/** What the reset password page passes along, so this page can say the new password is set. */
export interface LoginPageState {
  passwordChanged?: boolean;
}

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useGlobalQueryClient();
  const logIn = useLogIn();
  const {
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '', rememberMe: false },
  });

  const passwordChanged = (location.state as LoginPageState | null)?.passwordChanged === true;

  const onSubmit = handleSubmit(async (values) => {
    try {
      await logIn.mutateAsync({ data: values });
    } catch (error) {
      // A failed sign-in is one fixed message, never a field error: it can't say which field is wrong (P26)
      applyServerErrors(error, setError, { email: 'email', password: 'password' });
      return;
    }

    // Another account may have been signed in before: the routing after sign-in reads a fresh session
    queryClient.removeQueries({ queryKey: getGetMeQueryKey() });
    await navigate('/', { replace: true });
  });

  return (
    <GlobalPageLayout title={authStrings.login.title}>
      {passwordChanged && (
        <Alert severity="success" sx={{ mb: 3 }}>
          {authStrings.login.passwordChanged}
        </Alert>
      )}
      <Box component="form" noValidate onSubmit={(event) => void onSubmit(event)} sx={{ display: 'grid', gap: 2.5 }}>
        {errors.root?.message !== undefined && <Alert severity="error">{errors.root.message}</Alert>}
        <FormTextField
          control={control}
          name="email"
          label={authStrings.fields.email}
          type="email"
          autoComplete="username"
          autoFocus
        />
        <PasswordField
          control={control}
          name="password"
          label={authStrings.fields.password}
          autoComplete="current-password"
        />
        <Controller
          control={control}
          name="rememberMe"
          render={({ field: { value, onChange, ref, ...field } }) => (
            <FormControlLabel
              label={authStrings.login.rememberMe}
              control={
                <Checkbox
                  {...field}
                  checked={value}
                  onChange={(event) => onChange(event.target.checked)}
                  slotProps={{ input: { ref } }}
                />
              }
            />
          )}
        />
        <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>
          {authStrings.login.submit}
        </Button>
      </Box>
      <Stack sx={{ mt: 4, gap: 1.5, alignItems: 'flex-start' }}>
        <Link component={RouterLink} to={globalPaths.forgotPassword}>
          {authStrings.login.forgotPassword}
        </Link>
        <Typography>
          {authStrings.login.noAccount}{' '}
          <Link component={RouterLink} to={globalPaths.signup}>
            {authStrings.login.signUp}
          </Link>
        </Typography>
      </Stack>
    </GlobalPageLayout>
  );
}
