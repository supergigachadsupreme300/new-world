using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CountryLife.Helpers
{
    public static class UIHelper
    {
        public static TMP_Text MakeText(string name, Transform parent, Vector2 position, string text,
            int fontSize, Color color, Vector2 size, bool wrap = false, bool ellipsis = false,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool raycastTarget = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            if (wrap) tmp.textWrappingMode = TextWrappingModes.Normal;
            if (ellipsis) tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = raycastTarget;

            return tmp;
        }

        public static Button MakeButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, Color color, UnityEngine.Events.UnityAction callback)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(callback);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tr = textGO.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }
    }
}
