using System;
using System.Collections.Generic;
using UnityEngine;

namespace Retro.Menu
{
    /// <summary>
    /// Saves the state of this scene's <see cref="SaveableObject"/>s: which were destroyed, which are shown,
    /// where they stand, and their <see cref="ISaveableState"/> components. One per gameplay scene, under --- SYSTEMS ---.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldStateSaveParticipant : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        private class ComponentState
        {
            public string type;
            public string state;
        }

        [Serializable]
        private class ObjectState
        {
            public string id;
            public bool destroyed;
            public bool active = true;
            public bool hasTransform;
            public Vector3 position;
            public Quaternion rotation;
            public List<ComponentState> components = new List<ComponentState>();
        }

        [Serializable]
        private class State
        {
            public List<ObjectState> objects = new List<ObjectState>();
        }

        private readonly Dictionary<string, SaveableObject> _objects = new Dictionary<string, SaveableObject>();
        private readonly List<ISaveableState> _scratch = new List<ISaveableState>();

        public string SaveKey => "world." + gameObject.scene.name;

        private void Awake()
        {
            // Hidden objects never run Awake themselves, so gather them all from the scene up front.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (SaveableObject saveable in root.GetComponentsInChildren<SaveableObject>(true))
                {
                    if (string.IsNullOrEmpty(saveable.Id))
                    {
                        Debug.LogWarning($"[WorldStateSaveParticipant] '{saveable.name}' has no save id; it won't be saved.", saveable);
                    }
                    else if (_objects.ContainsKey(saveable.Id))
                    {
                        Debug.LogError($"[WorldStateSaveParticipant] '{saveable.name}' shares a save id with '{_objects[saveable.Id].name}'.", saveable);
                    }
                    else
                    {
                        _objects.Add(saveable.Id, saveable);
                    }
                }
            }
        }

        private void OnEnable() => MenuRegistry.Register(this);
        private void OnDisable() => MenuRegistry.Unregister(this);

        public string Capture()
        {
            var state = new State();
            foreach (KeyValuePair<string, SaveableObject> pair in _objects)
            {
                SaveableObject saveable = pair.Value;
                if (saveable == null)
                {
                    state.objects.Add(new ObjectState { id = pair.Key, destroyed = true });
                    continue;
                }

                var entry = new ObjectState { id = pair.Key, active = saveable.gameObject.activeSelf };
                if (saveable.SaveTransform)
                {
                    entry.hasTransform = true;
                    saveable.transform.GetPositionAndRotation(out entry.position, out entry.rotation);
                }

                saveable.GetComponents(_scratch);
                foreach (ISaveableState component in _scratch)
                {
                    entry.components.Add(new ComponentState { type = component.GetType().Name, state = component.CaptureState() });
                }
                state.objects.Add(entry);
            }
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            State state = JsonUtility.FromJson<State>(json);
            if (state == null) return;

            foreach (ObjectState entry in state.objects)
            {
                if (!_objects.TryGetValue(entry.id, out SaveableObject saveable) || saveable == null) continue;

                if (entry.destroyed)
                {
                    // Hide first: Destroy waits for the end of the frame, and the object shouldn't Start before then.
                    saveable.gameObject.SetActive(false);
                    Destroy(saveable.gameObject);
                    continue;
                }

                if (saveable.SaveActive) saveable.gameObject.SetActive(entry.active);
                if (saveable.SaveTransform && entry.hasTransform) saveable.transform.SetPositionAndRotation(entry.position, entry.rotation);

                saveable.GetComponents(_scratch);
                foreach (ISaveableState component in _scratch)
                {
                    string type = component.GetType().Name;
                    ComponentState saved = entry.components.Find(c => c.type == type);
                    if (saved != null) component.RestoreState(saved.state);
                }
            }
        }
    }
}
