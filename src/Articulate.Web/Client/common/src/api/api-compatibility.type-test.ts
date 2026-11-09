import { BlogMlService } from './sdk.gen.js';

// The v17 schema describes these responses as Blob | File. The v18 schema
// describes them as FileContentResult. The shared client must promise neither;
// callers narrow the runtime value to Blob before downloading it.
type DataOf<T> = T extends { data: infer Data } ? Data : never;
type IsExactlyUnknown<T> = unknown extends T ? ([keyof T] extends [never] ? true : false) : false;
type Assert<T extends true> = T;

type BlogMlExportData = DataOf<Awaited<ReturnType<typeof BlogMlService.postBlogmlExport>>>;
type DisqusExportData = DataOf<Awaited<ReturnType<typeof BlogMlService.getBlogmlExportDisqus>>>;

const blogMlExportDataMustStayUnknown: Assert<IsExactlyUnknown<BlogMlExportData>> = true;
const disqusExportDataMustStayUnknown: Assert<IsExactlyUnknown<DisqusExportData>> = true;

void blogMlExportDataMustStayUnknown;
void disqusExportDataMustStayUnknown;
