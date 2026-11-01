// zod/mini: the same schemas as `zod`, but tree-shakable, which keeps about 45 kB (gzipped) out of the sign-in page
import { z } from 'zod/mini';
import { strings } from '../strings';

// The server's validation rules, mirrored so most mistakes show before a round trip. The server stays authoritative:
// whatever it rejects reaches the field through applyServerErrors. Each limit names its source.

const messages = strings.validation;

/** `User.EmailMaxLength`. */
const EMAIL_MAX_LENGTH = 256;
/** `User.NameMaxLength`. */
const PERSON_NAME_MAX_LENGTH = 100;
/** `Organization.NameMaxLength`. */
const ORGANIZATION_NAME_MAX_LENGTH = 100;
/** `PasswordPolicy` (S3): the length is the only rule. */
const PASSWORD_MIN_LENGTH = 10;
const PASSWORD_MAX_LENGTH = 128;
/** `OrganizationSlug`. */
export const SLUG_MAX_LENGTH = 40;
const SLUG_MIN_LENGTH = 3;
const SLUG_FORMAT = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

// One @ with something on each side and no spaces. The server checks more (a bare address), but never less, so this
// can't turn away an address the server would take.
const EMAIL_FORMAT = /^[^\s@]+@[^\s@]+$/;

/** Trimmed text that must not be empty, then at most `maxLength` characters. */
const requiredText = (message: string, maxLength: number) =>
  z.string().check(z.trim(), z.minLength(1, message), z.maxLength(maxLength, messages.tooLong(maxLength)));

export const rules = {
  email: requiredText(messages.emailRequired, EMAIL_MAX_LENGTH).check(z.regex(EMAIL_FORMAT, messages.emailInvalid)),
  /** A password to sign in with: taken as written, never trimmed. */
  currentPassword: z.string().check(z.minLength(1, messages.passwordRequired)),
  /** A password being set: taken as written, never trimmed. */
  newPassword: z
    .string()
    .check(
      z.minLength(1, messages.passwordRequired),
      z.minLength(PASSWORD_MIN_LENGTH, messages.passwordTooShort(PASSWORD_MIN_LENGTH)),
      z.maxLength(PASSWORD_MAX_LENGTH, messages.tooLong(PASSWORD_MAX_LENGTH)),
    ),
  firstName: requiredText(messages.firstNameRequired, PERSON_NAME_MAX_LENGTH),
  lastName: requiredText(messages.lastNameRequired, PERSON_NAME_MAX_LENGTH),
  organizationName: requiredText(messages.organizationNameRequired, ORGANIZATION_NAME_MAX_LENGTH),
  /** Taken as written, like the server (P21). Reserved words are the server's to report. */
  slug: z
    .string()
    .check(
      z.minLength(1, messages.slugRequired),
      z.minLength(SLUG_MIN_LENGTH, messages.slugLength(SLUG_MIN_LENGTH, SLUG_MAX_LENGTH)),
      z.maxLength(SLUG_MAX_LENGTH, messages.slugLength(SLUG_MIN_LENGTH, SLUG_MAX_LENGTH)),
      z.regex(SLUG_FORMAT, messages.slugFormat),
    ),
};
