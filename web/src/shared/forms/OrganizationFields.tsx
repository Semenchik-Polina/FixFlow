import InputAdornment from '@mui/material/InputAdornment';
import TextField from '@mui/material/TextField';
import { Controller, useFormContext, useWatch } from 'react-hook-form';
import { strings } from '../strings';
import { suggestSlug } from '../utils/suggestSlug';
import { FormTextField } from './FormTextField';

/** The fields this component adds to the surrounding form, which provides them through `FormProvider`. */
export interface OrganizationFieldsValues {
  organizationName: string;
  slug: string;
}

/**
 * An organization's name and slug, for signup and for creating another organization. The slug follows the name until
 * the user edits it (A10), and the helper text shows the address it makes, because it can't change later.
 */
export function OrganizationFields() {
  const { control, setValue, formState } = useFormContext<OrganizationFieldsValues>();
  const slug = useWatch({ control, name: 'slug' });
  const slugEdited = formState.dirtyFields.slug === true;

  return (
    <>
      <Controller
        control={control}
        name="organizationName"
        render={({ field: { ref, onChange, ...field }, fieldState }) => (
          <TextField
            {...field}
            inputRef={ref}
            onChange={(event) => {
              onChange(event);
              if (!slugEdited) {
                setValue('slug', suggestSlug(event.target.value), { shouldValidate: formState.isSubmitted });
              }
            }}
            label={strings.organization.name}
            autoComplete="organization"
            error={fieldState.invalid}
            helperText={fieldState.error?.message}
            fullWidth
          />
        )}
      />
      <FormTextField
        control={control}
        name="slug"
        label={strings.organization.slug}
        helperText={strings.organization.slugHint(
          `${window.location.host}/${slug || strings.organization.slugExample}`,
        )}
        autoComplete="off"
        spellCheck={false}
        slotProps={{ input: { startAdornment: <InputAdornment position="start">/</InputAdornment> } }}
      />
    </>
  );
}
