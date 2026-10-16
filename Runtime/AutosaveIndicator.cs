using System.Collections;
using UnityEngine;

namespace Retro.Menu
{
    /// <summary>Briefly shows a "Saving..." note in a corner after each autosave.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class AutosaveIndicator : MonoBehaviour
    {
        [Tooltip("How long the note stays fully visible, in seconds.")]
        [Min(0f)]
        [SerializeField] private float _holdSeconds = 1.5f;

        [Tooltip("How long it takes to fade out, in seconds.")]
        [Min(0f)]
        [SerializeField] private float _fadeSeconds = 0.6f;

        private CanvasGroup _group;
        private Coroutine _showing;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void OnEnable() => GameSaveService.Autosaved += HandleAutosaved;
        private void OnDisable() => GameSaveService.Autosaved -= HandleAutosaved;

        private void HandleAutosaved()
        {
            if (_showing != null) StopCoroutine(_showing);
            _showing = StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            _group.alpha = 1f;
            yield return new WaitForSecondsRealtime(_holdSeconds);
            for (float t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - t / _fadeSeconds;
                yield return null;
            }
            _group.alpha = 0f;
            _showing = null;
        }
    }
}
