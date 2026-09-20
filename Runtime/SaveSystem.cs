using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Retro.Menu
{
    [Serializable]
    public class SaveEntry
    {
        public string key;
        public string json;
    }

    [Serializable]
    public class SaveData
    {
        public int formatVersion = 1;
        public string savedAtUtc;
        public string sceneName;
        public List<SaveEntry> entries = new List<SaveEntry>();

        public string Find(string key)
        {
            foreach (SaveEntry e in entries)
                if (e.key == key) return e.json;
            return null;
        }

        public string DisplayLabel
        {
            get
            {
                if (!DateTime.TryParse(savedAtUtc, out DateTime parsed)) return sceneName;
                return $"{sceneName}  -  {parsed.ToLocalTime():yyyy-MM-dd HH:mm}";
            }
        }
    }

    /// <summary>
    /// Slot-based JSON saves in Application.persistentDataPath.
    ///
    /// The file is a bag of keyed blobs, one per <see cref="ISaveParticipant"/>. Nothing
    /// here knows what a player or an inventory is - that keeps the package importable
    /// and means adding a new saved system is a new participant, not a schema edit.
    ///
    /// Forward/backward compatibility: unknown keys on load are ignored, and missing
    /// keys simply leave that system untouched. So an old save loads into a newer build
    /// and a new save loads into an older one, both degrading rather than failing.
    ///
    /// Scope: participants decide what they persist. World state - opened doors, already
    /// collected pickups - is NOT covered unless you write a participant for it.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        static string PathForSlot(int slot) =>
            Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        public static bool SlotExists(int slot) => File.Exists(PathForSlot(slot));

        public static SaveData Peek(int slot)
        {
            if (!SlotExists(slot)) return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathForSlot(slot)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save slot {slot} is unreadable: {e.Message}");
                return null;
            }
        }

        public static bool Save(int slot)
        {
            var data = new SaveData
            {
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                sceneName  = SceneManager.GetActiveScene().name,
            };

            foreach (ISaveParticipant participant in MenuRegistry.SaveParticipants)
            {
                try
                {
                    data.entries.Add(new SaveEntry
                    {
                        key  = participant.SaveKey,
                        json = participant.Capture(),
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"Save participant '{participant.SaveKey}' threw while capturing: {e}");
                }
            }

            if (data.entries.Count == 0)
                Debug.LogWarning("Saving with no registered participants - the file will be empty. " +
                                 "Add a PlayerTransformSaveParticipant to your player, at minimum.");

            try
            {
                File.WriteAllText(PathForSlot(slot), JsonUtility.ToJson(data, true));
                Debug.Log($"Saved slot {slot} -> {PathForSlot(slot)}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem.Save failed: {e.Message}");
                return false;
            }
        }

        public static bool Load(int slot)
        {
            SaveData data = Peek(slot);
            if (data == null) return false;

            foreach (ISaveParticipant participant in MenuRegistry.SaveParticipants)
            {
                string json = data.Find(participant.SaveKey);
                if (json == null) continue; // not in this save; leave the system alone

                try { participant.Restore(json); }
                catch (Exception e)
                {
                    Debug.LogError($"Save participant '{participant.SaveKey}' threw while restoring: {e}");
                }
            }

            return true;
        }

        public static void Delete(int slot)
        {
            if (SlotExists(slot)) File.Delete(PathForSlot(slot));
        }
    }
}
