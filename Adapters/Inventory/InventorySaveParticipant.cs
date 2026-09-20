using System;
using System.Collections.Generic;
using UnityEngine;
using Retro.Inventory;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Persists inventory contents into the pause menu's save file.
    ///
    /// Excluded from compilation unless com.retro.inventory is present, so the core
    /// menu package still imports into a project with no inventory.
    ///
    /// Put this on the same object as your InventorySystem.
    /// </summary>
    public class InventorySaveParticipant : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        class SavedStack
        {
            public string itemId;
            public int quantity;
        }

        [Serializable]
        class State
        {
            public int selectedSlot;
            public List<SavedStack> stacks = new List<SavedStack>();
        }

        [SerializeField] private InventorySystem _inventory;

        [Tooltip("Required to turn saved item ids back into ItemDefinition assets.")]
        [SerializeField] private ItemDatabase _database;

        public string SaveKey => "inventory";

        void Awake()
        {
            if (_inventory == null) _inventory = GetComponent<InventorySystem>();
        }

        void OnEnable()  => MenuRegistry.Register(this);
        void OnDisable() => MenuRegistry.Unregister(this);

        public string Capture()
        {
            var state = new State();
            if (_inventory == null) return JsonUtility.ToJson(state);

            state.selectedSlot = _inventory.SelectedSlotIndex;
            foreach (ItemStack stack in _inventory.Slots)
            {
                if (stack == null || stack.IsEmpty) continue;
                state.stacks.Add(new SavedStack
                {
                    itemId   = stack.Item.Id,
                    quantity = stack.Quantity,
                });
            }

            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            if (_inventory == null) return;

            if (_database == null)
            {
                Debug.LogError($"{nameof(InventorySaveParticipant)}: no ItemDatabase assigned, " +
                               "inventory cannot be restored.", this);
                return;
            }

            State state = JsonUtility.FromJson<State>(json);
            if (state == null) return;

            // Clear through the public API. Snapshot first - removing mutates Slots.
            var toRemove = new List<KeyValuePair<string, int>>();
            foreach (ItemStack stack in _inventory.Slots)
            {
                if (stack == null || stack.IsEmpty) continue;
                toRemove.Add(new KeyValuePair<string, int>(stack.Item.Id, stack.Quantity));
            }
            foreach (var pair in toRemove)
                _inventory.RemoveItemById(pair.Key, pair.Value);

            foreach (SavedStack saved in state.stacks)
            {
                ItemDefinition definition = _database.Resolve(saved.itemId);
                if (definition == null)
                {
                    Debug.LogWarning($"Save references unknown item id '{saved.itemId}' - skipped. " +
                                     "Rebuild the Item Database if you added items since saving.", this);
                    continue;
                }
                _inventory.AddItem(definition, saved.quantity, playFeedback: false);
            }

            _inventory.SelectSlot(state.selectedSlot);
        }
    }
}
