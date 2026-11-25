import Add from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { DataGrid, type GridColDef } from '@mui/x-data-grid';
import { keepPreviousData } from '@tanstack/react-query';
import { useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { useListProperties } from '../../../api/generated';
import type { PropertyResponse } from '../../../api/generated/model';
import { LoadError } from '../../../shared/components/LoadError';
import { useDebouncedValue } from '../../../shared/hooks/useDebouncedValue';
import { FIRST_PAGE, PAGE_SIZE_OPTIONS } from '../components/paging';
import { PropertyFormDialog } from '../components/PropertyFormDialog';
import { propertyStrings } from '../strings';

const s = propertyStrings.list;

/** How long typing pauses before the search runs. */
const SEARCH_DELAY_MS = 300;

const columns: GridColDef<PropertyResponse>[] = [
  {
    field: 'name',
    headerName: propertyStrings.columns.name,
    flex: 1,
    minWidth: 200,
    // A link, so the property opens from the keyboard too; the grid decides which cell's link is in the tab order
    renderCell: ({ row, tabIndex }) => (
      <Link component={RouterLink} to={row.id} tabIndex={tabIndex}>
        {row.name}
      </Link>
    ),
  },
  {
    field: 'address',
    headerName: propertyStrings.columns.address,
    flex: 1.5,
    minWidth: 220,
    valueGetter: (_, row) => `${row.address.line1}, ${row.address.city}`,
  },
  { field: 'timeZone', headerName: propertyStrings.columns.timeZone, flex: 1, minWidth: 160 },
];

/** The organization's properties, a page at a time, searchable by name. Managers and Admins only. */
export function PropertiesPage() {
  const navigate = useNavigate();
  const [paginationModel, setPaginationModel] = useState(FIRST_PAGE);
  const [search, setSearch] = useState('');
  const name = useDebouncedValue(search.trim(), SEARCH_DELAY_MS);
  const [creating, setCreating] = useState(false);
  const properties = useListProperties(
    { page: paginationModel.page + 1, pageSize: paginationModel.pageSize, ...(name !== '' && { name }) },
    // The current page stays while the next loads, so the grid doesn't empty on every page change
    { query: { placeholderData: keepPreviousData } },
  );

  return (
    <Box>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 3 }}>
        <Typography variant="h5" component="h1">
          {s.title}
        </Typography>
        <Button variant="contained" startIcon={<Add />} onClick={() => setCreating(true)}>
          {s.create}
        </Button>
      </Stack>
      <TextField
        type="search"
        label={s.search}
        size="small"
        value={search}
        onChange={(event) => {
          setSearch(event.target.value);
          // Results from the first page, as soon as the search runs
          setPaginationModel((model) => ({ ...model, page: 0 }));
        }}
        sx={{ mb: 2, width: { xs: '100%', sm: 320 } }}
      />
      {properties.error !== null && properties.data === undefined ? (
        <LoadError error={properties.error} onRetry={properties.refetch} />
      ) : (
        // A flex column lets the grid take the height of its rows
        <Box sx={{ display: 'flex', flexDirection: 'column' }}>
          <DataGrid
            aria-label={s.title}
            rows={properties.data?.items ?? []}
            columns={columns}
            rowCount={properties.data?.totalCount ?? 0}
            loading={properties.isFetching}
            paginationMode="server"
            paginationModel={paginationModel}
            onPaginationModelChange={setPaginationModel}
            pageSizeOptions={PAGE_SIZE_OPTIONS}
            // The API orders properties by name and filters by name only, with the search above
            disableColumnSorting
            disableColumnFilter
            disableColumnMenu
            disableRowSelectionOnClick
            localeText={{ noRowsLabel: name === '' ? s.empty : s.noMatch }}
            slotProps={{ loadingOverlay: { variant: 'skeleton', noRowsVariant: 'skeleton' } }}
          />
        </Box>
      )}

      {creating && (
        <PropertyFormDialog onClose={() => setCreating(false)} onSaved={(propertyId) => void navigate(propertyId)} />
      )}
    </Box>
  );
}
