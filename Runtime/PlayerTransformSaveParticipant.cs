using System;
using UnityEngine;

namespace Retro.Menu
{
    /// <summary>
    /// Saves and restores where the player is standing and which way they face.
    /// Lives in the core package because Transform and CharacterController are engine
    /// types - no game-specific dependency needed.
    ///
    /// Drop this on your player root.
    /// </summary>
    public class PlayerTransformSaveParticipant : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        class State
        {
            public Vector3 position;
            public float yaw;
        }

        public string SaveKey => "player.transform";

        void OnEnable()  => MenuRegistry.Register(this);
        void OnDisable() => MenuRegistry.Unregister(this);

        public string Capture()
        {
            return JsonUtility.ToJson(new State
            {
                position = transform.position,
                yaw      = transform.eulerAngles.y,
            });
        }

        public void Restore(string json)
        {
            State state = JsonUtility.FromJson<State>(json);
            if (state == null) return;

            // A CharacterController overwrites direct transform writes, so it has to be
            // off for the teleport to stick.
            var cc = GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (wasEnabled) cc.enabled = false;

            transform.position = state.position;
            transform.rotation = Quaternion.Euler(0f, state.yaw, 0f);

            if (wasEnabled) cc.enabled = true;

            // Pitch is deliberately not restored: it is owned by whatever look script
            // drives the camera, and would be overwritten on the next frame anyway.
        }
    }
}
