import path from 'node:path';
import { createRequire } from 'node:module';
import localRules from './eslint-local-rules.js';

export const createEslintConfig = (tsconfigRootDir = import.meta.dirname) => {
  const require = createRequire(path.join(tsconfigRootDir, 'package.json'));
  const unwrap = (module) => module.default ?? module;
  const { fixupPluginRules } = require('@eslint/compat');
  const tsPlugin = unwrap(require('@typescript-eslint/eslint-plugin'));
  const tsParser = unwrap(require('@typescript-eslint/parser'));
  const prettierConfig = unwrap(require('eslint-config-prettier'));
  const importPlugin = unwrap(require('eslint-plugin-import'));
  const litPlugin = unwrap(require('eslint-plugin-lit'));
  const litA11y = unwrap(require('eslint-plugin-lit-a11y'));
  const prettierPlugin = unwrap(require('eslint-plugin-prettier'));
  const wcPlugin = unwrap(require('eslint-plugin-wc'));
  const globals = unwrap(require('globals'));

  return [
    {
      ignores: ['**/vite.*.ts', '**/devops/**/*', '**/src/api/**/*', '**/*.d.ts'],
    },
    {
      files: ['**/*.ts'],
      plugins: {
        '@typescript-eslint': tsPlugin,
        import: importPlugin,
        lit: litPlugin,
        'lit-a11y': litA11y,
        wc: wcPlugin,
        'local-rules': fixupPluginRules(localRules),
        prettier: prettierPlugin,
      },
      languageOptions: {
        globals: {
          ...globals.browser,
          UmbExtensionManifest: 'readonly',
        },
        parser: tsParser,
        parserOptions: {
          project: path.resolve(tsconfigRootDir, 'tsconfig.json'),
          tsconfigRootDir,
        },
      },
      settings: {
        'import/resolver': {
          typescript: {
            project: path.resolve(tsconfigRootDir, 'tsconfig.json'),
          },
          node: true,
        },
      },
      rules: {
        ...tsPlugin.configs.recommended.rules,
        ...importPlugin.configs.recommended.rules,
        ...litPlugin.configs.recommended.rules,
        ...litA11y.configs.recommended.rules,
        ...wcPlugin.configs.recommended.rules,
        'no-var': 'error',
        'import/no-unresolved': ['error', { ignore: ['^@umbraco-cms'] }],
        'import/order': 'warn',
        'import/no-duplicates': ['warn', { 'prefer-inline': true }],
        'local-rules/bad-type-import': 'error',
        'local-rules/no-direct-api-import': 'warn',
        'local-rules/prefer-import-aliases': 'error',
        'local-rules/enforce-element-suffix-on-element-class-name': 'error',
        'local-rules/prefer-static-styles-last': 'warn',
        'local-rules/ensure-relative-import-use-js-extension': 'error',
        '@typescript-eslint/no-non-null-assertion': 'off',
        '@typescript-eslint/no-explicit-any': 'warn',
        '@typescript-eslint/no-unused-vars': 'warn',
        '@typescript-eslint/consistent-type-exports': 'error',
        '@typescript-eslint/consistent-type-imports': 'error',
        'prettier/prettier': 'warn',
      },
    },
    prettierConfig,
  ];
};
