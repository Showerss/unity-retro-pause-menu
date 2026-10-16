using Retro.QuestSystem.Events;
using UnityEngine;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Connects scripted sequences to <see cref="GameSaveService"/> whenever both packages are installed:
    /// no sequence starts while a save loads, autosaves wait until no sequence is playing,
    /// and flag triggers re-check once a loaded save has been applied.
    /// </summary>
    internal static class StorySaveHooks
    {
        // After SubsystemRegistration, where both sides clear their static hooks.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            GameSequence.BlockStart = () => GameSaveService.IsRestoring;
            GameSaveService.AddAutosaveBlocker(() => GameSequence.PlayingCount > 0);
            GameSaveService.Restored += SequenceFlagTrigger.CheckAll;
        }
    }
}
