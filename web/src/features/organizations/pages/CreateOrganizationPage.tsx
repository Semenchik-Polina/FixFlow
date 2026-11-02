import { zodResolver } from '@hookform/resolvers/zod';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import { FormProvider, useForm } from 'react-hook-form';
import { Link as RouterLink, useNavigate } from 'react-router';
import { z } from 'zod/mini';
import { getGetMeQueryKey, useCreateOrganization } from '../../../api/generated';
import { GlobalPageLayout } from '../../../shared/components/GlobalPageLayout';
import { applyServerErrors } from '../../../shared/forms/applyServerErrors';
import { OrganizationFields } from '../../../shared/forms/OrganizationFields';
import { rules } from '../../../shared/forms/rules';
import { globalPaths } from '../../../shared/routing/paths';
import { useGlobalQueryClient } from '../../../shared/session/globalQueryClient';
import { SignedInAs } from '../../../shared/session/SignedInAs';
import { organizationStrings } from '../strings';

const schema = z.object({ organizationName: rules.organizationName, slug: rules.slug });

/** A signed-in user creates another organization and becomes its admin. */
export function CreateOrganizationPage() {
  const navigate = useNavigate();
  const queryClient = useGlobalQueryClient();
  const createOrganization = useCreateOrganization();
  const form = useForm({ resolver: zodResolver(schema), defaultValues: { organizationName: '', slug: '' } });
  const {
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = form;

  const onSubmit = handleSubmit(async ({ organizationName, slug }) => {
    try {
      await createOrganization.mutateAsync({ data: { name: organizationName, slug } });
    } catch (error) {
      applyServerErrors(error, setError, { name: 'organizationName', slug: 'slug' });
      return;
    }

    // The new membership: the organization's layout checks the slug against a fresh session
    queryClient.removeQueries({ queryKey: getGetMeQueryKey() });
    await navigate(`/${slug}`, { replace: true });
  });

  return (
    <GlobalPageLayout title={organizationStrings.create.title}>
      <Typography sx={{ mb: 4 }}>{organizationStrings.create.intro}</Typography>
      <FormProvider {...form}>
        <Box component="form" noValidate onSubmit={(event) => void onSubmit(event)} sx={{ display: 'grid', gap: 2.5 }}>
          {errors.root?.message !== undefined && <Alert severity="error">{errors.root.message}</Alert>}
          <OrganizationFields />
          <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>
            {organizationStrings.create.submit}
          </Button>
        </Box>
      </FormProvider>
      <Link component={RouterLink} to={globalPaths.organizations} sx={{ display: 'inline-block', mt: 4 }}>
        {organizationStrings.create.cancel}
      </Link>
      <SignedInAs />
    </GlobalPageLayout>
  );
}
