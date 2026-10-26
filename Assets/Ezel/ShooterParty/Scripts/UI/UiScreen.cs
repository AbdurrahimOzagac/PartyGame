using UnityEngine;

namespace ShooterParty.UI
{
    // A full screen or a modal on the UI root canvas. Shows and hides with a 0.25 s fade (and a small rise),
    // and only takes clicks while shown.
    [RequireComponent(typeof(CanvasGroup))]
    public class UiScreen : MonoBehaviour
    {
        public const float FadeSeconds = 0.25f;
        public bool IsShown { get; private set; }

        CanvasGroup group;
        RectTransform content;
        float t;

        protected virtual void Awake()
        {
            group = GetComponent<CanvasGroup>();
            content = transform.childCount > 0 ? transform.GetChild(transform.childCount - 1) as RectTransform : null;
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            gameObject.SetActive(true);
        }

        public virtual void Show()
        {
            if (IsShown) return;
            IsShown = true;
            group.interactable = true; group.blocksRaycasts = true;
            OnShown();
        }

        public virtual void Hide()
        {
            if (!IsShown) return;
            IsShown = false;
            group.interactable = false; group.blocksRaycasts = false;
        }

        protected virtual void OnShown() { }

        protected virtual void Update()
        {
            float target = IsShown ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / FadeSeconds);
            float e = Ease.OutCubic(t);
            group.alpha = e;
            if (content) content.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, e);
        }
    }
}
