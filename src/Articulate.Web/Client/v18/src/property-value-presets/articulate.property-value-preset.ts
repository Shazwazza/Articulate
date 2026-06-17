import type { UmbPropertyValuePreset, UmbPropertyValuePresetApiCallArgs } from '@umbraco-cms/backoffice/property';

export class ArticulateBlogPresetApi implements UmbPropertyValuePreset {
  async processValue(
    value: unknown,
    _config: unknown,
    _typeArgs: unknown,
    callArgs: UmbPropertyValuePresetApiCallArgs,
  ): Promise<unknown> {
    if (value !== undefined && value !== null && value !== '') {
      return value;
    }

    switch (callArgs.alias) {
      case 'theme':
        return 'VAPOR';
      case 'pageSize':
        return 10;
      case 'categoriesUrlName':
        return 'categories';
      case 'tagsUrlName':
        return 'tags';
      case 'searchUrlName':
        return 'search';
      case 'categoriesPageName':
        return 'Categories';
      case 'tagsPageName':
        return 'Tags';
      case 'searchPageName':
        return 'Search results';
      case 'enableComments':
        return 1;
      default:
        return value;
    }
  }

  destroy(): void {}
}

export default ArticulateBlogPresetApi;
