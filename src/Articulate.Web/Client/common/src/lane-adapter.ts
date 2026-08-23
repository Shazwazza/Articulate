import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import * as PropEditor from '@umbraco-cms/backoffice/property-editor';

// v17 exports UmbPropertyValueChangeEvent; v18 does not. The class is only
// used when LANE === "v17", gated by build-time define. esbuild tree-shakes
// the unused branch from the v18 bundle.
const UmbPropertyValueChangeEvent = (
  PropEditor as {
    UmbPropertyValueChangeEvent?: new () => Event;
  }
).UmbPropertyValueChangeEvent as new () => Event;

declare const LANE: 'v17' | 'v18';

export const createThemePickerChangeEvent = (): Event =>
  LANE === 'v17' ? new UmbPropertyValueChangeEvent() : new UmbChangeEvent();
