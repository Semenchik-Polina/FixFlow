import Autocomplete from '@mui/material/Autocomplete';
import TextField from '@mui/material/TextField';
import type { ReactNode } from 'react';
import { Controller, type Control, type FieldPath, type FieldValues } from 'react-hook-form';

export interface FormAutocompleteProps<T extends FieldValues> {
  control: Control<T>;
  /** A string field: an option, or `''` while none is picked. */
  name: FieldPath<T>;
  label: string;
  options: readonly string[];
  getOptionLabel?: (option: string) => string;
  disabled?: boolean;
  helperText?: ReactNode;
  /** Called with the picked option (`''` when cleared), once the field has it. */
  onChange?: (option: string) => void;
}

/**
 * A pick-one list bound to a React Hook Form string field: type to filter, choose with the keyboard or the mouse. Its
 * error replaces the helper text, and a server error can focus it. Full width.
 */
export function FormAutocomplete<T extends FieldValues>({
  control,
  name,
  label,
  options,
  getOptionLabel,
  disabled,
  helperText,
  onChange,
}: FormAutocompleteProps<T>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <Autocomplete
          options={options}
          value={field.value === '' ? null : field.value}
          onChange={(_, option) => {
            field.onChange(option ?? '');
            onChange?.(option ?? '');
          }}
          onBlur={field.onBlur}
          getOptionLabel={getOptionLabel}
          disabled={disabled}
          autoHighlight
          fullWidth
          renderInput={(params) => (
            <TextField
              {...params}
              name={field.name}
              // The input itself, so a field error can focus it
              inputRef={field.ref}
              label={label}
              error={fieldState.invalid}
              helperText={fieldState.error?.message ?? helperText}
            />
          )}
        />
      )}
    />
  );
}
