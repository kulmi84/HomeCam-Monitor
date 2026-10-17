#if V2_BETA
using System.Runtime.CompilerServices;
namespace HomeCamMonitor;

internal static class CameraGridUpdate
{
    private sealed class State { internal int Depth; }
    private static readonly ConditionalWeakTable<DataGridView, State> States = new();
    internal static bool IsActive(DataGridView grid) => States.TryGetValue(grid, out var state) && state.Depth > 0;
    internal static IDisposable Begin(DataGridView grid) => new Scope(States.GetOrCreateValue(grid));
    private sealed class Scope : IDisposable
    {
        private State? state;
        internal Scope(State state) { this.state = state; state.Depth++; }
        public void Dispose() { if (state is { } current) { current.Depth--; state = null; } }
    }
}
#endif
