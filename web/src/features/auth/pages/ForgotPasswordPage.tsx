import { zodResolver } from '@hookform/resolvers/zod';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import { useForm } from 'react-hook-form';
import { Link as RouterLink } from 'react-router';
import { z } from 'zod/mini';
import { useForgotPassword } from '../../../api/generated';
import { GlobalPageLayout } from '../../../shared/components/GlobalPageLayout';
import { applyServerErrors } from '../../../shared/forms/applyServerErrors';
import { FormTextField } from '../../../shared/forms/FormTextField';
import { rules } from '../../../shared/forms/rules';
import { globalPaths } from '../../../shared/routing/paths';
import { authStrings } from '../strings';

const schema = z.object({ email: rules.email });

export function ForgotPasswordPage() {
  const forgotPassword = useForgotPassword();
  const {
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { email: '' } });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await forgotPassword.mutateAsync({ data: values });
    } catch (error) {
      applyServerErrors(error, setError, { email: 'email' });
    }
  });

  // The API answers 202 whether or not the email has an account, and so does this page
  const sentTo = forgotPassword.isSuccess ? forgotPassword.variables.data.email : null;
  if (sentTo !== null) {
    return (
      <GlobalPageLayout title={authStrings.forgotPassword.sentTitle}>
        <Typography sx={{ mb: 2 }}>{authStrings.forgotPassword.sent(sentTo)}</Typography>
        <Typography sx={{ color: 'text.secondary', mb: 4 }}>{authStrings.forgotPassword.sentHint}</Typography>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, alignItems: 'center' }}>
          <Button variant="contained" component={RouterLink} to={globalPaths.login}>
            {authStrings.forgotPassword.backToLogin}
          </Button>
          <Button onClick={() => forgotPassword.reset()}>{authStrings.forgotPassword.sendAgain}</Button>
        </Box>
      </GlobalPageLayout>
    );
  }

  return (
    <GlobalPageLayout title={authStrings.forgotPassword.title}>
      <Typography sx={{ mb: 3 }}>{authStrings.forgotPassword.intro}</Typography>
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
        <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>
          {authStrings.forgotPassword.submit}
        </Button>
      </Box>
      <Link component={RouterLink} to={globalPaths.login} sx={{ display: 'inline-block', mt: 4 }}>
        {authStrings.forgotPassword.backToLogin}
      </Link>
    </GlobalPageLayout>
  );
}
