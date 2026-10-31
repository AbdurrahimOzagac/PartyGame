using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Short messages at the top: "Bea joined", "Connection lost — rejoining…". Up to three at once, 3 s each.
    public class Toasts : MonoBehaviour
    {
        public RectTransform template;
        readonly List<(RectTransform rt, float until)> live = new List<(RectTransform, float)>();

        void Awake() { if (template) template.gameObject.SetActive(false); }

        public void Show(string text)
        {
            if (!template) return;
            if (live.Count >= 3) { Destroy(live[0].rt.gameObject); live.RemoveAt(0); }
            var rt = Instantiate(template, template.parent);
            rt.gameObject.SetActive(true);
            rt.GetComponentInChildren<TMP_Text>().text = text;
            live.Add((rt, Time.unscaledTime + 3f));
        }

        void Update()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var (rt, until) = live[i];
                if (!rt) { live.RemoveAt(i); continue; }
                float left = until - Time.unscaledTime;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg) cg.alpha = Mathf.Clamp01(left / 0.3f) * Mathf.Clamp01((3f - left) / 0.15f);
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(0f, -(live.Count - 1 - i) * 78f), 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                if (left <= 0f) { Destroy(rt.gameObject); live.RemoveAt(i); }
            }
        }
    }
}
