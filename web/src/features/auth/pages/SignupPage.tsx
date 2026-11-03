import { zodResolver } from '@hookform/resolvers/zod';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import { FormProvider, useForm } from 'react-hook-form';
import { Link as RouterLink, useNavigate } from 'react-router';
import { z } from 'zod/mini';
import { getGetMeQueryKey, useSignUp } from '../../../api/generated';
import { GlobalPageLayout } from '../../../shared/components/GlobalPageLayout';
import { applyServerErrors } from '../../../shared/forms/applyServerErrors';
import { FormTextField } from '../../../shared/forms/FormTextField';
import { OrganizationFields } from '../../../shared/forms/OrganizationFields';
import { PasswordField } from '../../../shared/forms/PasswordField';
import { rules } from '../../../shared/forms/rules';
import { globalPaths } from '../../../shared/routing/paths';
import { useGlobalQueryClient } from '../../../shared/session/globalQueryClient';
import { authStrings } from '../strings';

const schema = z.object({
  firstName: rules.firstName,
  lastName: rules.lastName,
  email: rules.email,
  password: rules.newPassword,
  organizationName: rules.organizationName,
  slug: rules.slug,
});

/** The visitor creates their account and their first organization, and is signed in (D24). */
export function SignupPage() {
  const navigate = useNavigate();
  const queryClient = useGlobalQueryClient();
  const signUp = useSignUp();
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: { firstName: '', lastName: '', email: '', password: '', organizationName: '', slug: '' },
  });
  const {
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = form;

  const onSubmit = handleSubmit(async (values) => {
    let slug: string;
    try {
      ({ slug } = await signUp.mutateAsync({ data: values }));
    } catch (error) {
      applyServerErrors(error, setError, {
        firstName: 'firstName',
        lastName: 'lastName',
        email: 'email',
        password: 'password',
        organizationName: 'organizationName',
        slug: 'slug',
      });
      return;
    }

    queryClient.removeQueries({ queryKey: getGetMeQueryKey() });
    await navigate(`/${slug}`, { replace: true });
  });

  return (
    <GlobalPageLayout title={authStrings.signup.title}>
      <Typography sx={{ mb: 4 }}>{authStrings.signup.intro}</Typography>
      <FormProvider {...form}>
        <Box component="form" noValidate onSubmit={(event) => void onSubmit(event)} sx={{ display: 'grid', gap: 2.5 }}>
          {errors.root?.message !== undefined && <Alert severity="error">{errors.root.message}</Alert>}
          <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600 }}>
            {authStrings.signup.aboutYou}
          </Typography>
          <FormTextField
            control={control}
            name="firstName"
            label={authStrings.fields.firstName}
            autoComplete="given-name"
            autoFocus
          />
          <FormTextField
            control={control}
            name="lastName"
            label={authStrings.fields.lastName}
            autoComplete="family-name"
          />
          <FormTextField
            control={control}
            name="email"
            label={authStrings.fields.email}
            type="email"
            autoComplete="email"
          />
          <PasswordField
            control={control}
            name="password"
            label={authStrings.fields.password}
            helperText={authStrings.passwordHint}
            autoComplete="new-password"
          />
          <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600, mt: 2 }}>
            {authStrings.signup.yourOrganization}
          </Typography>
          <OrganizationFields />
          <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>
            {authStrings.signup.submit}
          </Button>
        </Box>
      </FormProvider>
      <Typography sx={{ mt: 4 }}>
        {authStrings.signup.haveAccount}{' '}
        <Link component={RouterLink} to={globalPaths.login}>
          {authStrings.signup.logIn}
        </Link>
      </Typography>
    </GlobalPageLayout>
  );
}
