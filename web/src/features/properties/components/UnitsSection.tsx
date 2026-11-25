import Add from '@mui/icons-material/Add';
import ArchiveOutlined from '@mui/icons-material/ArchiveOutlined';
import EditOutlined from '@mui/icons-material/EditOutlined';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { DataGrid, GridActionsCellItem, type GridColDef, type GridPaginationModel } from '@mui/x-data-grid';
import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { getListUnitsQueryKey, useArchiveUnit } from '../../../api/generated';
import type { PagedResultOfUnitResponse, UnitResponse } from '../../../api/generated/model';
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog';
import { LoadError } from '../../../shared/components/LoadError';
import { propertyStrings } from '../strings';
import { PAGE_SIZE_OPTIONS } from './paging';
import { UnitNameDialog } from './UnitNameDialog';

const s = propertyStrings.units;

type OpenDialog = { kind: 'add' } | { kind: 'rename' | 'archive'; unit: UnitResponse } | null;

interface UnitsSectionProps {
  propertyId: string;
  /** The page of units shown, from the property page's query; undefined until it first loads. */
  units: PagedResultOfUnitResponse | undefined;
  loading: boolean;
  error: unknown;
  onRetry: () => unknown;
  paginationModel: GridPaginationModel;
  onPaginationModelChange: (model: GridPaginationModel) => void;
}

/** A property's units, a page at a time, with adding, renaming and archiving. */
export function UnitsSection({
  propertyId,
  units,
  loading,
  error,
  onRetry,
  paginationModel,
  onPaginationModelChange,
}: UnitsSectionProps) {
  const queryClient = useQueryClient();
  const archiveUnit = useArchiveUnit();
  const [dialog, setDialog] = useState<OpenDialog>(null);

  const columns = useMemo<GridColDef<UnitResponse>[]>(
    () => [
      { field: 'name', headerName: propertyStrings.columns.name, flex: 1, minWidth: 160 },
      {
        field: 'actions',
        type: 'actions',
        headerName: propertyStrings.columns.actions,
        width: 120,
        getActions: ({ row }) => [
          <GridActionsCellItem
            key="rename"
            icon={<EditOutlined />}
            label={s.rename(row.name)}
            onClick={() => setDialog({ kind: 'rename', unit: row })}
          />,
          <GridActionsCellItem
            key="archive"
            icon={<ArchiveOutlined />}
            label={s.archive(row.name)}
            onClick={() => setDialog({ kind: 'archive', unit: row })}
          />,
        ],
      },
    ],
    [],
  );

  const close = () => setDialog(null);

  return (
    <Box component="section" sx={{ mt: 5 }}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', gap: 2, mb: 2 }}>
        <Typography variant="h6" component="h2">
          {s.title}
        </Typography>
        <Button variant="outlined" startIcon={<Add />} onClick={() => setDialog({ kind: 'add' })}>
          {s.add}
        </Button>
      </Stack>
      {error !== null && units === undefined ? (
        <LoadError error={error} onRetry={onRetry} />
      ) : (
        // A flex column lets the grid take the height of its rows
        <Box sx={{ display: 'flex', flexDirection: 'column' }}>
          <DataGrid
            aria-label={s.title}
            rows={units?.items ?? []}
            columns={columns}
            rowCount={units?.totalCount ?? 0}
            loading={loading}
            paginationMode="server"
            paginationModel={paginationModel}
            onPaginationModelChange={onPaginationModelChange}
            pageSizeOptions={PAGE_SIZE_OPTIONS}
            // The API orders units by name and takes no sort or filter
            disableColumnSorting
            disableColumnFilter
            disableColumnMenu
            disableRowSelectionOnClick
            localeText={{ noRowsLabel: s.empty }}
            slotProps={{ loadingOverlay: { variant: 'skeleton', noRowsVariant: 'skeleton' } }}
          />
        </Box>
      )}

      {dialog?.kind === 'add' && <UnitNameDialog propertyId={propertyId} onClose={close} />}
      {dialog?.kind === 'rename' && <UnitNameDialog propertyId={propertyId} unit={dialog.unit} onClose={close} />}
      {dialog?.kind === 'archive' && (
        <ConfirmDialog
          title={s.archiveTitle(dialog.unit.name)}
          message={s.archiveMessage}
          confirmLabel={s.archiveConfirm}
          onConfirm={async () => {
            await archiveUnit.mutateAsync({ propertyId, unitId: dialog.unit.id });
            void queryClient.invalidateQueries({ queryKey: getListUnitsQueryKey(propertyId) });
            close();
          }}
          onClose={close}
        />
      )}
    </Box>
  );
}
