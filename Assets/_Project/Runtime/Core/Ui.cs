// What the HUD and the menus share (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md): the colours of the art direction, panels made in code (no asset files but the theme),
// text and boxes, and the name of the key or button an action is on, as on the device last touched: keyboard and mouse, or gamepad.
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

namespace Dad.Core
{
    public static class Ui
    {
        // The art direction: near black, charcoal, cold steel, muted silver, a restrained crimson.
        public static readonly Color Ink = Hex(0x050608), Coal = Hex(0x090B10), Steel = Hex(0x84909C), Silver = Hex(0xC3C8CE), Crimson = Hex(0x8D1824),
            White = Hex(0xEEF0F2), Glass = new Color(0.035f, 0.043f, 0.063f, 0.82f), Health = Hex(0x6FB27A), Armour = Hex(0x5E92CF), Police = Hex(0x4F86E0), Route = Hex(0xD9A441),
            Job = Hex(0xE0525C); // a job's marker (Jobs): the crimson brightened to read on the radar and at night

        public static Color Hex(int rgb, float a = 1) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        public static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>A panel drawn over the game, made here: 1920 by 1080 scaled to the screen; higher order on top.</summary>
        public static UIDocument Panel(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = name;
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/Theme");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.sortingOrder = order;
            ps.clearColor = false;
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            var root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0;
            return doc;
        }

        public static Label Text(string text, float size, Color colour, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.unityTextAlign = align;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }

        public static VisualElement Box(Color fill, float radius = 0)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.backgroundColor = fill;
            Round(e, radius);
            return e;
        }

        public static void Round(VisualElement e, float r) => e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        public static void Border(VisualElement e, float width, Color c)
        {
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
        }

        /// <summary>Places an element by its edges, in reference pixels; NaN leaves an edge free.</summary>
        public static T At<T>(T e, float left = float.NaN, float top = float.NaN, float right = float.NaN, float bottom = float.NaN, float width = float.NaN, float height = float.NaN) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            if (!float.IsNaN(left)) e.style.left = left;
            if (!float.IsNaN(top)) e.style.top = top;
            if (!float.IsNaN(right)) e.style.right = right;
            if (!float.IsNaN(bottom)) e.style.bottom = bottom;
            if (!float.IsNaN(width)) e.style.width = width;
            if (!float.IsNaN(height)) e.style.height = height;
            return e;
        }

        public static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        // ---- which device, and the names of keys and buttons

        /// <summary>The gamepad was touched last (rather than the keyboard or mouse).</summary>
        public static bool Pad { get; private set; }
        public static string Scheme => Pad ? "Gamepad" : "Keyboard&Mouse";
        static IDisposable watching;

        /// <summary>Follows which device is used: a button pressed, a stick or the mouse moved. Called every frame by the HUD.</summary>
        public static void Watch()
        {
            watching ??= InputSystem.onAnyButtonPress.Call(c => Pad = c.device is Gamepad);
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.rightStick.ReadValue().sqrMagnitude > 0.25f)) Pad = true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 9) Pad = false;
        }

        /// <summary>For tests: which device to name.</summary>
        public static void UsePad(bool pad) => Pad = pad;

        /// <summary>The key or button an action is on, for the device in use ("F", "Y", "RMB"); the first if there are several; empty if none.</summary>
        public static string Key(InputAction action, bool? pad = null)
        {
            if (action == null) return "";
            string scheme = (pad ?? Pad) ? "Gamepad" : "Keyboard&Mouse";
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isPartOfComposite) continue;
                bool ours = b.isComposite ? PartIn(action, i, scheme) : b.groups != null && b.groups.Contains(scheme);
                if (!ours) continue;
                var text = action.GetBindingDisplayString(i);
                return Short(text);
            }
            return "";
        }

        static bool PartIn(InputAction a, int composite, string scheme)
        {
            for (int i = composite + 1; i < a.bindings.Count && a.bindings[i].isPartOfComposite; i++)
                if (a.bindings[i].groups != null && a.bindings[i].groups.Contains(scheme)) return true;
            return false;
        }

        /// <summary>The name a binding is shown by, for one binding of an action.</summary>
        public static string KeyAt(InputAction action, int binding) => binding < 0 || binding >= action.bindings.Count ? "" : Short(action.GetBindingDisplayString(binding));

        static string Short(string s)
        {
            switch (s)
            {
                case "Button South": return "A";
                case "Button East": return "B";
                case "Button West": return "X";
                case "Button North": return "Y";
                case "Left Shoulder": return "LB";
                case "Right Shoulder": return "RB";
                case "Left Trigger": return "LT";
                case "Right Trigger": return "RT";
                case "Left Stick Press": return "LS";
                case "Right Stick Press": return "RS";
                case "Start": return "Menu";
                case "Select": return "View";
                case "Left Button": return "LMB";
                case "Right Button": return "RMB";
                case "Middle Button": return "MMB";
                default: return s.Replace("D-Pad/", "D-pad ").Replace("Left Stick", "LS").Replace("Right Stick", "RS");
            }
        }

        /// <summary>A key cap: the key's name in a small box, as prompts show it.</summary>
        public static VisualElement Cap(string key, float size = 18)
        {
            var cap = Box(Fade(White, 0.92f), 5);
            cap.style.paddingLeft = cap.style.paddingRight = 7;
            cap.style.paddingTop = cap.style.paddingBottom = 2;
            cap.style.marginRight = 8;
            cap.style.minWidth = size + 8;
            cap.style.alignItems = Align.Center;
            var t = Text(key, size - 2, Ink, true, TextAnchor.MiddleCenter);
            t.name = "key";
            cap.Add(t);
            return cap;
        }

        /// <summary>A key cap and what it does, in a row.</summary>
        public static VisualElement Hint(string key, string what, float size = 18)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginRight = 22;
            row.Add(Cap(key, size));
            row.Add(Text(what, size, Silver));
            return row;
        }

        /// <summary>A soft round shadow, dark in the middle and clear at the edge, made once: behind the radar's edge and the damage glow.</summary>
        public static Texture2D Glow => glow != null ? glow : glow = Ring("Glow", 0.55f, 1.15f);
        /// <summary>The same, clear over most of the middle: the screen's edges only, for the damage glow.</summary>
        public static Texture2D Edge => edge != null ? edge : edge = Ring("Edge", 0.85f, 1.45f);
        static Texture2D glow, edge;

        static Texture2D Ring(string name, float inner, float outer)
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                    px[x + y * N] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(inner, outer, r))));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
