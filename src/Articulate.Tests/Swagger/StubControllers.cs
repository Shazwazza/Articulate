#nullable enable
#pragma warning disable SA1649 // File name should match first type name — this file intentionally holds two stub types in different namespaces.
// Stub controllers in the namespaces the handler matches against, so the real
// ControllerTypeInfo.Namespace.StartsWith(...) check is exercised.

namespace Articulate.Controllers.Api
{
    internal sealed class StubArticulateController
    {
        public void SampleAction() { }
    }
}

namespace Some.Other.Api.Controllers
{
    internal sealed class StubUnrelatedController
    {
        public void SampleAction() { }
    }
}
#pragma warning restore SA1649
