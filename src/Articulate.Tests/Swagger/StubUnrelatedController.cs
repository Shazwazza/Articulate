// Stub controller in a namespace the handler must not match, so the real
// ControllerTypeInfo.Namespace.StartsWith(...) check is exercised.

namespace Some.Other.Api.Controllers
{
    internal sealed class StubUnrelatedController
    {
        public void SampleAction() { }
    }
}
