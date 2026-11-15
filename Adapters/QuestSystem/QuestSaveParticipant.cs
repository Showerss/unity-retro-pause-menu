using System;
using System.Collections.Generic;
using UnityEngine;
using Retro.QuestSystem;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Persists quest progress into the pause menu's save file: the active chapter and quest,
    /// and every objective's progress across the campaign.
    ///
    /// Excluded from compilation unless com.retro.questsystem is present.
    ///
    /// Put this on the same object as your QuestManager.
    /// </summary>
    public class QuestSaveParticipant : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        class SavedObjective
        {
            public string questId;
            public string objectiveId;
            public int amount;
            public bool completed;
        }

        [Serializable]
        class State
        {
            public string chapterId;
            public string questId;
            public List<SavedObjective> objectives = new List<SavedObjective>();
        }

        [Tooltip("Optional. Found on this object if left empty.")]
        [SerializeField] private QuestManager _quests;

        [Tooltip("Authored quest definitions resolved by questId on load (for quests started outside a campaign chapter, e.g. from NPC dialogue).")]
        [SerializeField] private List<QuestDefinition> _questDefinitions = new List<QuestDefinition>();

        private QuestDefinition _restoredClone;

        public string SaveKey => "quests";

        /// <summary>Registers an authored quest asset so its <see cref="QuestDefinition.questId"/> can be resolved on load.</summary>
        public void RegisterQuest(QuestDefinition quest)
        {
            if (quest != null && !_questDefinitions.Contains(quest))
                _questDefinitions.Add(quest);
        }

        void Awake()
        {
            if (_quests == null) _quests = GetComponent<QuestManager>();
        }

        void OnEnable()  => MenuRegistry.Register(this);
        void OnDisable() => MenuRegistry.Unregister(this);

        void OnDestroy() => DestroyRestoredClone();

        public string Capture()
        {
            var state = new State();
            if (_quests == null) _quests = GetComponent<QuestManager>();
            if (_quests == null) return JsonUtility.ToJson(state);

            state.chapterId = _quests.ActiveChapter != null ? _quests.ActiveChapter.chapterId : null;
            state.questId   = _quests.ActiveQuest   != null ? _quests.ActiveQuest.questId     : null;

            // ActiveQuest is typically a runtime clone ("<name> (Runtime)") holding the live objective state.
            if (_quests.ActiveQuest != null)
            {
                CaptureQuestObjectives(_quests.ActiveQuest, state.objectives);
            }

            foreach (QuestDefinition quest in AllQuests())
            {
                if (_quests.ActiveQuest != null &&
                    string.Equals(quest.questId, _quests.ActiveQuest.questId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                CaptureQuestObjectives(quest, state.objectives);
            }

            return JsonUtility.ToJson(state);
        }

        static void CaptureQuestObjectives(QuestDefinition quest, List<SavedObjective> destination)
        {
            if (quest == null || quest.objectives == null) return;
            foreach (QuestObjective objective in quest.objectives)
            {
                if (objective == null) continue;
                if (objective.currentAmount == 0 && !objective.isCompleted) continue;
                destination.Add(new SavedObjective
                {
                    questId     = quest.questId,
                    objectiveId = objective.id,
                    amount      = objective.currentAmount,
                    completed   = objective.isCompleted,
                });
            }
        }

        public void Restore(string json)
        {
            if (_quests == null) _quests = GetComponent<QuestManager>();
            if (_quests == null) return;

            State state = JsonUtility.FromJson<State>(json);
            if (state == null) return;

            _quests.ResetCampaignProgress();

            ChapterDefinition chapter = FindChapter(state.chapterId);
            if (chapter != null) _quests.StartChapter(chapter);

            QuestDefinition activeClone = null;
            if (!string.IsNullOrEmpty(state.questId))
            {
                QuestDefinition authored = FindQuest(state.questId);
                if (authored == null)
                {
                    Debug.LogWarning($"Save references unknown quest '{state.questId}' - skipped.", this);
                }
                else
                {
                    DestroyRestoredClone();
                    activeClone = Instantiate(authored);
                    activeClone.name = authored.name + " (Runtime)";
                    activeClone.ResetAllObjectives();
                    _restoredClone = activeClone;
                }
            }

            foreach (SavedObjective saved in state.objectives)
            {
                QuestDefinition targetQuest = (activeClone != null && string.Equals(saved.questId, state.questId, StringComparison.OrdinalIgnoreCase))
                    ? activeClone
                    : FindCampaignQuest(saved.questId);

                QuestObjective objective = targetQuest?.GetObjective(saved.objectiveId);
                if (objective == null)
                {
                    Debug.LogWarning($"Save references unknown objective '{saved.questId}/{saved.objectiveId}' - skipped.", this);
                    continue;
                }
                objective.currentAmount = saved.amount;
                objective.isCompleted   = saved.completed;
            }

            if (activeClone != null)
            {
                _quests.StartQuest(activeClone, replaceActive: true);

                // Saved the moment a quest finished, before the manager moved on: move on now instead.
                if (activeClone.IsQuestComplete() && activeClone.autoAdvanceToNextQuest)
                    _quests.AdvanceToNextQuest();
            }
        }

        void DestroyRestoredClone()
        {
            if (_restoredClone == null) return;
            if (Application.isPlaying) Destroy(_restoredClone);
            else DestroyImmediate(_restoredClone);
            _restoredClone = null;
        }

        static bool IsRuntimeClone(QuestDefinition quest) =>
            quest != null &&
            (quest.name.EndsWith("(Runtime)", StringComparison.Ordinal) ||
             quest.name.EndsWith("(Sandbox Runtime)", StringComparison.Ordinal));

        IEnumerable<ChapterDefinition> AllChapters()
        {
            if (_quests.Campaign != null)
            {
                foreach (ChapterDefinition chapter in _quests.Campaign.chapters)
                    if (chapter != null) yield return chapter;
            }
            else if (_quests.StartingChapter != null)
            {
                yield return _quests.StartingChapter;
            }
        }

        IEnumerable<QuestDefinition> AllQuests()
        {
            foreach (ChapterDefinition chapter in AllChapters())
                foreach (QuestDefinition quest in chapter.quests)
                    if (quest != null) yield return quest;
        }

        ChapterDefinition FindChapter(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (ChapterDefinition chapter in AllChapters())
                if (string.Equals(chapter.chapterId, id, StringComparison.OrdinalIgnoreCase)) return chapter;
            return null;
        }

        QuestDefinition FindCampaignQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (QuestDefinition quest in AllQuests())
                if (!IsRuntimeClone(quest) && string.Equals(quest.questId, id, StringComparison.OrdinalIgnoreCase))
                    return quest;
            return null;
        }

        QuestDefinition FindQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_questDefinitions != null)
            {
                foreach (QuestDefinition quest in _questDefinitions)
                {
                    if (quest != null && !IsRuntimeClone(quest) &&
                        string.Equals(quest.questId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return quest;
                    }
                }
            }

            QuestDefinition campaignQuest = FindCampaignQuest(id);
            if (campaignQuest != null) return campaignQuest;

            foreach (QuestDefinition quest in Resources.FindObjectsOfTypeAll<QuestDefinition>())
            {
                if (quest != null && quest != _quests.ActiveQuest && !IsRuntimeClone(quest) &&
                    string.Equals(quest.questId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return quest;
                }
            }

            return null;
        }
    }
}
