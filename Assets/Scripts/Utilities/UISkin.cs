using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    /// <summary>
    /// Runtime styling helpers that make existing UI (buttons, panels, tray) look
    /// premium without touching scene layout: glossy rounded sprites, themed tints,
    /// soft glows and a press-punch component. All helpers are idempotent and
    /// null-safe so they can never break the HUD if a reference is missing.
    /// </summary>
    public static class UISkin
    {
        public static void StyleButton(Button button)
        {
            if (button == null) return;

            Image img = button.image != null ? button.image : button.GetComponent<Image>();
            if (img != null)
            {
                UISpriteFactory.ApplyRounded(img, 0.42f);
                Color tint = ChromaPalette.GetThemeButtonColor();
                tint.a = Mathf.Max(img.color.a, 0.92f);
                img.color = tint;
                AddGloss(img.rectTransform);
            }

            if (button.GetComponent<UIButtonFeedback>() == null)
            {
                button.gameObject.AddComponent<UIButtonFeedback>();
            }
        }

        // Turn an existing panel image into a glossy glass plaque.
        public static void StyleGlassPanel(Image img, Color tint, float alpha = 0.55f, float radius01 = 0.32f)
        {
            if (img == null) return;
            UISpriteFactory.ApplyRounded(img, radius01);
            tint.a = alpha;
            img.color = tint;
            AddGloss(img.rectTransform);
        }

        // Adds a soft glow behind a transform (returns the glow image so callers can recolor it).
        public static Image AddGlow(RectTransform target, Color color, float expand, string id = "__glow")
        {
            if (target == null) return null;
            Transform existing = target.parent != null ? target.parent.Find(id) : null;
            Image glow;
            if (existing != null)
            {
                glow = existing.GetComponent<Image>();
            }
            else
            {
                GameObject obj = new GameObject(id, typeof(RectTransform), typeof(Image));
                obj.transform.SetParent(target.parent != null ? target.parent : target, false);
                RectTransform rect = (RectTransform)obj.transform;
                rect.anchorMin = target.anchorMin;
                rect.anchorMax = target.anchorMax;
                rect.pivot = target.pivot;
                rect.anchoredPosition = target.anchoredPosition;
                rect.sizeDelta = target.sizeDelta + new Vector2(expand, expand);
                IgnoreLayout(obj);
                rect.SetAsFirstSibling();
                glow = obj.GetComponent<Image>();
                glow.raycastTarget = false;
                UISpriteFactory.ApplySoftCircle(glow);
            }

            if (glow != null)
            {
                glow.color = color;
            }

            return glow;
        }

        // A subtle top gloss highlight stripe over a rounded element.
        public static void AddGloss(RectTransform parent)
        {
            if (parent == null || parent.Find("__gloss") != null) return;

            GameObject obj = new GameObject("__gloss", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchorMin = new Vector2(0.08f, 0.52f);
            rect.anchorMax = new Vector2(0.92f, 0.93f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image img = obj.GetComponent<Image>();
            img.raycastTarget = false;
            UISpriteFactory.ApplyRounded(img, 0.5f);
            img.color = new Color(1f, 1f, 1f, 0.22f);
            rect.SetAsLastSibling();
        }

        public static void IgnoreLayout(GameObject obj)
        {
            if (obj == null) return;
            LayoutElement le = obj.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = obj.AddComponent<LayoutElement>();
            }

            le.ignoreLayout = true;
        }
    }
}
