using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    public static class Theme
    {
        public static readonly Color Bg = Hex("#0E1420");
        public static readonly Color Panel = Hex("#151D2B");
        public static readonly Color PanelLight = Hex("#1D2738");
        public static readonly Color PanelHover = Hex("#253248");
        public static readonly Color Border = Hex("#2A3548");
        public static readonly Color Text = Hex("#E6EDF7");
        public static readonly Color TextDim = Hex("#A4AFC2");
        public static readonly Color TextFaint = Hex("#828EA5");
        public static readonly Color Accent = Hex("#4DD0E1");
        public static readonly Color Accent2 = Hex("#7C4DFF");
        public static readonly Color Good = Hex("#3DDC97");
        public static readonly Color Warn = Hex("#FFB020");
        public static readonly Color Bad = Hex("#FF5D5D");
        public static readonly Color Gold = Hex("#FFD166");
        public static readonly Color Terminal = Hex("#0A0E14");
        public static readonly Color TerminalText = Hex("#7CFFB2");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>Dynamic TMP font assets created at runtime from the TTFs in Resources/Fonts.</summary>
    public static class UIFonts
    {
        static TMP_FontAsset _sans, _medium, _bold, _mono;
        public static TMP_FontAsset Sans => _sans ??= Create("FiraSans-Regular");
        public static TMP_FontAsset Medium => _medium ??= Create("FiraSans-Medium");
        public static TMP_FontAsset Bold => _bold ??= Create("FiraSans-Bold");
        public static TMP_FontAsset Mono => _mono ??= Create("DejaVuSansMono", false);

        const string Warm = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·•→←◆★✓✗⚠●▶◀✉⚙⏎✕…×°☎♥○♪";

        /// <summary>Adds common glyphs to the dynamic atlases up front, so the first click doesn't hitch.</summary>
        public static void Prewarm()
        {
            foreach (var f in new[] { Sans, Medium, Bold, Mono }) f.TryAddCharacters(Warm, out _);
        }

        static TMP_FontAsset Create(string file, bool monoFallback = true)
        {
            var font = Resources.Load<Font>("Fonts/" + file);
            if (font == null)
            {
                Debug.LogError("[UIFonts] missing font " + file);
                return TMP_Settings.defaultFontAsset;
            }
            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = file + " SDF";
            if (monoFallback)
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { Mono };
            return fa;
        }
    }

    /// <summary>Small helpers for building uGUI hierarchies from code.</summary>
    public static class UIKit
    {
        /// <summary>Settings → Gameplay → Reduce motion: UI animations hold still.</summary>
        public static bool ReduceMotion;

        static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
        const int SpriteRadius = 32;
        static Sprite _circle;

        /// <summary>A white rounded-rect sprite with 9-slice borders of SpriteRadius pixels.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (RoundedCache.TryGetValue(0, out var s)) return s;
                int size = SpriteRadius * 2 + 2;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, SpriteRadius, size - SpriteRadius);
                    float cy = Mathf.Clamp(y + 0.5f, SpriteRadius, size - SpriteRadius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(SpriteRadius - d + 0.5f) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                                  new Vector4(SpriteRadius, SpriteRadius, SpriteRadius, SpriteRadius));
                s.name = "Rounded";
                RoundedCache[0] = s;
                return s;
            }
        }

        static Sprite _fadeRight;

        /// <summary>Horizontal gradient: opaque on the left, transparent on the right.</summary>
        public static Sprite FadeRight
        {
            get
            {
                if (_fadeRight != null) return _fadeRight;
                const int w = 256;
                var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < w; x++)
                {
                    float t = x / (w - 1f);
                    tex.SetPixel(x, 0, new Color(1, 1, 1, 1f - t * t * (3 - 2 * t)));
                }
                tex.Apply(false, true);
                _fadeRight = Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 100);
                return _fadeRight;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
                return _circle;
            }
        }

        // ------------------------------------------------------------- rect helpers
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            return rt;
        }

        /// <summary>Insets from the anchor rect: left, bottom, right, top.</summary>
        public static RectTransform Insets(this RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float pad = 0) => rt.Anchor(0, 0, 1, 1).Insets(pad, pad, pad, pad);

        /// <summary>Absolute placement measured from the parent's top-left corner (y grows downward).</summary>
        public static RectTransform TopLeft(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Fixed-size rect centred on the parent's centre plus an offset.</summary>
        public static RectTransform Center(this RectTransform rt, float w, float h, float x = 0, float y = 0)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Height(this RectTransform rt, float h)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (!le) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            return rt;
        }

        // ------------------------------------------------------------- widgets
        public static Image Image(Transform parent, string name, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, float radius = 12, bool raycast = false)
        {
            var img = Image(parent, name, color, raycast);
            img.sprite = Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SpriteRadius / Mathf.Max(1f, radius);
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.TopLeft, TMP_FontAsset font = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font ? font : UIFonts.Sans;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.richText = true;
            return t;
        }

        public static Button Button(Transform parent, string name, Color color, Action onClick, float radius = 10)
        {
            var img = Panel(parent, name, color, radius, true);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            cb.colorMultiplier = 1.25f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            // Don't keep buttons selected: Space/Enter ship code and must not "submit" the last button clicked.
            b.onClick.AddListener(() => EventSystem.current?.SetSelectedGameObject(null));
            return b;
        }

        public static TextMeshProUGUI Label(this Button b, string text, float size, Color color, TMP_FontAsset font = null)
        {
            var t = Text(b.transform, "Label", text, size, color, TextAlignmentOptions.Center, font ?? UIFonts.Bold);
            t.rectTransform.Fill(4);
            return t;
        }

        public static VerticalLayoutGroup VList(RectTransform rt, float spacing, RectOffset padding = null, bool fitHeight = false)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (fitHeight)
            {
                var f = rt.gameObject.AddComponent<ContentSizeFitter>();
                f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return v;
        }

        /// <summary>A vertical scroll view. Returns the content transform to add children to.</summary>
        public static RectTransform ScrollList(Transform parent, string name, float spacing, out ScrollRect scroll)
        {
            var root = Rect(name, parent);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.001f); // catches scroll events
            root.gameObject.AddComponent<RectMask2D>();
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            scroll.inertia = true;
            scroll.decelerationRate = 0.08f;
            var content = Rect("Content", root);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VList(content, spacing, new RectOffset(0, 6, 0, 8), fitHeight: true);
            scroll.content = content;
            scroll.viewport = root;
            return content;
        }

        public static Image Bar(Transform parent, string name, Color back, Color fill, out Image fillImage, float radius = 6)
        {
            var bg = Panel(parent, name, back, radius);
            fillImage = Panel(bg.transform, "Fill", fill, radius);
            fillImage.rectTransform.Anchor(0, 0, 0, 1).Insets(0, 0, 0, 0);
            return bg;
        }

        public static void SetFill(Image fill, float t)
        {
            t = Mathf.Clamp01(t);
            fill.rectTransform.anchorMax = new Vector2(t, 1);
            fill.enabled = t > 0.004f;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>
        /// Anything animated every frame gets its own tiny nested canvas, so moving it doesn't force the
        /// surrounding panel (dozens of texts and images) to rebuild its batches.
        /// </summary>
        public static void IsolateAnimated(Transform t)
        {
            if (t is RectTransform rt && !rt.GetComponent<Canvas>() && rt.GetComponentInParent<Canvas>(true)) SubCanvas(rt);
        }

        /// <summary>Makes a rect its own nested canvas (with raycasting) to isolate UI rebuilds.</summary>
        public static Canvas SubCanvas(RectTransform rt)
        {
            var c = rt.GetComponent<Canvas>();
            if (!c) c = rt.gameObject.AddComponent<Canvas>();
            if (!rt.GetComponent<GraphicRaycaster>()) rt.gameObject.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>Assigns text only when it changed, so TMP doesn't regenerate its mesh for nothing.</summary>
        public static void Set(TMP_Text t, string value)
        {
            if (!string.Equals(t.text, value, StringComparison.Ordinal)) t.text = value;
        }

        public static void SetActive(Component c, bool active)
        {
            if (c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }
    }

    /// <summary>Forwards pointer events to delegates.</summary>
    public class PointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        public Action<PointerEventData> Enter, Exit, Down, Click;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke(e);
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke(e);
        public void OnPointerDown(PointerEventData e) => Down?.Invoke(e);
        public void OnPointerClick(PointerEventData e) => Click?.Invoke(e);

        public static PointerRelay On(Component c)
        {
            var r = c.GetComponent<PointerRelay>();
            return r ? r : c.gameObject.AddComponent<PointerRelay>();
        }
    }

    /// <summary>Squash-and-stretch on demand.</summary>
    public class Punch : MonoBehaviour
    {
        float _t = 1f, _amount;
        Vector3 _base = Vector3.one;

        void Awake() => _base = transform.localScale;

        public void Play(float amount = 0.08f)
        {
            if (UIKit.ReduceMotion) return;
            _amount = amount;
            _t = 0f;
            UIKit.IsolateAnimated(transform);
        }

        void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime * 5f);
            float s = _t >= 1f ? 1f : 1f - _amount * Mathf.Sin(_t * Mathf.PI) * (1f - _t * 0.5f);
            transform.localScale = _base * s;
        }
    }

    /// <summary>Gentle looping scale/alpha pulse, e.g. for things that want attention.</summary>
    public class Pulse : MonoBehaviour
    {
        public float Speed = 3f, ScaleAmount = 0.04f;
        public Graphic Glow;
        Vector3 _base;

        void Awake()
        {
            _base = transform.localScale;
            UIKit.IsolateAnimated(transform);
        }
        void OnDisable() => transform.localScale = _base;

        void Update()
        {
            if (UIKit.ReduceMotion)
            {
                if (transform.localScale != _base) transform.localScale = _base;
                return;
            }
            float s = Mathf.Sin(Time.unscaledTime * Speed);
            transform.localScale = _base * (1f + s * ScaleAmount);
            if (Glow) Glow.color = Glow.color.WithAlpha(0.35f + 0.35f * (s * 0.5f + 0.5f));
        }
    }

    /// <summary>"+123" text that floats up and fades out, then destroys itself.</summary>
    public class FloatingText : MonoBehaviour
    {
        public float Life = 1.1f, Rise = 90f;
        /// <summary>When set, called instead of destroying (for pooling).</summary>
        public Action<FloatingText> Finished;
        float _t;
        CanvasGroup _group;
        Vector2 _start;
        float _drift;

        // Fade through a CanvasGroup: changing a TMP colour/alpha regenerates the whole text mesh.
        void Awake()
        {
            UIKit.IsolateAnimated(transform);
            _group = GetComponent<CanvasGroup>();
            if (!_group) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        public void Restart(Vector2 start)
        {
            _t = 0;
            _start = start;
            _drift = UnityEngine.Random.Range(-25f, 25f);
            if (_group) _group.alpha = 1f;
        }

        void Start()
        {
            if (_t == 0) Restart(((RectTransform)transform).anchoredPosition);
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = _t / Life;
            // with reduced motion the number fades where it appeared
            if (!UIKit.ReduceMotion)
                ((RectTransform)transform).anchoredPosition = _start + new Vector2(_drift * k, Rise * (1 - (1 - k) * (1 - k)));
            _group.alpha = 1f - k * k;
            if (_t < Life) return;
            if (Finished != null) Finished(this);
            else Destroy(gameObject);
        }
    }
}
