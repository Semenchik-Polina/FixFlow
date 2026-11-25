import { zodResolver } from '@hookform/resolvers/zod';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import { useQueryClient } from '@tanstack/react-query';
import type { SyntheticEvent } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod/mini';
import { getListUnitsQueryKey, useCreateUnit, useRenameUnit } from '../../../api/generated';
import type { UnitResponse } from '../../../api/generated/model';
import { applyServerErrors } from '../../../shared/forms/applyServerErrors';
import { FormTextField } from '../../../shared/forms/FormTextField';
import { rules } from '../../../shared/forms/rules';
import { strings } from '../../../shared/strings';
import { propertyStrings } from '../strings';

const s = propertyStrings.units;

const schema = z.object({ name: rules.unitName });

interface UnitNameDialogProps {
  propertyId: string;
  /** The unit to rename; a new unit when absent. */
  unit?: UnitResponse;
  onClose: () => void;
}

/**
 * Adds a unit to a property, or renames one. A name another unit of the property has comes back from the API as an
 * error on the field. Open while mounted.
 */
export function UnitNameDialog({ propertyId, unit, onClose }: UnitNameDialogProps) {
  const queryClient = useQueryClient();
  const createUnit = useCreateUnit();
  const renameUnit = useRenameUnit();
  const {
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { name: unit?.name ?? '' } });

  const onSubmit = handleSubmit(async ({ name }) => {
    try {
      if (unit === undefined) {
        await createUnit.mutateAsync({ propertyId, data: { name } });
      } else {
        await renameUnit.mutateAsync({ propertyId, unitId: unit.id, data: { name } });
      }
    } catch (error) {
      applyServerErrors(error, setError, { name: 'name' });
      return;
    }

    void queryClient.invalidateQueries({ queryKey: getListUnitsQueryKey(propertyId) });
    onClose();
  });

  return (
    <Dialog
      open
      onClose={isSubmitting ? undefined : onClose}
      fullWidth
      maxWidth="xs"
      slotProps={{ paper: { component: 'form', onSubmit: (event: SyntheticEvent) => void onSubmit(event) } }}
    >
      <DialogTitle>{unit === undefined ? s.addTitle : s.renameTitle}</DialogTitle>
      <DialogContent sx={{ display: 'grid', gap: 2.5 }}>
        {errors.root?.message !== undefined && <Alert severity="error">{errors.root.message}</Alert>}
        <FormTextField control={control} name="name" label={s.name} autoFocus sx={{ mt: 1 }} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isSubmitting}>
          {strings.common.cancel}
        </Button>
        <Button type="submit" variant="contained" disabled={isSubmitting}>
          {unit === undefined ? s.add : s.save}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
