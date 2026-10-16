using System;
using System.Collections.Generic;
using Retro.QuestSystem.Events;
using UnityEngine;

namespace Retro.Menu.Adapters
{
    /// <summary>Saves every story flag. Flags are shared by all scenes, so this lives under GameBootstrap in Boot.</summary>
    [DisallowMultipleComponent]
    public sealed class StoryFlagsSaveParticipant : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        private class Entry
        {
            public string id;
            public int value;
        }

        [Serializable]
        private class State
        {
            public List<Entry> flags = new List<Entry>();
        }

        public string SaveKey => "story.flags";

        private void OnEnable() => MenuRegistry.Register(this);
        private void OnDisable() => MenuRegistry.Unregister(this);

        public string Capture()
        {
            var state = new State();
            foreach (KeyValuePair<string, int> pair in GameFlags.Snapshot())
            {
                state.flags.Add(new Entry { id = pair.Key, value = pair.Value });
            }
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            State state = JsonUtility.FromJson<State>(json);
            if (state == null) return;

            var values = new Dictionary<string, int>();
            foreach (Entry entry in state.flags)
            {
                if (!string.IsNullOrEmpty(entry.id)) values[entry.id] = entry.value;
            }
            GameFlags.Restore(values);
        }
    }
}
