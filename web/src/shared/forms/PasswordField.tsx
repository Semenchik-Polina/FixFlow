import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import { useState } from 'react';
import type { FieldValues } from 'react-hook-form';
import { strings } from '../strings';
import { FormTextField, type FormTextFieldProps } from './FormTextField';

/** A password field with a button that shows what was typed, which helps on a phone keyboard. */
export function PasswordField<T extends FieldValues>(props: Omit<FormTextFieldProps<T>, 'type' | 'slotProps'>) {
  const [visible, setVisible] = useState(false);

  return (
    <FormTextField
      {...props}
      type={visible ? 'text' : 'password'}
      slotProps={{
        input: {
          endAdornment: (
            <InputAdornment position="end">
              <IconButton
                aria-label={visible ? strings.forms.hidePassword : strings.forms.showPassword}
                onClick={() => setVisible((shown) => !shown)}
                edge="end"
              >
                {visible ? <VisibilityOff /> : <Visibility />}
              </IconButton>
            </InputAdornment>
          ),
        },
      }}
    />
  );
}
