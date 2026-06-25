import { fixupPluginRules } from "@eslint/compat";
import tsPlugin from '@typescript-eslint/eslint-plugin';
import tsParser from '@typescript-eslint/parser';
import prettierConfig from 'eslint-config-prettier';
import importPlugin from 'eslint-plugin-import';
import litPlugin from 'eslint-plugin-lit';
import litA11y from 'eslint-plugin-lit-a11y';
import prettierPlugin from 'eslint-plugin-prettier';
import wcPlugin from 'eslint-plugin-wc';
import globals from 'globals';
import localRules from './eslint-local-rules.js';

export default [
	{
		ignores: ['**/vite.*.ts', 'devops/**/*', 'src/packages/**/*', 'src/api/**/*'],
	},
	{
		files: ['**/*.ts'],
		plugins: {
			'@typescript-eslint': tsPlugin,
			import: importPlugin,
			lit: litPlugin,
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
				project: true,
				tsconfigRootDir: import.meta.dirname,
			},
		},
		settings: {
			'import/resolver': {
				typescript: true,
				node: true,
			},
		},
		rules: {
			...tsPlugin.configs.recommended.rules,
			...importPlugin.configs.recommended.rules,
			...litPlugin.configs.recommended.rules,
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
	{
		files: ['**/*.ts'],
		plugins: { 'lit-a11y': litA11y },
		rules: litA11y.configs.recommended.rules,
	},
	prettierConfig,
];
