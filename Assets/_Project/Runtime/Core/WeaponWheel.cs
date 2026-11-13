// The weapon wheel (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md): hold Tab or LB on foot and what you carry is laid out in a ring, time slows to a quarter, and pointing
// with the mouse or the right stick chooses; letting go takes it in hand (Arsenal.Select). While it is open the view does not turn and you cannot fire: both wait for time
// above half speed (FootCamera, Arsenal).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Dad.Core
{
    public sealed class WeaponWheel : MonoBehaviour
    {
        public const float Slow = 0.25f, Outer = 230, Inner = 110;

        public GameInput Input;
        public Player Player;

        public bool Open { get; private set; }
        /// <summary>The slot pointed at (Arsenal.Slots); the one in hand until you point.</summary>
        public int Pointed { get; private set; }

        UIDocument doc;
        VisualElement ring;
        Label name_, rounds;
        readonly List<Label> labels = new List<Label>();
        Vector2 aim;
        bool byKey; // opened by holding the key, so letting it go closes it (Show from code waits for Hide)
        int drawnFor = -1, drawnCount = -1;

        public static WeaponWheel Attach(GameObject host, GameInput input, Player player)
        {
            var w = host.AddComponent<WeaponWheel>();
            w.Input = input;
            w.Player = player;
            return w;
        }

        void Awake()
        {
            doc = Ui.Panel("Weapon wheel", 5, transform);
            var root = doc.rootVisualElement;
            ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.style.position = Position.Absolute;
            ring.style.left = Length.Percent(50);
            ring.style.top = Length.Percent(50);
            ring.style.width = ring.style.height = Outer * 2;
            ring.style.translate = new Translate(-Outer, -Outer, 0);
            ring.generateVisualContent += Draw;
            root.Add(ring);
            name_ = Ui.At(Ui.Text("", 26, Ui.White, true, TextAnchor.MiddleCenter), Outer - 100, Outer - 30, float.NaN, float.NaN, 200);
            rounds = Ui.At(Ui.Text("", 18, Ui.Silver, false, TextAnchor.MiddleCenter), Outer - 100, Outer + 6, float.NaN, float.NaN, 200);
            ring.Add(name_);
            ring.Add(rounds);
            Ui.Show(root, false);
        }

        void OnDestroy() { if (Open && Mathf.Approximately(Time.timeScale, Slow)) Time.timeScale = 1; }

        Arsenal Guns => Player != null && Player.Now == Player.Phase.Walking ? Player.Arsenal : null;

        void Update()
        {
            var guns = Guns;
            bool can = Input != null && Input.Context == InputContext.OnFoot && guns != null;
            if (!Open)
            {
                if (can && Input.WeaponWheel.WasPressedThisFrame()) { Show(guns); byKey = true; }
                return;
            }
            if (!can) { Hide(false); return; }
            // pointing: the right stick where it is held, the mouse by how far it has moved since the wheel opened
            var look = Input.Look.ReadValue<Vector2>();
            bool pad = Input.Look.activeControl != null && Input.Look.activeControl.device is Gamepad;
            if (pad) { if (look.sqrMagnitude > 0.25f) aim = look; }
            else { aim += look; if (aim.magnitude > 120) aim = aim.normalized * 120; }
            if (aim.magnitude > (pad ? 0.5f : 25)) Point(Toward(aim, guns.Slots.Count));
            if (byKey && !Input.WeaponWheel.IsPressed()) Hide(true);
        }

        /// <summary>The slot in a direction (x right, y up), slot 0 at the top and the rest clockwise.</summary>
        public static int Toward(Vector2 way, int count)
        {
            float a = Mathf.Atan2(way.x, way.y) * Mathf.Rad2Deg; // 0 up, clockwise
            if (a < 0) a += 360;
            return Mathf.RoundToInt(a / (360f / count)) % count;
        }

        public void Show(Arsenal guns)
        {
            Open = true;
            byKey = false;
            aim = Vector2.zero;
            Pointed = guns.Current;
            if (Time.timeScale > Slow) Time.timeScale = Slow;
            Ui.Show(doc.rootVisualElement, true);
            Labels(guns);
            Point(Pointed);
        }

        /// <summary>Closes the wheel, taking the pointed gun in hand if asked.</summary>
        public void Hide(bool take)
        {
            Open = false;
            Ui.Show(doc.rootVisualElement, false);
            if (Mathf.Approximately(Time.timeScale, Slow)) Time.timeScale = 1;
            var guns = Guns;
            if (take && guns != null) guns.Select(Pointed);
        }

        public void Point(int slot)
        {
            var guns = Guns;
            if (guns == null) return;
            Pointed = Mathf.Clamp(slot, 0, guns.Slots.Count - 1);
            var s = guns.Slots[Pointed];
            name_.text = s == null ? "UNARMED" : s.Name.ToUpperInvariant();
            rounds.text = s == null ? "" : s.InGun + " / " + s.Spare;
            for (int i = 0; i < labels.Count; i++) labels[i].style.color = i == Pointed ? Ui.Ink : Ui.Silver;
            if (drawnFor != Pointed) { drawnFor = Pointed; ring.MarkDirtyRepaint(); }
        }

        void Labels(Arsenal guns)
        {
            if (drawnCount == guns.Slots.Count) return;
            drawnCount = guns.Slots.Count;
            foreach (var l in labels) l.RemoveFromHierarchy();
            labels.Clear();
            for (int i = 0; i < guns.Slots.Count; i++)
            {
                var s = guns.Slots[i];
                float a = (i * 360f / guns.Slots.Count - 90) * Mathf.Deg2Rad, r = (Outer + Inner) / 2;
                var l = Ui.At(Ui.Text(s == null ? "HANDS" : s.Name.ToUpperInvariant(), 18, Ui.Silver, true, TextAnchor.MiddleCenter), Outer + Mathf.Cos(a) * r - 70, Outer + Mathf.Sin(a) * r - 12, float.NaN, float.NaN, 140, 24);
                l.style.letterSpacing = 2;
                ring.Add(l);
                labels.Add(l);
            }
            ring.MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var guns = Guns;
            int n = guns != null ? guns.Slots.Count : drawnCount;
            if (n <= 0) return;
            var p = ctx.painter2D;
            var c = new Vector2(Outer, Outer);
            float step = 360f / n, gap = 1.5f;
            for (int i = 0; i < n; i++)
            {
                float mid = i * step - 90, a0 = mid - step / 2 + gap, a1 = mid + step / 2 - gap;
                p.fillColor = i == Pointed ? Ui.Fade(Ui.White, 0.95f) : Ui.Glass;
                p.BeginPath();
                p.Arc(c, Outer, a0, a1);
                p.Arc(c, Inner, a1, a0, ArcDirection.CounterClockwise);
                p.ClosePath();
                p.Fill();
            }
            p.strokeColor = Ui.Crimson;
            p.lineWidth = 3;
            float m = Pointed * step - 90;
            p.BeginPath();
            p.Arc(c, Outer + 6, m - step / 2 + gap, m + step / 2 - gap);
            p.Stroke();
        }
    }
}
