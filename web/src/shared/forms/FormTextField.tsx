import TextField, { type TextFieldProps } from '@mui/material/TextField';
import { Controller, type Control, type FieldPath, type FieldValues } from 'react-hook-form';

export type FormTextFieldProps<T extends FieldValues> = Omit<
  TextFieldProps,
  'name' | 'value' | 'defaultValue' | 'onChange' | 'onBlur' | 'error' | 'inputRef'
> & {
  control: Control<T>;
  name: FieldPath<T>;
};

/** A text field bound to a React Hook Form field: its error replaces the helper text. Full width. */
export function FormTextField<T extends FieldValues>({ control, name, helperText, ...props }: FormTextFieldProps<T>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field: { ref, ...field }, fieldState }) => (
        <TextField
          {...props}
          {...field}
          // The input itself, so a field error can focus it
          inputRef={ref}
          error={fieldState.invalid}
          helperText={fieldState.error?.message ?? helperText}
          fullWidth
        />
      )}
    />
  );
}
