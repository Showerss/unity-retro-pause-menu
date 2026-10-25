using System;
using UnityEngine;
using Retro.Inventory;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Persists the player's Wallet into the pause menu's save file. Put it next to the Wallet.
    /// </summary>
    public class WalletSaveParticipant : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        class State
        {
            public int amount;
        }

        [SerializeField] private Wallet _wallet;

        [Tooltip("Unique key in the save file.")]
        [SerializeField] private string _saveKey = "wallet";

        public string SaveKey => _saveKey;

        void Awake()
        {
            if (_wallet == null) _wallet = GetComponent<Wallet>();
        }

        void OnEnable()  => MenuRegistry.Register(this);
        void OnDisable() => MenuRegistry.Unregister(this);

        public string Capture() => JsonUtility.ToJson(new State { amount = _wallet != null ? _wallet.Amount : 0 });

        public void Restore(string json)
        {
            State state = JsonUtility.FromJson<State>(json);
            if (_wallet != null && state != null) _wallet.Set(state.amount);
        }
    }
}
