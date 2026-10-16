using System;
using Retro.QuestSystem.Events;
using UnityEngine;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Saves whether a <see cref="GameSequence"/> has finished, so one-time events stay done after loading.
    /// Put it next to the sequence, with a <see cref="SaveableObject"/>. A save made mid-sequence plays it again from the top.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameSequence), typeof(SaveableObject))]
    public sealed class SequenceSaveState : MonoBehaviour, ISaveableState
    {
        [Serializable]
        private class SavedState
        {
            public bool finished;
        }

        private GameSequence _sequence;

        private void Awake() => _sequence = GetComponent<GameSequence>();

        public string CaptureState() => JsonUtility.ToJson(new SavedState { finished = Sequence.HasFinished });

        public void RestoreState(string state)
        {
            SavedState saved = JsonUtility.FromJson<SavedState>(state);
            if (saved != null) Sequence.RestoreFinished(saved.finished);
        }

        // Restore can run before this object's Awake when it starts hidden.
        private GameSequence Sequence => _sequence != null ? _sequence : _sequence = GetComponent<GameSequence>();
    }
}
