import { zodResolver } from '@hookform/resolvers/zod';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import LinearProgress from '@mui/material/LinearProgress';
import { useQueryClient } from '@tanstack/react-query';
import type { SyntheticEvent } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { z } from 'zod/mini';
import {
  getGetPropertyQueryKey,
  getListPropertiesQueryKey,
  useCreateProperty,
  useUpdateProperty,
} from '../../../api/generated';
import type { PropertyRequest, PropertyResponse } from '../../../api/generated/model';
import { LoadError } from '../../../shared/components/LoadError';
import { applyServerErrors } from '../../../shared/forms/applyServerErrors';
import { FormAutocomplete } from '../../../shared/forms/FormAutocomplete';
import { FormTextField } from '../../../shared/forms/FormTextField';
import { rules } from '../../../shared/forms/rules';
import { strings } from '../../../shared/strings';
import { countryName } from '../countryName';
import { usePropertyTimeZones } from '../hooks/usePropertyTimeZones';
import { propertyStrings } from '../strings';

const s = propertyStrings.form;

// A field across both columns of the form's grid
const wide = { gridColumn: '1 / -1' };

const schema = z.object({
  name: rules.propertyName,
  address: z.object({
    line1: rules.streetAddress,
    line2: rules.addressLine2,
    city: rules.city,
    postalCode: rules.postalCode,
    countryCode: rules.countryCode,
  }),
  timeZone: rules.timeZone,
});

type PropertyFormValues = z.infer<typeof schema>;

// The API's field names are the form's, in form order
const serverFields = {
  name: 'name',
  'address.line1': 'address.line1',
  'address.line2': 'address.line2',
  'address.city': 'address.city',
  'address.postalCode': 'address.postalCode',
  'address.countryCode': 'address.countryCode',
  timeZone: 'timeZone',
} as const;

interface PropertyFormDialogProps {
  /** The property to edit; a new one when absent. */
  property?: PropertyResponse;
  onClose: () => void;
  /** After the API saved it, with its id. */
  onSaved: (propertyId: string) => void;
}

/**
 * Creates or edits a property. The time zone is one of the chosen country's (ADR 0008): choosing a country with one
 * zone fills it in. Open while mounted, so each opening starts from the property's values.
 */
export function PropertyFormDialog({ property, onClose, onSaved }: PropertyFormDialogProps) {
  const queryClient = useQueryClient();
  const timeZones = usePropertyTimeZones();
  const createProperty = useCreateProperty();
  const updateProperty = useUpdateProperty();
  const {
    control,
    handleSubmit,
    getValues,
    setValue,
    setError,
    formState: { errors, isSubmitting, isSubmitted },
  } = useForm<PropertyFormValues>({ resolver: zodResolver(schema), defaultValues: toFormValues(property) });
  const countryCode = useWatch({ control, name: 'address.countryCode' });

  const zones = timeZones.data?.zonesByCountry.get(countryCode) ?? [];
  // A stored zone the list doesn't name (an alias the API took) stays selectable
  const storedZone = property?.address.countryCode === countryCode ? property.timeZone : undefined;
  const zoneOptions = storedZone === undefined || zones.includes(storedZone) ? zones : [...zones, storedZone];

  const onCountryChange = (country: string) => {
    const countryZones = timeZones.data?.zonesByCountry.get(country) ?? [];
    const options = { shouldValidate: isSubmitted };
    if (countryZones.length === 1) {
      setValue('timeZone', countryZones[0] ?? '', options);
    } else if (!countryZones.includes(getValues('timeZone'))) {
      setValue('timeZone', '', options);
    }
  };

  const onSubmit = handleSubmit(async ({ name, address, timeZone }) => {
    const data: PropertyRequest = {
      name,
      address: { ...address, line2: address.line2 || null, postalCode: address.postalCode || null },
      timeZone,
    };
    let propertyId: string;
    try {
      if (property === undefined) {
        ({ id: propertyId } = await createProperty.mutateAsync({ data }));
      } else {
        propertyId = property.id;
        await updateProperty.mutateAsync({ id: propertyId, data });
      }
    } catch (error) {
      applyServerErrors(error, setError, serverFields);
      return;
    }

    void queryClient.invalidateQueries({ queryKey: getListPropertiesQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getGetPropertyQueryKey(propertyId) });
    onSaved(propertyId);
  });

  return (
    <Dialog
      open
      onClose={isSubmitting ? undefined : onClose}
      fullWidth
      maxWidth="sm"
      // The paper is the form, so the actions below the scrolling content submit it
      slotProps={{ paper: { component: 'form', onSubmit: (event: SyntheticEvent) => void onSubmit(event) } }}
    >
      <DialogTitle>{property === undefined ? s.createTitle : s.editTitle}</DialogTitle>
      <DialogContent>
        {timeZones.data === undefined ? (
          timeZones.error !== null ? (
            <LoadError error={timeZones.error} onRetry={timeZones.refetch} />
          ) : (
            <LinearProgress aria-label={s.loadingCountries} />
          )
        ) : (
          <Box
            sx={{
              display: 'grid',
              gap: 2.5,
              gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
              pt: 1,
            }}
          >
            {errors.root?.message !== undefined && (
              <Alert severity="error" sx={wide}>
                {errors.root.message}
              </Alert>
            )}
            <FormTextField control={control} name="name" label={s.name} sx={wide} />
            <FormTextField control={control} name="address.line1" label={s.streetAddress} sx={wide} />
            <FormTextField control={control} name="address.line2" label={s.addressLine2} sx={wide} />
            <FormTextField control={control} name="address.city" label={s.city} />
            <FormTextField control={control} name="address.postalCode" label={s.postalCode} />
            <FormAutocomplete
              control={control}
              name="address.countryCode"
              label={s.country}
              options={timeZones.data.countries}
              getOptionLabel={(code) => `${countryName(code)} (${code})`}
              onChange={onCountryChange}
            />
            <FormAutocomplete
              control={control}
              name="timeZone"
              label={s.timeZone}
              options={zoneOptions}
              disabled={countryCode === ''}
              helperText={countryCode === '' ? s.chooseCountryFirst : s.timeZoneHint}
            />
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isSubmitting}>
          {strings.common.cancel}
        </Button>
        <Button type="submit" variant="contained" disabled={isSubmitting || timeZones.data === undefined}>
          {property === undefined ? s.create : s.save}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function toFormValues(property: PropertyResponse | undefined): PropertyFormValues {
  const address = property?.address;
  return {
    name: property?.name ?? '',
    address: {
      line1: address?.line1 ?? '',
      line2: address?.line2 ?? '',
      city: address?.city ?? '',
      postalCode: address?.postalCode ?? '',
      countryCode: address?.countryCode ?? '',
    },
    timeZone: property?.timeZone ?? '',
  };
}
