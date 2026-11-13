// The HUD (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md). Everything on it reads a system that is there: the radar the city map, the route the waypoint, the dots the police
// that are out; the bars your health and armour (Vitals), the marks how wanted you are (Heat), the gun and rounds what you hold (Arsenal), the prompt what the use button does
// where you are (Player), the speed, gear, lamps and fuel the car you drive. Built in code on one UI Toolkit panel; the Interface settings turn its parts on and off and size it.
using System.Collections.Generic;
using Dad.Traffic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dad.Core
{
    public sealed class Hud : MonoBehaviour
    {
        public const float RadarWidth = 320, RadarHeight = 200, Edge = 40;

        public GameInput Input;
        public Player Player;
        public TestDrive Drive;
        public DayCycle Day;
        public Precinct Precinct;

        public UIDocument Document { get; private set; }
        /// <summary>For tests: what the HUD shows now.</summary>
        public string Shown(string part) => part switch
        {
            "ammo" => ammo.text, "weapon" => weaponName.text, "prompt" => promptText.text, "key" => promptKey.text, "speed" => speed.text, "gear" => gear.text,
            "clock" => clock.text, "street" => street.text, "area" => area.text, "down" => downTime.text,
            "cash" => cash.text, "objective" => objective.text, "timer" => timer.text, "pays" => pays.text, _ => "",
        };
        public bool Showing(string part) => part switch
        {
            "hud" => all.resolvedStyle.display != DisplayStyle.None && all.style.display != DisplayStyle.None,
            "radar" => radarBox.style.display != DisplayStyle.None, "weapon" => weaponBox.style.display != DisplayStyle.None, "prompt" => promptBox.style.display != DisplayStyle.None,
            "vehicle" => vehicleBox.style.display != DisplayStyle.None, "crosshair" => cross.style.display != DisplayStyle.None, "down" => downBox.style.display != DisplayStyle.None,
            "waypoint" => wayMark.style.display != DisplayStyle.None, "job" => jobBox.style.display != DisplayStyle.None,
            "jobmark" => jobMark.style.display != DisplayStyle.None, "timer" => timer.style.display != DisplayStyle.None, _ => false,
        };
        public int HeatShown { get; private set; }
        public float HealthShown => healthFill.style.width.value.value / 100f;
        public MapView Radar => radar;

        VisualElement all, radarBox, you, northMark, wayMark, healthFill, armourFill, heatRow, weaponBox, reloadBar, reloadFill, cross, hit, vignette, downBox, vehicleBox, promptBox, noticeBox, fuelFill, lamp, leftArrow, rightArrow, bannerBox, jobMark, jobBox;
        readonly VisualElement[] marks = new VisualElement[Heat.Top];
        readonly VisualElement[] ticks = new VisualElement[4];
        readonly List<VisualElement> cops = new List<VisualElement>();
        readonly List<(Label label, float until)> notices = new List<(Label, float)>();
        MapView radar;
        Label clock, cash, gain, objective, timer, pays, weaponName, ammo, spare, area, street, banner, downTime, speed, unit, gear, engine, promptKey, promptText;
        string areaWas, promptWas;
        float bannerUntil, nextPlace, gainUntil;

        public static Hud Attach(GameObject host, GameInput input, Player player, TestDrive drive, DayCycle day, Precinct precinct)
        {
            var h = host.AddComponent<Hud>();
            h.Input = input;
            h.Player = player;
            h.Drive = drive;
            h.Day = day;
            h.Precinct = precinct;
            return h;
        }

        void Awake()
        {
            Document = Ui.Panel("HUD", 0, transform);
            Build(Document.rootVisualElement);
            GameSettings.Changed += Sized;
            Progress.Changed += Gained;
            Sized();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= Sized;
            Progress.Changed -= Gained;
        }

        // What the cash just did, shown beside it for a few seconds.
        void Gained(int change)
        {
            if (change == 0 || gain == null) return;
            gain.text = (change > 0 ? "+" : "-") + Jobs.Money(Mathf.Abs(change));
            gain.style.color = change > 0 ? Ui.Health : Ui.Job;
            gainUntil = Time.unscaledTime + 3;
        }

        void Sized()
        {
            if (Document == null) return;
            float k = GameSettings.Current.HudSize;
            Document.panelSettings.referenceResolution = new Vector2Int(Mathf.RoundToInt(1920 / k), Mathf.RoundToInt(1080 / k));
        }

        void Build(VisualElement root)
        {
            // damage glow and the down screen sit under everything else
            vignette = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 0, 0, 0, 0);
            vignette.style.backgroundImage = new StyleBackground(Ui.Edge);
            vignette.style.unityBackgroundImageTintColor = Ui.Crimson;
            vignette.style.opacity = 0;
            root.Add(vignette);

            all = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 0, 0, 0, 0);
            root.Add(all);

            // ---- radar, bottom left, with health and armour under it and the wanted marks over it
            radarBox = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, Edge, float.NaN, float.NaN, Edge + 18, RadarWidth, RadarHeight);
            all.Add(radarBox);
            radar = Ui.At(new MapView(), 0, 0, 0, 0);
            Ui.Round(radar, 10);
            Ui.Border(radar, 2, Ui.Fade(Ui.Ink, 0.9f));
            radar.RouteWidth = 4;
            radarBox.Add(radar);
            var shade = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 0, 0, 0, 0);
            shade.style.backgroundImage = new StyleBackground(Ui.Glow);
            shade.style.unityBackgroundImageTintColor = Ui.Fade(Ui.Ink, 0.55f);
            Ui.Round(shade, 10);
            radar.Add(shade);
            wayMark = Diamond(13, Ui.Route);
            radar.Add(wayMark);
            jobMark = Ui.Box(Ui.Job, 8);
            jobMark.style.position = Position.Absolute;
            jobMark.style.left = jobMark.style.top = 0;
            jobMark.style.width = jobMark.style.height = 16;
            Ui.Border(jobMark, 2, Ui.Fade(Ui.Ink, 0.9f));
            radar.Add(jobMark);
            northMark = Ui.Box(Ui.Fade(Ui.Ink, 0.85f), 9);
            northMark.style.width = northMark.style.height = 18;
            northMark.style.position = Position.Absolute;
            northMark.style.left = northMark.style.top = 0;
            northMark.style.justifyContent = Justify.Center;
            northMark.Add(Ui.Text("N", 12, Ui.White, true, TextAnchor.MiddleCenter));
            radar.Add(northMark);
            you = new VisualElement { pickingMode = PickingMode.Ignore };
            you.style.position = Position.Absolute;
            you.style.width = 20;
            you.style.height = 20;
            you.style.left = RadarWidth / 2 - 10;
            you.style.top = RadarHeight / 2 - 10;
            you.generateVisualContent += Arrow;
            radar.Add(you);

            var bars = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, Edge, float.NaN, float.NaN, Edge, RadarWidth, 8);
            bars.style.flexDirection = FlexDirection.Row;
            all.Add(bars);
            healthFill = Bar(bars, 0.62f, Ui.Health);
            var gap = new VisualElement();
            gap.style.width = 6;
            bars.Add(gap);
            armourFill = Bar(bars, 0.38f, Ui.Armour);

            heatRow = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, Edge + 4, float.NaN, float.NaN, Edge + 18 + RadarHeight + 12);
            heatRow.style.flexDirection = FlexDirection.Row;
            all.Add(heatRow);
            for (int i = 0; i < marks.Length; i++) { marks[i] = Diamond(15, Ui.White); marks[i].style.position = Position.Relative; marks[i].style.marginRight = 12; heatRow.Add(marks[i]); }

            // ---- top right: the clock, the cash, then the gun
            clock = Ui.At(Ui.Text("12:00", 26, Ui.White, true, TextAnchor.MiddleRight), float.NaN, Edge - 8, Edge, float.NaN, 200);
            all.Add(clock);
            var money = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, float.NaN, Edge + 26, Edge, float.NaN, 360);
            money.style.flexDirection = FlexDirection.Row;
            money.style.justifyContent = Justify.FlexEnd;
            money.style.alignItems = Align.Center;
            all.Add(money);
            gain = Ui.Text("", 16, Ui.Health, true, TextAnchor.MiddleRight);
            gain.style.marginRight = 10;
            money.Add(gain);
            cash = Ui.Text("Tk 0", 22, Ui.Health, true, TextAnchor.MiddleRight);
            money.Add(cash);
            weaponBox = Ui.At(Ui.Box(Ui.Glass, 8), float.NaN, Edge + 64, Edge, float.NaN, 250);
            weaponBox.style.paddingLeft = weaponBox.style.paddingRight = 16;
            weaponBox.style.paddingTop = 10;
            weaponBox.style.paddingBottom = 12;
            all.Add(weaponBox);
            weaponName = Ui.Text("", 16, Ui.Steel, true);
            weaponName.style.letterSpacing = 3;
            weaponBox.Add(weaponName);
            var rounds = new VisualElement { pickingMode = PickingMode.Ignore };
            rounds.style.flexDirection = FlexDirection.Row;
            rounds.style.alignItems = Align.FlexEnd;
            ammo = Ui.Text("", 40, Ui.White, true);
            spare = Ui.Text("", 20, Ui.Silver);
            spare.style.marginLeft = 8;
            spare.style.marginBottom = 6;
            rounds.Add(ammo);
            rounds.Add(spare);
            weaponBox.Add(rounds);
            reloadBar = Ui.Box(Ui.Fade(Ui.Steel, 0.3f), 2);
            reloadBar.style.height = 4;
            reloadBar.style.marginTop = 6;
            reloadFill = Ui.Box(Ui.White, 2);
            reloadFill.style.height = 4;
            reloadBar.Add(reloadFill);
            weaponBox.Add(reloadBar);

            // ---- top middle: the job, and its clock while you carry a parcel
            jobBox = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 0, Edge - 8, 0, float.NaN);
            jobBox.style.alignItems = Align.Center;
            all.Add(jobBox);
            var card = Ui.Box(Ui.Glass, 8);
            card.style.paddingLeft = card.style.paddingRight = 18;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 10;
            card.style.alignItems = Align.Center;
            card.style.borderTopWidth = 3;
            card.style.borderTopColor = Ui.Job;
            jobBox.Add(card);
            objective = Ui.Text("", 20, Ui.White, true, TextAnchor.MiddleCenter);
            card.Add(objective);
            var clockLine = Row(card);
            clockLine.style.alignItems = Align.Center;
            clockLine.style.marginTop = 2;
            timer = Ui.Text("", 28, Ui.White, true, TextAnchor.MiddleCenter);
            timer.style.marginRight = 12;
            clockLine.Add(timer);
            pays = Ui.Text("", 15, Ui.Steel, false, TextAnchor.MiddleCenter);
            clockLine.Add(pays);

            // ---- top left: notices
            noticeBox = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, Edge, Edge - 8, float.NaN, float.NaN, 520);
            all.Add(noticeBox);

            // ---- bottom right: where you are, the car
            street = Ui.At(Ui.Text("", 20, Ui.White, true, TextAnchor.MiddleRight), float.NaN, float.NaN, Edge, Edge + 24, 600);
            area = Ui.At(Ui.Text("", 15, Ui.Steel, false, TextAnchor.MiddleRight), float.NaN, float.NaN, Edge, Edge, 600);
            area.style.letterSpacing = 2;
            all.Add(street);
            all.Add(area);
            bannerBox = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, float.NaN, float.NaN, Edge, 260, 900);
            bannerBox.style.alignItems = Align.FlexEnd;
            banner = Ui.Text("", 54, Ui.White, true, TextAnchor.MiddleRight);
            banner.style.letterSpacing = 4;
            bannerBox.Add(banner);
            var rule = Ui.Box(Ui.Crimson);
            rule.style.width = 120;
            rule.style.height = 3;
            rule.style.marginTop = 6;
            bannerBox.Add(rule);
            all.Add(bannerBox);

            vehicleBox = Ui.At(Ui.Box(Ui.Glass, 8), float.NaN, float.NaN, Edge, Edge + 64, 250);
            vehicleBox.style.paddingLeft = vehicleBox.style.paddingRight = 16;
            vehicleBox.style.paddingTop = vehicleBox.style.paddingBottom = 10;
            all.Add(vehicleBox);
            var top = Row(vehicleBox);
            top.style.alignItems = Align.FlexEnd;
            speed = Ui.Text("0", 46, Ui.White, true);
            unit = Ui.Text("KM/H", 15, Ui.Steel, true);
            unit.style.marginLeft = 8;
            unit.style.marginBottom = 9;
            var fill = new VisualElement();
            fill.style.flexGrow = 1;
            gear = Ui.Text("N", 30, Ui.Silver, true, TextAnchor.MiddleRight);
            gear.style.marginBottom = 4;
            top.Add(speed);
            top.Add(unit);
            top.Add(fill);
            top.Add(gear);
            var signs = Row(vehicleBox);
            signs.style.alignItems = Align.Center;
            signs.style.marginTop = 2;
            leftArrow = Pointer(true);
            rightArrow = Pointer(false);
            lamp = Ui.Text("LAMPS", 12, Ui.Route, true);
            lamp.style.marginLeft = 10;
            engine = Ui.Text("ENGINE OFF", 12, Ui.Crimson, true);
            engine.style.marginLeft = 10;
            var fill2 = new VisualElement();
            fill2.style.flexGrow = 1;
            signs.Add(leftArrow);
            signs.Add(lamp);
            signs.Add(engine);
            signs.Add(fill2);
            signs.Add(rightArrow);
            var fuel = Row(vehicleBox);
            fuel.style.alignItems = Align.Center;
            fuel.style.marginTop = 6;
            fuel.Add(Ui.Text("FUEL", 11, Ui.Steel, true));
            var tank = Ui.Box(Ui.Fade(Ui.Steel, 0.3f), 2);
            tank.style.flexGrow = 1;
            tank.style.height = 4;
            tank.style.marginLeft = 8;
            fuelFill = Ui.Box(Ui.Silver, 2);
            fuelFill.style.height = 4;
            tank.Add(fuelFill);
            fuel.Add(tank);

            // ---- the middle: crosshair and the prompt
            cross = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 960, 540, float.NaN, float.NaN, 0, 0);
            cross.style.left = Length.Percent(50);
            cross.style.top = Length.Percent(50);
            all.Add(cross);
            var dot = Ui.At(Ui.Box(Ui.White, 2), -2, -2, float.NaN, float.NaN, 4, 4);
            cross.Add(dot);
            for (int i = 0; i < 4; i++)
            {
                bool flat = i % 2 == 0;
                ticks[i] = Ui.At(Ui.Box(Ui.White, 1), 0, 0, float.NaN, float.NaN, flat ? 10 : 2, flat ? 2 : 10);
                Ui.Border(ticks[i], 0.5f, Ui.Fade(Ui.Ink, 0.6f));
                cross.Add(ticks[i]);
            }
            hit = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, -14, -14, float.NaN, float.NaN, 28, 28);
            hit.generateVisualContent += HitMark;
            cross.Add(hit);

            promptBox = Ui.At(Ui.Box(Ui.Glass, 8), float.NaN, float.NaN, float.NaN, 170);
            promptBox.style.left = Length.Percent(50);
            promptBox.style.translate = new Translate(Length.Percent(-50), 0, 0);
            promptBox.style.flexDirection = FlexDirection.Row;
            promptBox.style.alignItems = Align.Center;
            promptBox.style.paddingLeft = promptBox.style.paddingRight = 14;
            promptBox.style.paddingTop = promptBox.style.paddingBottom = 8;
            var cap = Ui.Cap("F", 20);
            promptKey = cap.Q<Label>("key");
            promptBox.Add(cap);
            promptText = Ui.Text("", 20, Ui.White);
            promptBox.Add(promptText);
            all.Add(promptBox);

            // ---- down
            downBox = Ui.At(Ui.Box(Ui.Fade(Ui.Ink, 0.55f)), 0, 0, 0, 0);
            downBox.style.alignItems = Align.Center;
            downBox.style.justifyContent = Justify.Center;
            var downTitle = Ui.Text("DOWN", 96, Ui.White, true, TextAnchor.MiddleCenter);
            downTitle.style.letterSpacing = 12;
            downBox.Add(downTitle);
            var line = Ui.Box(Ui.Crimson);
            line.style.width = 220;
            line.style.height = 3;
            line.style.marginTop = 8;
            line.style.marginBottom = 14;
            downBox.Add(line);
            downTime = Ui.Text("", 22, Ui.Silver, false, TextAnchor.MiddleCenter);
            downBox.Add(downTime);
            root.Add(downBox);
        }

        static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            parent.Add(r);
            return r;
        }

        static VisualElement Bar(VisualElement parent, float share, Color colour)
        {
            var back = Ui.Box(Ui.Fade(Ui.Ink, 0.7f), 3);
            back.style.width = Length.Percent(share * 100);
            back.style.height = 8;
            back.style.flexShrink = 0;
            back.style.paddingLeft = back.style.paddingRight = back.style.paddingTop = back.style.paddingBottom = 1;
            Ui.Border(back, 1, Ui.Fade(Ui.Ink, 0.9f));
            var fill = Ui.Box(colour, 2);
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
            back.Add(fill);
            parent.Add(back);
            return fill;
        }

        static VisualElement Diamond(float size, Color colour)
        {
            var d = Ui.Box(colour, 2);
            d.style.position = Position.Absolute;
            d.style.left = d.style.top = 0;
            d.style.width = d.style.height = size;
            d.style.rotate = new Rotate(new Angle(45, AngleUnit.Degree));
            Ui.Border(d, 1.5f, Ui.Fade(Ui.Ink, 0.9f));
            return d;
        }

        // An indicator: a green triangle pointing left or right.
        static VisualElement Pointer(bool left)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = 16;
            e.style.height = 14;
            e.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.fillColor = Ui.Health;
                p.BeginPath();
                if (left) { p.MoveTo(new Vector2(0, 7)); p.LineTo(new Vector2(14, 0)); p.LineTo(new Vector2(14, 14)); }
                else { p.MoveTo(new Vector2(16, 7)); p.LineTo(new Vector2(2, 0)); p.LineTo(new Vector2(2, 14)); }
                p.ClosePath();
                p.Fill();
            };
            return e;
        }

        // You on the radar: a point to where you face.
        static void Arrow(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.fillColor = Ui.White;
            p.strokeColor = Ui.Ink;
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(10, 1));
            p.LineTo(new Vector2(18, 18));
            p.LineTo(new Vector2(10, 14));
            p.LineTo(new Vector2(2, 18));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        // A shot that struck: four short strokes on the slants.
        static void HitMark(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.strokeColor = Ui.White;
            p.lineWidth = 2.5f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(2, 2)); p.LineTo(new Vector2(9, 9));
            p.MoveTo(new Vector2(26, 2)); p.LineTo(new Vector2(19, 9));
            p.MoveTo(new Vector2(2, 26)); p.LineTo(new Vector2(9, 19));
            p.MoveTo(new Vector2(26, 26)); p.LineTo(new Vector2(19, 19));
            p.Stroke();
        }

        void Update()
        {
            Ui.Watch();
            Rumble.Tick();
            var s = GameSettings.Current;
            var context = Input != null ? Input.Context : InputContext.OnFoot;
            bool down = Player != null && Player.Now == Player.Phase.Down;
            bool playing = Input == null || context == InputContext.OnFoot || context == InputContext.Driving;
            Ui.Show(all, s.Hud && playing && !down);
            Ui.Show(downBox, down && context != InputContext.Paused && context != InputContext.Menu);
            if (down) downTime.text = "Back on your feet in " + Mathf.CeilToInt(Mathf.Max(0, Player.DownFor - Player.DownTime));
            Hurt();
            Notices(s);
            if (!s.Hud || !playing || down) return;

            var origin = FloatingOrigin.Active;
            var target = origin != null ? origin.Target : null;
            double east = 0, north = 0;
            if (target != null) origin.Offset.ToWorld(target.position.x, target.position.z, out east, out north);
            var car = Drive != null ? Drive.Vehicle : null;
            Radar_(s, target, east, north, car);
            Job_();
            Vitals_();
            Weapon(s, context);
            Vehicle(s, car);
            Prompt(s, context);
            Place(east, north, target != null);
            if (Day != null)
            {
                int minutes = (int)(Day.Hours * 60) % (24 * 60);
                clock.text = $"{minutes / 60:00}:{minutes % 60:00}";
            }
        }

        void Radar_(GameSettings s, Transform target, double east, double north, PlayerVehicle car)
        {
            Ui.Show(radarBox, s.Radar);
            if (!s.Radar) return;
            radar.Map = CityMap.Active;
            radar.East = east;
            radar.North = north;
            var cam = Camera.main;
            float look = cam != null ? cam.transform.eulerAngles.y : 0;
            radar.Turn = s.NorthUp ? 0 : look;
            // on foot it shows about 400 m across; driving fast, more
            float fast = car != null ? Mathf.Clamp01((float)System.Math.Abs(car.State.Speed) / 25f) : 0;
            radar.PerMetre = Mathf.Lerp(0.8f, 0.45f, fast);
            if (target != null) Waypoint.Follow(east, north);
            radar.Refresh(Waypoint.Version);
            float facing = target != null ? target.eulerAngles.y : 0;
            you.style.rotate = new Rotate(new Angle(facing - radar.Turn, AngleUnit.Degree));
            radar.Place(northMark, east, north + 1e6, true, 12);
            // a job's own waypoint shows as the job's mark, not as a second one
            if (Waypoint.Active && !Waypoint.ForJob) radar.Place(wayMark, Waypoint.East, Waypoint.North, true, 9);
            else Ui.Show(wayMark, false);
            var job = Jobs.Active;
            if (job != null && job.Marked) radar.Place(jobMark, job.MarkEast, job.MarkNorth, true, 10);
            else Ui.Show(jobMark, false);
            int n = 0;
            if (Precinct != null)
                foreach (var o in Precinct.Officers)
                {
                    if (o == null || o.Dead || origin_(o.transform.position, out double oe, out double on) == false) continue;
                    if (n == cops.Count) { var dot = Ui.Box(Ui.Police, 5); dot.style.position = Position.Absolute; dot.style.left = dot.style.top = 0; dot.style.width = dot.style.height = 10; Ui.Border(dot, 1.5f, Ui.Ink); radar.Insert(radar.IndexOf(you), dot); cops.Add(dot); }
                    radar.Place(cops[n], oe, on, false, 4);
                    n++;
                }
            for (int i = n; i < cops.Count; i++) Ui.Show(cops[i], false);
            // the edge flashes red and blue while the police can see you
            bool seen = Heat.Level > 0 && Time.time - Heat.SeenAt < 1.5f;
            var edge = seen ? (Mathf.Repeat(Time.time, 0.5f) < 0.25f ? Ui.Crimson : Ui.Police) : Ui.Fade(Ui.Ink, 0.9f);
            Ui.Border(radar, seen ? 3 : 2, edge);
        }

        static bool origin_(Vector3 at, out double east, out double north)
        {
            east = north = 0;
            var o = FloatingOrigin.Active;
            if (o == null) return false;
            o.Offset.ToWorld(at.x, at.z, out east, out north);
            return true;
        }

        // The job, the clock on it (red in the last 20 seconds) and what it pays now; and the cash.
        void Job_()
        {
            cash.text = Jobs.Money(Progress.Current.Cash);
            float fade = gainUntil - Time.unscaledTime;
            gain.style.opacity = Mathf.Clamp01(fade / 0.5f);
            var j = Jobs.Active;
            bool on = j != null && j.Marked;
            Ui.Show(jobBox, on);
            if (!on) return;
            objective.text = j.Objective;
            bool carrying = j.Now == Jobs.Stage.Carrying;
            Ui.Show(timer, carrying);
            Ui.Show(pays, carrying);
            if (!carrying) return;
            timer.text = Jobs.Clock(j.Left);
            bool late = j.Left < 20;
            timer.style.color = late && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f ? Ui.Job : Ui.White;
            pays.text = "pays " + Jobs.Money(j.PayNow);
        }

        void Vitals_()
        {
            var v = Player != null ? Player.Vitals : Vitals.Player;
            float health = v != null ? v.Health / Vitals.Most : 1, armour = v != null ? v.Armour / Vitals.Most : 0;
            healthFill.style.width = Length.Percent(health * 100);
            healthFill.style.backgroundColor = health < 0.25f ? Ui.Crimson : Ui.Health;
            armourFill.style.width = Length.Percent(armour * 100);
            HeatShown = Heat.Level;
            Ui.Show(heatRow, Heat.Level > 0);
            bool seen = Time.time - Heat.SeenAt < 2f;
            bool flash = seen && Mathf.Repeat(Time.time, 0.6f) < 0.3f;
            for (int i = 0; i < marks.Length; i++)
            {
                bool on = i < Heat.Level;
                marks[i].style.backgroundColor = on ? (flash ? Ui.Police : Ui.White) : Ui.Fade(Ui.Ink, 0.5f);
                marks[i].style.opacity = on && !seen ? 0.55f : 1;
            }
        }

        void Weapon(GameSettings s, InputContext context)
        {
            var a = Player != null ? Player.Arsenal : null;
            bool onFoot = context == InputContext.OnFoot && Player != null && Player.Now == Player.Phase.Walking;
            var slot = a != null ? a.Now : null;
            Ui.Show(weaponBox, onFoot && slot != null);
            Ui.Show(cross, onFoot && slot != null && s.Crosshair);
            if (slot == null || !onFoot) return;
            weaponName.text = slot.Name.ToUpperInvariant();
            ammo.text = slot.InGun.ToString();
            ammo.style.color = slot.InGun == 0 ? Ui.Crimson : Ui.White;
            spare.text = "/ " + slot.Spare;
            Ui.Show(reloadBar, a.Reloading);
            reloadFill.style.width = Length.Percent(a.ReloadDone * 100);
            float spread = slot.Spread + a.Bloom + (a.Aiming ? 0 : 2.5f);
            float gap = 4 + spread * 5;
            ticks[0].style.left = -gap - 10; ticks[0].style.top = -1;
            ticks[2].style.left = gap; ticks[2].style.top = -1;
            ticks[1].style.left = -1; ticks[1].style.top = -gap - 10;
            ticks[3].style.left = -1; ticks[3].style.top = gap;
            float since = Time.time - a.StruckAt;
            Ui.Show(hit, since < 0.25f);
            cross.style.opacity = a.Aiming ? 1 : 0.7f;
        }

        void Vehicle(GameSettings s, PlayerVehicle car)
        {
            Ui.Show(vehicleBox, car != null);
            if (car == null) return;
            var st = car.State;
            double v = System.Math.Abs(st.Speed);
            speed.text = Mathf.RoundToInt((float)(v * (s.Miles ? 2.23694 : 3.6))).ToString();
            unit.text = s.Miles ? "MPH" : "KM/H";
            gear.text = st.Gear < 0 ? "R" : st.Gear == 0 ? "N" : st.Gear.ToString();
            bool blink = Mathf.Repeat(Time.time, 0.7f) < 0.35f;
            leftArrow.style.opacity = blink && (st.Hazards || st.Indicator < 0) ? 1 : 0.15f;
            rightArrow.style.opacity = blink && (st.Hazards || st.Indicator > 0) ? 1 : 0.15f;
            Ui.Show(lamp, st.HeadLamps);
            Ui.Show(engine, !st.EngineOn);
            fuelFill.style.width = Length.Percent((float)st.Fuel * 100);
            fuelFill.style.backgroundColor = st.Fuel < 0.15 ? Ui.Crimson : Ui.Silver;
        }

        void Prompt(GameSettings s, InputContext context)
        {
            string what = Player != null && s.Prompts ? Player.Prompt : null;
            Ui.Show(promptBox, what != null);
            if (what == null) return;
            var action = Player.Now == Player.Phase.Driving ? Input?.ExitVehicle : Input?.Interact;
            promptKey.text = Ui.Key(action);
            if (what != promptWas) { promptWas = what; promptText.text = what; }
        }

        // Street and district, a few times a second; the district's name large for a while when you come into it.
        void Place(double east, double north, bool known)
        {
            var map = CityMap.Active;
            if (!known || map == null) { street.text = area.text = ""; Ui.Show(bannerBox, false); return; }
            if (Time.unscaledTime >= nextPlace)
            {
                nextPlace = Time.unscaledTime + 0.25f;
                street.text = map.Street(east, north) ?? "";
                var now = map.Area(east, north) ?? "";
                area.text = now.ToUpperInvariant();
                if (now != areaWas)
                {
                    if (areaWas != null && now.Length > 0) { banner.text = now.ToUpperInvariant(); bannerUntil = Time.unscaledTime + 4; }
                    areaWas = now;
                }
            }
            float left = bannerUntil - Time.unscaledTime;
            Ui.Show(bannerBox, left > 0);
            bannerBox.style.opacity = Mathf.Clamp01(left / 0.6f) * Mathf.Clamp01((4 - left) / 0.4f);
        }

        void Hurt()
        {
            var v = Player != null ? Player.Vitals : Vitals.Player;
            if (v == null) { vignette.style.opacity = 0; return; }
            float since = Time.time - v.HurtAt;
            float flash = Mathf.Clamp01(1 - since / 0.5f) * 0.55f;
            float low = v.Health < 30 && !v.Dead ? (0.3f + 0.12f * Mathf.Sin(Time.time * 5)) * (1 - v.Health / 30) + 0.12f : 0;
            vignette.style.opacity = v.Dead ? 0.6f : Mathf.Max(flash, low);
        }

        void Notices(GameSettings s)
        {
            if (Notice.Waiting.Count > 0)
            {
                foreach (var text in Notice.Waiting)
                {
                    if (!s.Notices) continue;
                    var l = Ui.Text(text, 20, Ui.White);
                    var box = Ui.Box(Ui.Glass, 6);
                    box.style.paddingLeft = box.style.paddingRight = 14;
                    box.style.paddingTop = box.style.paddingBottom = 8;
                    box.style.marginBottom = 8;
                    box.style.alignSelf = Align.FlexStart;
                    box.style.borderLeftWidth = 3;
                    box.style.borderLeftColor = Ui.Crimson;
                    box.Add(l);
                    noticeBox.Add(box);
                    notices.Add((l, Time.unscaledTime + 5));
                }
                Notice.Waiting.Clear();
                while (notices.Count > 3) { notices[0].label.parent.RemoveFromHierarchy(); notices.RemoveAt(0); }
            }
            for (int i = notices.Count - 1; i >= 0; i--)
            {
                float left = notices[i].until - Time.unscaledTime;
                if (left > 0) { notices[i].label.parent.style.opacity = Mathf.Clamp01(left / 0.5f); continue; }
                notices[i].label.parent.RemoveFromHierarchy();
                notices.RemoveAt(i);
            }
        }
    }
}
