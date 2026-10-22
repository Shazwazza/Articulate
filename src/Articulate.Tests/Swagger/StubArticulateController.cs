// Stub controller in a namespace the handler matches against, so the real
// ControllerTypeInfo.Namespace.StartsWith(...) check is exercised.

namespace Articulate.Controllers.Api
{
    internal sealed class StubArticulateController
    {
        public void SampleAction() { }
    }
}
