using UnityEngine;

namespace Retro.Menu
{
    /// <summary>
    /// Gives an object a permanent id so the save remembers it: whether it has been destroyed (a pickup that was taken),
    /// optionally whether it is shown and where it stands, plus every <see cref="ISaveableState"/> component on it.
    /// Read by the scene's <see cref="WorldStateSaveParticipant"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveableObject : MonoBehaviour
    {
        [Tooltip("Permanent id in save files. Generated automatically; don't edit or copy it by hand.")]
        [SerializeField] private string _id;

        [Tooltip("Save whether the object is shown or hidden (e.g. something a sequence reveals).")]
        [SerializeField] private bool _saveActive;

        [Tooltip("Save its position and rotation (e.g. something a sequence moves).")]
        [SerializeField] private bool _saveTransform;

        public string Id => _id;
        public bool SaveActive => _saveActive;
        public bool SaveTransform => _saveTransform;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Prefab assets stay blank; each placed copy gets its own id.
            // Ids are only assigned while editing a loaded scene, never in play mode or mid-load.
            if (Application.isPlaying || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) || !gameObject.scene.isLoaded) return;
            if (!string.IsNullOrEmpty(_id) && IsUniqueInScene()) return;

            _id = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Duplicating an object copies its id, so check no other object in the scene has it.
        private bool IsUniqueInScene()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (SaveableObject other in root.GetComponentsInChildren<SaveableObject>(true))
                {
                    if (other != this && other._id == _id) return false;
                }
            }
            return true;
        }
#endif
    }
}
