using Retro.QuestSystem;
using UnityEngine;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Asks the <see cref="GameSaveService"/> for an autosave when quest objectives, quests or chapters are completed.
    /// Put it next to the QuestManager. Starting a quest never autosaves, so a fresh launch doesn't overwrite the autosave.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestAutosave : MonoBehaviour
    {
        [Tooltip("The game's quest manager.")]
        [SerializeField] private QuestManager _quests;

        [Tooltip("Autosave after each objective is completed.")]
        [SerializeField] private bool _onObjectiveCompleted = true;

        [Tooltip("Autosave after each quest is completed.")]
        [SerializeField] private bool _onQuestCompleted = true;

        [Tooltip("Autosave after each chapter is completed.")]
        [SerializeField] private bool _onChapterCompleted = true;

        private void Awake()
        {
            if (_quests == null) Debug.LogError($"[QuestAutosave] '{name}' has no QuestManager.", this);
        }

        private void OnEnable()
        {
            if (_quests == null) return;
            _quests.OnObjectiveCompleted += HandleObjectiveCompleted;
            _quests.OnQuestCompleted += HandleQuestCompleted;
            _quests.OnChapterCompleted += HandleChapterCompleted;
        }

        private void OnDisable()
        {
            if (_quests == null) return;
            _quests.OnObjectiveCompleted -= HandleObjectiveCompleted;
            _quests.OnQuestCompleted -= HandleQuestCompleted;
            _quests.OnChapterCompleted -= HandleChapterCompleted;
        }

        private void HandleObjectiveCompleted(QuestObjective objective)
        {
            if (_onObjectiveCompleted) GameSaveService.RequestAutosave();
        }

        private void HandleQuestCompleted(QuestDefinition quest)
        {
            if (_onQuestCompleted) GameSaveService.RequestAutosave();
        }

        private void HandleChapterCompleted(ChapterDefinition chapter)
        {
            if (_onChapterCompleted) GameSaveService.RequestAutosave();
        }
    }
}
