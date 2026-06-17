"use strict";

import badTypeImportRule from './devops/eslint/rules/bad-type-import.js';
import enforceElementSuffixOnElementClassNameRule from './devops/eslint/rules/enforce-element-suffix-on-element-class-name.js';
import ensureRelativeImportUseJsExtensionRule from './devops/eslint/rules/ensure-relative-import-use-js-extension.js';
import noDirectApiImportRule from './devops/eslint/rules/no-direct-api-import.js';
import preferImportAliasesRule from './devops/eslint/rules/prefer-import-aliases.js';
import preferStaticStylesLastRule from './devops/eslint/rules/prefer-static-styles-last.js';

export default {
  rules: {
  'bad-type-import': badTypeImportRule,
  'enforce-element-suffix-on-element-class-name': enforceElementSuffixOnElementClassNameRule,
  'ensure-relative-import-use-js-extension': ensureRelativeImportUseJsExtensionRule,
  'no-direct-api-import': noDirectApiImportRule,
  'prefer-import-aliases': preferImportAliasesRule,
  'prefer-static-styles-last': preferStaticStylesLastRule,
  }
};