import ArrowBack from '@mui/icons-material/ArrowBack';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { keepPreviousData, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router';
import { ApiError } from '../../../api/ApiError';
import {
  getGetPropertyQueryKey,
  getListPropertiesQueryKey,
  getListUnitsQueryKey,
  useArchiveProperty,
  useGetProperty,
  useListUnits,
} from '../../../api/generated';
import type { PropertyAddress } from '../../../api/generated/model';
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog';
import { LoadError } from '../../../shared/components/LoadError';
import { NotFoundPage } from '../../../shared/components/NotFoundPage';
import { FIRST_PAGE } from '../components/paging';
import { PropertyFormDialog } from '../components/PropertyFormDialog';
import { UnitsSection } from '../components/UnitsSection';
import { countryName } from '../countryName';
import { propertyStrings } from '../strings';

const s = propertyStrings.detail;

/** One property: its address and time zone, editing and archiving it, and its units. Managers and Admins only. */
export function PropertyDetailPage() {
  const { propertyId = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const property = useGetProperty(propertyId);
  const [unitsPage, setUnitsPage] = useState(FIRST_PAGE);
  const units = useListUnits(
    propertyId,
    { page: unitsPage.page + 1, pageSize: unitsPage.pageSize },
    { query: { placeholderData: keepPreviousData } },
  );
  const archiveProperty = useArchiveProperty();
  const [dialog, setDialog] = useState<'edit' | 'archive' | null>(null);

  if (property.data === undefined) {
    // Another organization's property, an archived one or a mistyped id
    if (property.error instanceof ApiError && property.error.status === 404) {
      return <NotFoundPage />;
    }

    return property.error !== null ? (
      <LoadError error={property.error} onRetry={property.refetch} />
    ) : (
      <Box aria-busy>
        <Skeleton variant="text" sx={{ typography: 'h5', width: '40%' }} />
        <Skeleton variant="text" sx={{ width: '60%' }} />
        <Skeleton variant="text" sx={{ width: '30%' }} />
      </Box>
    );
  }

  const { name, address, timeZone } = property.data;

  const archive = async () => {
    await archiveProperty.mutateAsync({ id: propertyId });
    void queryClient.invalidateQueries({ queryKey: getListPropertiesQueryKey() });
    await navigate('..', { relative: 'path' });
    // Gone for good: nothing should show it again from the cache
    queryClient.removeQueries({ queryKey: getGetPropertyQueryKey(propertyId) });
    queryClient.removeQueries({ queryKey: getListUnitsQueryKey(propertyId) });
  };

  return (
    <Box>
      <Link
        component={RouterLink}
        to=".."
        relative="path"
        sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, mb: 2 }}
      >
        <ArrowBack fontSize="small" />
        {s.allProperties}
      </Link>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        sx={{ alignItems: { sm: 'flex-start' }, justifyContent: 'space-between', gap: 2 }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h5" component="h1" gutterBottom sx={{ overflowWrap: 'anywhere' }}>
            {name}
          </Typography>
          {addressLines(address).map((line, index) => (
            // Lines can repeat (a city named like its street), and the list never reorders
            <Typography key={index}>{line}</Typography>
          ))}
          <Typography sx={{ color: 'text.secondary', mt: 1 }}>{s.timeZone(timeZone)}</Typography>
        </Box>
        <Stack direction="row" sx={{ gap: 1, flexShrink: 0 }}>
          <Button variant="outlined" onClick={() => setDialog('edit')}>
            {s.edit}
          </Button>
          <Button variant="outlined" color="error" onClick={() => setDialog('archive')}>
            {s.archive}
          </Button>
        </Stack>
      </Stack>

      <UnitsSection
        propertyId={propertyId}
        units={units.data}
        loading={units.isFetching}
        error={units.error}
        onRetry={units.refetch}
        paginationModel={unitsPage}
        onPaginationModelChange={setUnitsPage}
      />

      {dialog === 'edit' && (
        <PropertyFormDialog property={property.data} onClose={() => setDialog(null)} onSaved={() => setDialog(null)} />
      )}
      {dialog === 'archive' && (
        <ConfirmDialog
          title={s.archiveTitle(name)}
          message={s.archiveMessage(units.data?.totalCount ?? 0)}
          confirmLabel={s.archiveConfirm}
          onConfirm={archive}
          onClose={() => setDialog(null)}
        />
      )}
    </Box>
  );
}

/** The address as it's written on an envelope, ending with the country's name. */
function addressLines({ line1, line2, city, postalCode, countryCode }: PropertyAddress): string[] {
  return [line1, line2, [postalCode, city].filter(Boolean).join(' '), countryName(countryCode)].filter(
    (line): line is string => Boolean(line),
  );
}
