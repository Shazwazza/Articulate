namespace Articulate.Routing
{
    public interface IArticulateRouteRefreshState
    {
        public long CurrentVersion { get; }
        public long MarkDirty();
    }
}
