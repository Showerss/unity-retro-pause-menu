namespace Retro.Menu
{
    /// <summary>
    /// A component whose state should survive saving and loading, e.g. whether a lamp is lit.
    /// Put it on an object that also has a <see cref="SaveableObject"/>; that object saves every such component on it.
    /// </summary>
    public interface ISaveableState
    {
        /// <summary>The component's state as a string (usually JSON).</summary>
        string CaptureState();

        /// <summary>Applies a string from <see cref="CaptureState"/>. Called after the scene loads, before Start.</summary>
        void RestoreState(string state);
    }
}
