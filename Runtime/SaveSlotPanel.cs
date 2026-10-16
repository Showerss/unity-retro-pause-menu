using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Retro.Menu
{
    /// <summary>
    /// Drives the three slot buttons on either the Save or the Load panel, refreshing
    /// their labels from disk each time the panel opens.
    /// </summary>
    public class SaveSlotPanel : MonoBehaviour
    {
        public enum Mode { Save, Load }

        [SerializeField] private Mode _mode = Mode.Save;
        [SerializeField] private PauseMenuController _menu;
        [SerializeField] private Button[] _slotButtons = new Button[SaveSystem.SlotCount];

        [Tooltip("Optional. Loads the autosave on the Load panel; hidden on the Save panel, since only the game writes autosaves.")]
        [SerializeField] private Button _autosaveButton;

        void OnEnable() => Refresh();

        public void Refresh()
        {
            RefreshAutosave();

            for (int i = 0; i < _slotButtons.Length; i++)
            {
                Button button = _slotButtons[i];
                if (button == null) continue;

                int slot = i;
                SaveData data = SaveSystem.Peek(slot);
                bool exists = data != null;

                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = exists
                        ? $"{slot + 1}.  {data.DisplayLabel}"
                        : $"{slot + 1}.  [ empty ]";
                }

                // Loading an empty slot does nothing useful; saving over one is fine.
                button.interactable = _mode == Mode.Save || exists;

                button.onClick.RemoveAllListeners();
                if (_mode == Mode.Save)
                    button.onClick.AddListener(() => { _menu.SaveToSlot(slot); Refresh(); });
                else
                    button.onClick.AddListener(() => _menu.LoadFromSlot(slot));
            }
        }

        void RefreshAutosave()
        {
            if (_autosaveButton == null) return;

            _autosaveButton.gameObject.SetActive(_mode == Mode.Load);
            if (_mode != Mode.Load) return;

            SaveData data = SaveSystem.Peek(SaveSystem.AutosaveSlot);
            var label = _autosaveButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = data != null ? $"Autosave  -  {data.DisplayLabel}" : "Autosave  [ none ]";

            _autosaveButton.interactable = data != null;
            _autosaveButton.onClick.RemoveAllListeners();
            _autosaveButton.onClick.AddListener(() => _menu.LoadFromSlot(SaveSystem.AutosaveSlot));
        }
    }
}
