import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import prettier from 'eslint-config-prettier';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default defineConfig([
  // Generated code is never linted (web/CLAUDE.md)
  globalIgnores(['dist', 'coverage', 'src/api/generated']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommendedTypeChecked,
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      'no-restricted-globals': [
        'error',
        {
          name: 'fetch',
          message: 'Call the API through the generated client (src/api/generated), which uses src/api/http.ts.',
        },
      ],
      // The package roots re-export everything (the icons alone are thousands of modules) and slow down the dev server
      'no-restricted-imports': [
        'error',
        {
          paths: [
            { name: '@mui/material', message: "Import each component from its own path: '@mui/material/Button'." },
            { name: '@mui/icons-material', message: "Import each icon from its own path: '@mui/icons-material/Menu'." },
          ],
        },
      ],
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
          message: 'Render text, never raw HTML (S14).',
        },
      ],
    },
  },
  {
    // The one place that calls fetch, and the tests that check it
    files: ['src/api/http.ts', 'src/**/*.test.{ts,tsx}'],
    rules: {
      'no-restricted-globals': 'off',
    },
  },
  {
    files: ['*.config.{js,ts}'],
    languageOptions: {
      globals: globals.node,
    },
  },
  prettier,
]);
