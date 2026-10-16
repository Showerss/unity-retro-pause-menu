using System;
using FirstPerson.Interaction;
using UnityEngine;

namespace Retro.Menu.Adapters
{
    /// <summary>Saves whether a <see cref="LightSwitch"/> lamp is lit. Put it next to the lamp, with a <see cref="SaveableObject"/>.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LightSwitch), typeof(SaveableObject))]
    public sealed class LightSwitchSaveState : MonoBehaviour, ISaveableState
    {
        [Serializable]
        private class SavedState
        {
            public bool lit;
        }

        public string CaptureState() => JsonUtility.ToJson(new SavedState { lit = GetComponent<LightSwitch>().IsLit });

        public void RestoreState(string state)
        {
            SavedState saved = JsonUtility.FromJson<SavedState>(state);
            if (saved != null) GetComponent<LightSwitch>().SetLit(saved.lit);
        }
    }
}
