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

        void OnEnable() => Refresh();

        public void Refresh()
        {
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
    }
}
