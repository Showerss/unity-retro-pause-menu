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

        public string SaveKey => "quests";

        void Awake()
        {
            if (_quests == null) _quests = GetComponent<QuestManager>();
        }

        void OnEnable()  => MenuRegistry.Register(this);
        void OnDisable() => MenuRegistry.Unregister(this);

        public string Capture()
        {
            var state = new State();
            if (_quests == null) return JsonUtility.ToJson(state);

            state.chapterId = _quests.ActiveChapter != null ? _quests.ActiveChapter.chapterId : null;
            state.questId   = _quests.ActiveQuest   != null ? _quests.ActiveQuest.questId     : null;

            foreach (QuestDefinition quest in AllQuests())
            {
                foreach (QuestObjective objective in quest.objectives)
                {
                    if (objective.currentAmount == 0 && !objective.isCompleted) continue;
                    state.objectives.Add(new SavedObjective
                    {
                        questId     = quest.questId,
                        objectiveId = objective.id,
                        amount      = objective.currentAmount,
                        completed   = objective.isCompleted,
                    });
                }
            }

            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            if (_quests == null) return;

            State state = JsonUtility.FromJson<State>(json);
            if (state == null) return;

            // Progress lives on the quest assets, so clear everything before applying the save.
            _quests.ResetCampaignProgress();
            foreach (SavedObjective saved in state.objectives)
            {
                QuestObjective objective = FindQuest(saved.questId)?.GetObjective(saved.objectiveId);
                if (objective == null)
                {
                    Debug.LogWarning($"Save references unknown objective '{saved.questId}/{saved.objectiveId}' - skipped.", this);
                    continue;
                }
                objective.currentAmount = saved.amount;
                objective.isCompleted   = saved.completed;
            }

            ChapterDefinition chapter = FindChapter(state.chapterId);
            QuestDefinition quest = FindQuest(state.questId);
            if (chapter != null) _quests.StartChapter(chapter);
            if (quest != null) _quests.StartQuest(quest);

            // Saved the moment a quest finished, before the manager moved on: move on now instead.
            if (quest != null && quest.IsQuestComplete() && quest.autoAdvanceToNextQuest)
                _quests.AdvanceToNextQuest();
        }

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

        QuestDefinition FindQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (QuestDefinition quest in AllQuests())
                if (string.Equals(quest.questId, id, StringComparison.OrdinalIgnoreCase)) return quest;
            return null;
        }
    }
}
