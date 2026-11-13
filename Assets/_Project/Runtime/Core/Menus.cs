// The menus (2026-11-10, Docs/PRODUCTION/PLAYER_INTERFACE.md): the start screen, the pause menu, the settings in six tabs, and the map. Built in code on one UI Toolkit
// panel over the HUD. They are worked by rows: up and down chooses, left and right changes, select presses, back goes back a screen; tabs with Q and E or the shoulders;
// the mouse chooses by pointing and changes by clicking. The game stops under the pause menu and the map, not under the start screen (the city comes in behind it).
// Every setting acts as it is changed and is written when you leave the settings (GameSettings). Keys and buttons are rebound in Controls (GameInput.StartRebind).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Dad.Core
{
    public sealed class Menus : MonoBehaviour
    {
        /// <summary>Show the start screen at boot. Unset: shown except in batch runs (tests and benches).</summary>
        public static bool? Title;

        public GameInput Input;
        public Player Player;
        public Precinct Precinct;

        public UIDocument Document { get; private set; }
        /// <summary>The screen on top ("Title", "Pause", "Settings", "Map", "Credits"), or empty.</summary>
        public string Top => pages.Count == 0 ? "" : pages[pages.Count - 1].Name;
        public bool Open => pages.Count > 0;
        /// <summary>For tests: the rows on the screen on top, and which is chosen.</summary>
        public IReadOnlyList<string> RowNames { get { var l = new List<string>(); if (Open) foreach (var r in pages[^1].Items) l.Add(r.Name); return l; } }
        public int Chosen => Open ? pages[^1].At : -1;
        public string Tab => Open && pages[^1].Tabs != null ? pages[^1].Tabs[pages[^1].Tab] : "";
        public bool Rebinding => rebind != null;
        public MapView MapShown => map;
        /// <summary>For tests: the help line under the rows.</summary>
        public string HelpShown => help != null ? help.text : "";

        sealed class Row
        {
            public string Name, Help;
            /// <summary>A second, smaller line under the name (the credits: maker and licence).</summary>
            public string Under;
            public Func<string> Value;
            public Func<float> Share;
            public Action<int> Change;
            public Action Press;
            public bool Header, Warn;
            public VisualElement Element, Fill;
            public Label Label, Shown, UnderLabel;
        }

        sealed class Page
        {
            public string Name, Heading;
            public string[] Tabs;
            public int Tab, At;
            public Func<int, List<Row>> Make;
            public List<Row> Items = new List<Row>();
        }

        readonly List<Page> pages = new List<Page>();
        VisualElement root, backdrop, column, tabRow, hints, mapRoot, mapCursor, mapYou, mapWay, mapJob, titleBox;
        ScrollView list;
        Label heading, help;
        MapView map;
        readonly List<VisualElement> mapCops = new List<VisualElement>();
        InputActionRebindingExtensions.RebindingOperation rebind;
        Row rebinding;
        int quietUntil;
        bool dirty, padList, quitArmed, stopped;
        float repeatAt;
        int repeatDir;
        Vector2 mouseWas, mapCursorAt;
        bool mapByMouse, dragging;

        static GameSettings S => GameSettings.Current;

        public static Menus Attach(GameObject host, GameInput input, Player player, Precinct precinct)
        {
            var m = host.AddComponent<Menus>();
            m.Input = input;
            m.Player = player;
            m.Precinct = precinct;
            return m;
        }

        void Start()
        {
            Document = Ui.Panel("Menus", 10, transform);
            Build(Document.rootVisualElement);
            if (Input != null) Input.CancelPops = false;
            if (Title ?? !Application.isBatchMode) ShowTitle();
            else Ui.Show(root, false);
            Cast.Changed += Recast;
        }

        // Who you are changed (here or elsewhere): the rows that say so are shown again.
        void Recast()
        {
            if (pages.Count == 0) return;
            var p = pages[^1];
            for (int i = 0; i < p.Items.Count; i++) Paint(p.Items[i], i == p.At);
        }

        void OnDestroy()
        {
            Cast.Changed -= Recast;
            rebind?.Dispose();
            if (stopped) { Time.timeScale = 1; AudioListener.pause = false; }
        }

        // ---------------------------------------------------------------- building

        void Build(VisualElement r)
        {
            root = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 0, 0, 0, 0);
            r.Add(root);
            backdrop = Ui.At(Ui.Box(Ui.Fade(Ui.Ink, 0.72f)), 0, 0, 0, 0);
            root.Add(backdrop);

            // the start screen's title
            titleBox = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 120, 150, float.NaN, float.NaN, 1200);
            var game = Ui.Text("DHAKA: AFTER DARK", 92, Ui.White, true);
            game.style.letterSpacing = 6;
            titleBox.Add(game);
            var rule = Ui.Box(Ui.Crimson);
            rule.style.width = 160;
            rule.style.height = 4;
            rule.style.marginTop = 10;
            rule.style.marginBottom = 14;
            titleBox.Add(rule);
            var tag = Ui.Text("A CITY THAT NEVER SLEEPS.", 22, Ui.Silver);
            tag.style.letterSpacing = 6;
            titleBox.Add(tag);
            root.Add(titleBox);
            var credit = Ui.At(Ui.Text("MADE BY ISHAK HASAN SABBiR", 15, Ui.Steel), float.NaN, float.NaN, 60, 44);
            credit.style.letterSpacing = 4;
            credit.name = "credit";
            root.Add(credit);

            column = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 120, 130, float.NaN, 130, 760);
            root.Add(column);
            heading = Ui.Text("", 46, Ui.White, true);
            heading.style.letterSpacing = 6;
            heading.style.marginBottom = 18;
            column.Add(heading);
            tabRow = new VisualElement { pickingMode = PickingMode.Ignore };
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 14;
            column.Add(tabRow);
            list = new ScrollView(ScrollViewMode.Vertical) { pickingMode = PickingMode.Ignore };
            list.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            list.style.flexGrow = 1;
            list.style.flexShrink = 1;
            column.Add(list);
            help = Ui.Text("", 18, Ui.Steel);
            help.style.marginTop = 14;
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.minHeight = 48;
            column.Add(help);

            hints = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 120, float.NaN, float.NaN, 50);
            hints.style.flexDirection = FlexDirection.Row;
            root.Add(hints);

            // the map screen
            mapRoot = Ui.At(new VisualElement { pickingMode = PickingMode.Ignore }, 0, 0, 0, 0);
            map = Ui.At(new MapView(), 0, 0, 0, 0);
            map.RouteWidth = 6;
            mapRoot.Add(map);
            mapWay = Ui.Box(Ui.Route, 2);
            mapWay.style.position = Position.Absolute;
            mapWay.style.left = mapWay.style.top = 0;
            mapWay.style.width = mapWay.style.height = 18;
            mapWay.style.rotate = new Rotate(new Angle(45, AngleUnit.Degree));
            Ui.Border(mapWay, 2, Ui.Ink);
            map.Add(mapWay);
            mapJob = Ui.Box(Ui.Job, 10);
            mapJob.style.position = Position.Absolute;
            mapJob.style.left = mapJob.style.top = 0;
            mapJob.style.width = mapJob.style.height = 20;
            Ui.Border(mapJob, 2, Ui.Ink);
            map.Add(mapJob);
            mapYou = Ui.Box(Ui.White, 9);
            mapYou.style.position = Position.Absolute;
            mapYou.style.left = mapYou.style.top = 0;
            mapYou.style.width = mapYou.style.height = 18;
            Ui.Border(mapYou, 3, Ui.Crimson);
            map.Add(mapYou);
            mapCursor = new VisualElement { pickingMode = PickingMode.Ignore };
            mapCursor.style.position = Position.Absolute;
            mapCursor.style.left = mapCursor.style.top = 0;
            mapCursor.style.width = mapCursor.style.height = 40;
            mapCursor.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                p.strokeColor = Ui.White;
                p.lineWidth = 2;
                p.BeginPath();
                p.Arc(new Vector2(20, 20), 12, 0, 360);
                p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(20, 0)); p.LineTo(new Vector2(20, 10));
                p.MoveTo(new Vector2(20, 30)); p.LineTo(new Vector2(20, 40));
                p.MoveTo(new Vector2(0, 20)); p.LineTo(new Vector2(10, 20));
                p.MoveTo(new Vector2(30, 20)); p.LineTo(new Vector2(40, 20));
                p.Stroke();
            };
            mapRoot.Add(mapCursor);
            var legend = Ui.At(Ui.Box(Ui.Glass, 8), float.NaN, 40, 40, float.NaN, 230);
            legend.style.paddingLeft = legend.style.paddingRight = 16;
            legend.style.paddingTop = legend.style.paddingBottom = 12;
            legend.Add(Key(Ui.White, "You", true));
            legend.Add(Key(Ui.Route, "Waypoint", false));
            legend.Add(Key(Ui.Route, "Route", false, true));
            legend.Add(Key(Ui.Job, "Job", true));
            legend.Add(Key(Ui.Job, "Route to the job", false, true));
            legend.Add(Key(Ui.Police, "Police", true));
            mapRoot.Add(legend);
            var mapTitle = Ui.At(Ui.Text("MAP", 40, Ui.White, true), 120, 40);
            mapTitle.style.letterSpacing = 6;
            mapRoot.Add(mapTitle);
            root.Add(mapRoot);
            Ui.Show(mapRoot, false);
        }

        static VisualElement Key(Color c, string what, bool round, bool line = false)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            var mark = Ui.Box(c, round ? 7 : 2);
            mark.style.width = line ? 18 : 14;
            mark.style.height = line ? 4 : 14;
            mark.style.marginRight = line ? 12 : 16; // every label starts at the same place
            if (!line && !round) mark.style.rotate = new Rotate(new Angle(45, AngleUnit.Degree));
            row.Add(mark);
            row.Add(Ui.Text(what, 17, Ui.Silver));
            return row;
        }

        // ---------------------------------------------------------------- screens

        public void ShowTitle()
        {
            if (Input != null && Input.IsGameplay) Input.Push(InputContext.Menu);
            Push(new Page { Name = "Title", Heading = "", Make = _ => new List<Row>
            {
                Button("Play", PlayLine(), () => CloseAll()),
                Character(),
                Button("Settings", "Display, graphics, sound, controls and the HUD.", () => Push(SettingsPage())),
                Button("Credits", "Who made the people, guns, ships and buildings the game is built with, and their licences.", () => Push(CreditsPage())),
                Quit(),
            } });
        }

        // Who made what (Credits, Resources/Credits.txt): each work's title with its maker and licence under it, in sections; choosing one shows its source and
        // what was changed. Nothing to press; Back returns to the start screen.
        static Page CreditsPage() => new Page { Name = "Credits", Heading = "CREDITS", Make = _ =>
        {
            var rows = new List<Row>();
            string section = null;
            foreach (var e in Credits.Load())
            {
                if (e.Section != section && e.Section.Length > 0) rows.Add(new Row { Header = true, Name = e.Section });
                section = e.Section;
                rows.Add(new Row { Name = e.Title, Under = e.Under, Help = e.About });
            }
            return rows;
        } };

        // Who you play: one of the twenty, men then women; changing it changes the body standing behind the menu, and it is kept with your progress.
        Row Character()
        {
            Row r = null;
            r = new Row
            {
                Name = "Character",
                Help = CharacterLine(),
                Value = () => Cast.Name(Cast.You),
                Change = d =>
                {
                    string next = Cast.Step(d);
                    bool done = Player != null ? Player.Become(next) : Cast.Has(next);
                    if (Player == null && done) Cast.Use(next);
                    r.Help = done ? CharacterLine() : "Change who you are on foot, not in a car or while down.";
                    help.text = r.Help;
                },
            };
            return r;
        }

        static string CharacterLine() => $"{Cast.Clothes(Cast.You)}. Ten men and ten women to choose from; who you are is kept between runs.";

        // What the start screen says you have so far (Progress, kept between runs).
        static string PlayLine()
        {
            var p = Progress.Current;
            if (p.Deliveries == 0 && p.Cash == 0) return "Into the city. Deliveries pay; your cash is kept between runs.";
            return $"Into the city. {Jobs.Money(p.Cash)} in hand, {p.Deliveries} {(p.Deliveries == 1 ? "delivery" : "deliveries")} made.";
        }

        /// <summary>The settings at a tab (0 Display … 5 Interface), over whatever is open.</summary>
        public void ShowSettings(int tab = 0)
        {
            if (!Open) Pause();
            var p = SettingsPage();
            p.Tab = Mathf.Clamp(tab, 0, SettingTabs.Length - 1);
            Push(p);
        }

        /// <summary>The credits over the start screen, at a work if one is named (as choosing Credits does; for tests and pictures).</summary>
        public void ShowCredits(string at = null)
        {
            if (!Open) ShowTitle();
            if (Top != "Credits") Push(CreditsPage());
            if (at == null) return;
            var p = pages[^1];
            int i = p.Items.FindIndex(r => !r.Header && r.Name == at);
            if (i < 0) return;
            Choose(i);
            // a page just made has no layout yet to scroll by: scroll once it has
            var e = p.Items[i].Element;
            EventCallback<GeometryChangedEvent> once = null;
            once = _ => { e.UnregisterCallback(once); list.ScrollTo(e); };
            e.RegisterCallback(once);
        }

        /// <summary>Stops the game and opens the pause menu, as Escape does.</summary>
        public void Pause()
        {
            if (Input != null && Input.IsGameplay) Input.Push(InputContext.Paused);
            if (!Open) Push(PausePage());
        }

        /// <summary>Stops the game and opens the map, as M or the View button does.</summary>
        public void ShowMap()
        {
            if (Input != null && Input.IsGameplay) Input.Push(InputContext.Paused);
            Push(new Page { Name = "Map", Make = _ => new List<Row>() });
        }

        Page PausePage() => new Page { Name = "Pause", Heading = "PAUSED", Make = _ => new List<Row>
        {
            Button("Resume", "Back to the game.", () => CloseAll()),
            Button("Map", "The city, your waypoint and the way to it.", () => Push(new Page { Name = "Map", Make = __ => new List<Row>() })),
            Character(),
            Button("Settings", "Display, graphics, sound, controls and the HUD.", () => Push(SettingsPage())),
            Quit(),
        } };

        Row Quit()
        {
            Row q = null;
            q = Button("Quit to desktop", "Your cash and deliveries are saved as you go. A parcel you carry is not.", () =>
            {
                if (!quitArmed) { quitArmed = true; q.Name = "Press again to quit"; q.Label.text = q.Name; return; }
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            return q;
        }

        static readonly string[] SettingTabs = { "Display", "Graphics", "Audio", "Controls", "Gameplay", "Interface" };

        Page SettingsPage()
        {
            padList = Ui.Pad;
            return new Page { Name = "Settings", Heading = "SETTINGS", Tabs = SettingTabs, Make = SettingRows };
        }

        List<Row> SettingRows(int tab)
        {
            var rows = new List<Row>();
            switch (tab)
            {
                case 0:
                    var sizes = Resolutions();
                    rows.Add(Choice("Resolution", "The size of the picture.", () => $"{Width} × {Height}", d =>
                    {
                        int i = sizes.FindIndex(r => r.x == Width && r.y == Height);
                        var next = sizes[(Math.Max(0, i) + d + sizes.Count) % sizes.Count];
                        S.Width = next.x;
                        S.Height = next.y;
                    }));
                    rows.Add(Choice("Display mode", "Full screen takes the display; borderless sits over the desktop; windowed is a window.",
                        () => S.Mode == FullScreenMode.ExclusiveFullScreen ? "Full screen" : S.Mode == FullScreenMode.Windowed ? "Windowed" : "Borderless",
                        d => S.Mode = Cycle(new[] { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed }, S.Mode, d)));
                    rows.Add(Toggle("V-Sync", "Frames wait for the display: no tearing.", () => S.VSync, v => S.VSync = v));
                    rows.Add(Choice("Frame limit", "The most frames a second, when V-Sync is off.", () => S.FrameLimit == 0 ? "Off" : S.FrameLimit.ToString(),
                        d => S.FrameLimit = Cycle(new[] { 0, 30, 60, 120, 144 }, S.FrameLimit, d)));
                    break;
                case 1:
                    rows.Add(Choice("Quality", "Shadows, draw distance and effects. High is the game as made.", () => GraphicsTier.Current.ToString(),
                        d => GraphicsTier.Apply(Cycle(new[] { Tier.Low, Tier.Medium, Tier.High }, GraphicsTier.Current, d), Camera.main, true)));
                    rows.Add(Slider("Field of view", "How wide the camera sees, in degrees.", () => S.FieldOfView.ToString("0") + "°", () => Mathf.InverseLerp(45, 90, S.FieldOfView),
                        d => S.FieldOfView = Mathf.Clamp(S.FieldOfView + 5 * d, 45, 90)));
                    break;
                case 2:
                    rows.Add(Volume("Master", "Everything.", () => S.Master, v => S.Master = v));
                    rows.Add(Volume("Vehicles", "Engines, horns and tyres.", () => S.Vehicles, v => S.Vehicles = v));
                    rows.Add(Volume("World", "The train and the city.", () => S.World, v => S.World = v));
                    rows.Add(Volume("Voices", "People speaking.", () => S.Voices, v => S.Voices = v));
                    break;
                case 3:
                    rows.Add(Speed("Mouse look speed", "How far the view turns for the mouse.", () => S.MouseLook, v => S.MouseLook = v, 3));
                    rows.Add(Speed("Gamepad look speed", "How fast the view turns for the stick.", () => S.PadLook, v => S.PadLook = v, 3));
                    rows.Add(Speed("Aiming look speed", "Look speed while aiming, as a share of the above.", () => S.AimLook, v => S.AimLook = v, 2));
                    rows.Add(Toggle("Invert look", "Up on the stick or mouse looks down.", () => S.InvertLook, v => S.InvertLook = v));
                    rows.Add(Toggle("Vibration", "The gamepad shakes when you shoot and are hurt.", () => S.Vibration, v => S.Vibration = v));
                    rows.Add(Choice("Aim", "Hold the aim button, or press it once to aim and again to stop.", () => S.AimToggle ? "Toggle" : "Hold", d => S.AimToggle = !S.AimToggle));
                    rows.Add(Choice("Sprint", "Hold the sprint button, or press it once.", () => S.SprintToggle ? "Toggle" : "Hold", d => S.SprintToggle = !S.SprintToggle));
                    rows.Add(Choice("Keys shown for", "Which device's keys and buttons are listed below.", () => padList ? "Gamepad" : "Keyboard & mouse", d => { padList = !padList; Refill(); }));
                    KeyRows(rows);
                    rows.Add(Button("Reset keys", "Every key and button back as it came.", () =>
                    {
                        S.Bindings = "";
                        Changed();
                        Refill();
                        help.text = "Keys and buttons reset.";
                    }));
                    break;
                case 4:
                    rows.Add(Choice("Speed in", "The car's speed on the HUD.", () => S.Miles ? "mph" : "km/h", d => S.Miles = !S.Miles));
                    rows.Add(Button("Restore all defaults", "Every setting but your keys back as it came.", () =>
                    {
                        var d = new GameSettings { Bindings = S.Bindings };
                        GameSettings.Use(d, Input);
                        dirty = true;
                        help.text = "Settings restored.";
                    }));
                    break;
                case 5:
                    rows.Add(Toggle("HUD", "Everything on screen in play.", () => S.Hud, v => S.Hud = v));
                    rows.Add(Toggle("Radar", "The map in the corner.", () => S.Radar, v => S.Radar = v));
                    rows.Add(Choice("Radar turns", "With the camera, or north always up.", () => S.NorthUp ? "North up" : "With view", d => S.NorthUp = !S.NorthUp));
                    rows.Add(Toggle("Crosshair", "The mark where a shot goes.", () => S.Crosshair, v => S.Crosshair = v));
                    rows.Add(Toggle("Button prompts", "What the use button does where you stand.", () => S.Prompts, v => S.Prompts = v));
                    rows.Add(Toggle("Notices", "Short messages, top left.", () => S.Notices, v => S.Notices = v));
                    rows.Add(Slider("HUD size", "Everything on the HUD larger or smaller.", () => Mathf.RoundToInt(S.HudSize * 100) + "%", () => Mathf.InverseLerp(0.75f, 1.25f, S.HudSize),
                        d => S.HudSize = Mathf.Clamp(Mathf.Round((S.HudSize + 0.05f * d) * 20) / 20, 0.75f, 1.25f)));
                    break;
            }
            return rows;
        }

        static int Width => S.Width > 0 ? S.Width : Screen.width;
        static int Height => S.Height > 0 ? S.Height : Screen.height;

        static List<Vector2Int> Resolutions()
        {
            var l = new List<Vector2Int>();
            foreach (var r in Screen.resolutions) { var v = new Vector2Int(r.width, r.height); if (!l.Contains(v)) l.Add(v); }
            var now = new Vector2Int(Width, Height);
            if (!l.Contains(now)) l.Add(now);
            l.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return l;
        }

        static T Cycle<T>(T[] all, T now, int d)
        {
            int i = Array.IndexOf(all, now);
            return all[(Math.Max(0, i) + d + all.Length) % all.Length];
        }

        // ---- the keys

        static readonly (string map, string action, string name)[] Bound =
        {
            ("OnFoot", "Move", "Move"), ("OnFoot", "Sprint", "Sprint"), ("OnFoot", "Jump", "Jump"), ("OnFoot", "Crouch", "Crouch"), ("OnFoot", "Interact", "Use / get in"),
            ("OnFoot", "Attack", "Shoot"), ("OnFoot", "Aim", "Aim"), ("OnFoot", "Reload", "Reload"), ("OnFoot", "NextWeapon", "Next weapon"), ("OnFoot", "PreviousWeapon", "Previous weapon"),
            ("OnFoot", "WeaponWheel", "Weapon wheel"), ("OnFoot", "Map", "Map"),
            ("Driving", "Throttle", "Accelerate"), ("Driving", "Brake", "Brake / reverse"), ("Driving", "Steer", "Steer"), ("Driving", "Handbrake", "Handbrake"), ("Driving", "Horn", "Horn"),
            ("Driving", "ExitVehicle", "Get out"), ("Driving", "CameraView", "Camera view"), ("Driving", "Headlamps", "Headlamps"), ("Driving", "IndicateLeft", "Indicate left"),
            ("Driving", "IndicateRight", "Indicate right"), ("Driving", "Hazards", "Hazard lights"), ("Driving", "Ignition", "Engine on / off"), ("Driving", "Map", "Map"),
            ("Global", "Pause", "Pause"),
        };

        void KeyRows(List<Row> rows)
        {
            if (Input == null) return;
            string scheme = padList ? "Gamepad" : "Keyboard&Mouse";
            string lastMap = null;
            var keys = new List<(Row row, string map, string path)>();
            foreach (var (mapName, actionName, name) in Bound)
            {
                var action = Input.Asset.FindAction(mapName + "/" + actionName);
                if (action == null) continue;
                if (mapName != lastMap)
                {
                    lastMap = mapName;
                    rows.Add(new Row { Header = true, Name = mapName == "OnFoot" ? "ON FOOT" : mapName == "Driving" ? "DRIVING" : "ANYWHERE" });
                }
                int composite = -1, seen = 0;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite) { if (composite >= 0) break; continue; }
                    if (b.groups == null || !b.groups.Contains(scheme)) continue;
                    if (b.isPartOfComposite) composite = i;
                    else if (composite >= 0) continue;
                    var path = b.effectivePath ?? "";
                    // sticks and the mouse's movement are axes, not something a key press can take
                    if (path.Contains("delta") || path.EndsWith("Stick") || path.EndsWith("/x") || path.EndsWith("/y")) continue;
                    string label = b.isPartOfComposite ? name + " " + Part(b.name) : seen > 0 ? name + " (second)" : name;
                    seen++;
                    int index = i;
                    var row = new Row { Name = label, Help = "Select, then press the new key or button. Escape keeps the old one.", Value = () => Ui.KeyAt(action, index) };
                    row.Press = () => StartRebind(row, action, index);
                    rows.Add(row);
                    keys.Add((row, mapName, path));
                }
            }
            // a key used twice where both act at once is marked
            foreach (var a in keys)
                foreach (var b in keys)
                    if (a.row != b.row && a.map == b.map && a.path == b.path) { a.row.Warn = true; a.row.Help = "Also used for " + b.row.Name + " here: one of them will not work as meant."; }
        }

        static string Part(string name) => name switch { "up" => "forward", "down" => "back", "left" => "left", "right" => "right", "negative" => "left", "positive" => "right", _ => name };

        void StartRebind(Row row, InputAction action, int index)
        {
            if (Input == null || rebind != null) return;
            rebinding = row;
            help.text = "Press the new key or button now. Escape keeps the old one.";
            rebind = Input.StartRebind(action, index, padList ? "Gamepad" : "Keyboard");
        }

        void Rebound()
        {
            if (rebind == null || !rebind.completed && !rebind.canceled) return;
            bool done = rebind.completed;
            rebind.Dispose();
            rebind = null;
            rebinding = null;
            quietUntil = Time.frameCount + 2;
            if (!done) { help.text = "Kept as it was."; return; }
            S.Bindings = Input.SaveBindingOverrides();
            dirty = true;
            int at = pages[^1].At;
            Refill();
            pages[^1].At = at;
            Choose(at);
        }

        // ---- kinds of row

        static Row Button(string name, string help, Action press) => new Row { Name = name, Help = help, Press = press };

        Row Choice(string name, string help, Func<string> value, Action<int> change) => new Row { Name = name, Help = help, Value = value, Change = d => { change(d); Changed(); } };

        Row Toggle(string name, string help, Func<bool> get, Action<bool> set) => Choice(name, help, () => get() ? "On" : "Off", _ => set(!get()));

        Row Slider(string name, string help, Func<string> value, Func<float> share, Action<int> change)
        {
            var r = Choice(name, help, value, change);
            r.Share = share;
            return r;
        }

        Row Volume(string name, string help, Func<float> get, Action<float> set) =>
            Slider(name, help, () => Mathf.RoundToInt(get() * 100) + "%", get, d => set(Mathf.Clamp01(Mathf.Round(get() * 10 + d) / 10)));

        Row Speed(string name, string help, Func<float> get, Action<float> set, float most) =>
            Slider(name, help, () => get().ToString("0.0"), () => Mathf.InverseLerp(0.1f, most, get()), d => set(Mathf.Clamp(Mathf.Round((get() + 0.1f * d) * 10) / 10, 0.1f, most)));

        void Changed()
        {
            dirty = true;
            GameSettings.Use(S, Input);
        }

        // ---------------------------------------------------------------- showing

        void Push(Page p)
        {
            pages.Add(p);
            Show();
        }

        /// <summary>Back a screen; out of the menus from the first one (except the start screen, which only Play leaves).</summary>
        public void Back()
        {
            if (!Open || rebind != null) return;
            var top = pages[^1];
            if (top.Name == "Title" || pages.Count == 1 && top.Name == "Title") return;
            if (top.Name == "Settings") SaveIfChanged();
            pages.RemoveAt(pages.Count - 1);
            if (pages.Count == 0) { CloseAll(); return; }
            Show();
        }

        void SaveIfChanged()
        {
            if (!dirty) return;
            dirty = false;
            S.Save();
            Notice.Post("Settings saved");
        }

        /// <summary>Out of the menus, back to the game.</summary>
        public void CloseAll()
        {
            if (pages.Count > 0 && pages[^1].Name == "Settings") SaveIfChanged();
            pages.Clear();
            quitArmed = false;
            Ui.Show(root, false);
            if (Input != null) while (!Input.IsGameplay && Input.Pop()) { }
            Run(true);
        }

        void Run(bool on)
        {
            if (on && stopped) { Time.timeScale = 1; AudioListener.pause = false; stopped = false; }
            else if (!on && !stopped) { Time.timeScale = 0; AudioListener.pause = true; stopped = true; }
        }

        void Show()
        {
            var p = pages[^1];
            quitArmed = false;
            Ui.Show(root, true);
            bool title = p.Name == "Title", isMap = p.Name == "Map";
            // the game stops under every screen but the start screen
            Run(title || pages[0].Name == "Title");
            Ui.Show(titleBox, title);
            Ui.Show(root.Q("credit"), title);
            Ui.Show(mapRoot, isMap);
            Ui.Show(column, !isMap);
            backdrop.style.backgroundColor = Ui.Fade(Ui.Ink, title ? 0.45f : 0.72f);
            column.style.top = title ? 420 : 130;
            heading.text = p.Heading;
            Ui.Show(heading, !string.IsNullOrEmpty(p.Heading));
            tabRow.Clear();
            Ui.Show(tabRow, p.Tabs != null);
            if (p.Tabs != null)
                for (int i = 0; i < p.Tabs.Length; i++)
                {
                    var t = Ui.Text(p.Tabs[i].ToUpperInvariant(), 17, i == p.Tab ? Ui.Ink : Ui.Silver, true, TextAnchor.MiddleCenter);
                    t.pickingMode = PickingMode.Position;
                    t.userData = i;
                    t.style.paddingLeft = t.style.paddingRight = 14;
                    t.style.paddingTop = t.style.paddingBottom = 7;
                    t.style.marginRight = 6;
                    t.style.letterSpacing = 2;
                    t.style.backgroundColor = i == p.Tab ? Ui.White : Ui.Fade(Ui.Coal, 0.8f);
                    Ui.Round(t, 4);
                    tabRow.Add(t);
                }
            if (isMap) OpenMap();
            Refill();
            Hints();
        }

        void Refill()
        {
            var p = pages[^1];
            p.Items = p.Make(p.Tab);
            list.Clear();
            foreach (var r in p.Items) list.Add(Element(r));
            if (p.At >= p.Items.Count || p.Items.Count > 0 && p.Items[p.At].Header) p.At = Next(p, -1, 1);
            Choose(p.At);
        }

        VisualElement Element(Row r)
        {
            var e = new VisualElement { userData = r };
            r.Element = e;
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            if (r.Header)
            {
                e.pickingMode = PickingMode.Ignore;
                e.style.marginTop = 16;
                e.style.marginBottom = 4;
                e.style.paddingLeft = 4;
                var h = Ui.Text(r.Name, 15, Ui.Steel, true);
                h.style.letterSpacing = 3;
                e.Add(h);
                return e;
            }
            e.pickingMode = PickingMode.Position;
            e.style.paddingLeft = e.style.paddingRight = 18;
            e.style.marginBottom = 4;
            Ui.Round(e, 4);
            if (r.Under != null)
            {
                // two lines, both wrapping: the name, and under it the smaller line
                e.style.minHeight = 62;
                e.style.paddingTop = e.style.paddingBottom = 9;
                var lines = new VisualElement { pickingMode = PickingMode.Ignore };
                lines.style.flexGrow = 1;
                lines.style.flexShrink = 1;
                r.Label = Ui.Text(r.Name, 20, Ui.White);
                r.Label.style.whiteSpace = WhiteSpace.Normal;
                lines.Add(r.Label);
                r.UnderLabel = Ui.Text(r.Under, 16, Ui.Silver);
                r.UnderLabel.style.whiteSpace = WhiteSpace.Normal;
                r.UnderLabel.style.marginTop = 4;
                lines.Add(r.UnderLabel);
                e.Add(lines);
                Paint(r, false);
                return e;
            }
            e.style.height = 50;
            r.Label = Ui.Text(r.Name, 22, Ui.White);
            r.Label.style.flexGrow = 1;
            e.Add(r.Label);
            if (r.Share != null)
            {
                var bar = Ui.Box(Ui.Fade(Ui.Steel, 0.35f), 2);
                bar.style.width = 160;
                bar.style.height = 6;
                bar.style.marginRight = 16;
                r.Fill = Ui.Box(Ui.White, 2);
                r.Fill.style.height = 6;
                bar.Add(r.Fill);
                e.Add(bar);
            }
            if (r.Value != null)
            {
                r.Shown = Ui.Text("", 22, Ui.Silver, false, TextAnchor.MiddleRight);
                r.Shown.style.minWidth = 200;
                e.Add(r.Shown);
            }
            Paint(r, false);
            return e;
        }

        void Paint(Row r, bool chosen)
        {
            if (r.Header || r.Element == null) return;
            r.Element.style.backgroundColor = chosen ? Ui.White : Ui.Fade(Ui.Coal, 0.55f);
            r.Label.style.color = chosen ? Ui.Ink : Ui.White;
            r.Label.style.unityFontStyleAndWeight = chosen ? FontStyle.Bold : FontStyle.Normal;
            if (r.UnderLabel != null) r.UnderLabel.style.color = chosen ? Ui.Ink : Ui.Silver;
            if (r.Shown != null)
            {
                string v = r == rebinding ? "Press a key…" : r.Value();
                r.Shown.text = r.Change != null && chosen ? "<   " + v + "   >" : v;
                r.Shown.style.color = r.Warn ? Ui.Crimson : chosen ? Ui.Ink : Ui.Silver;
            }
            if (r.Fill != null)
            {
                r.Fill.style.width = Length.Percent(Mathf.Clamp01(r.Share()) * 100);
                r.Fill.style.backgroundColor = chosen ? Ui.Crimson : Ui.White;
            }
        }

        void Choose(int at)
        {
            var p = pages[^1];
            if (p.Items.Count == 0) { help.text = ""; return; }
            at = Mathf.Clamp(at, 0, p.Items.Count - 1);
            p.At = at;
            for (int i = 0; i < p.Items.Count; i++) Paint(p.Items[i], i == at);
            var r = p.Items[at];
            if (rebind == null) help.text = r.Help ?? "";
            if (r.Element != null && r.Element.panel != null) list.ScrollTo(r.Element);
        }

        static int Next(Page p, int from, int way)
        {
            int n = p.Items.Count;
            if (n == 0) return 0;
            for (int k = 1; k <= n; k++)
            {
                int i = ((from + way * k) % n + n) % n;
                if (!p.Items[i].Header) return i;
            }
            return 0;
        }

        void Hints()
        {
            hints.Clear();
            var p = pages[^1];
            bool pad = Ui.Pad;
            if (p.Name == "Map")
            {
                hints.Add(Ui.Hint(pad ? "A" : "LMB", Waypoint.Active ? "Set or clear waypoint" : "Set waypoint"));
                hints.Add(Ui.Hint(pad ? "LT / RT" : "Wheel", "Zoom"));
                hints.Add(Ui.Hint(pad ? "LS" : "Drag", "Move"));
                hints.Add(Ui.Hint(pad ? "B" : "Esc", "Back"));
                return;
            }
            if (p.Name == "Credits") hints.Add(Ui.Hint(pad ? "D-pad" : "Arrows", "Move"));
            else hints.Add(Ui.Hint(pad ? "A" : "Enter", "Select"));
            if (p.Name != "Title") hints.Add(Ui.Hint(pad ? "B" : "Esc", "Back"));
            if (p.Tabs != null) hints.Add(Ui.Hint(pad ? "LB / RB" : "Q / E", "Tabs"));
            if (p.Name == "Settings") hints.Add(Ui.Hint(pad ? "D-pad" : "A / D", "Change"));
        }

        // ---------------------------------------------------------------- input

        void Update()
        {
            if (Input == null || Document == null) return;
            bool cursorFree = Open || Application.isBatchMode;
            UnityEngine.Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = cursorFree;
            var c = Input.Context;
            // from play: Escape (GameInput pushes Paused), M or View for the map
            if (!Open)
            {
                if (c == InputContext.Paused) { Push(PausePage()); return; }
                if (c == InputContext.OnFoot && Input.OnFootMap.WasPressedThisFrame() || c == InputContext.Driving && Input.DrivingMap.WasPressedThisFrame()) ShowMap();
                return;
            }
            if (Input.IsGameplay) { pages.Clear(); Ui.Show(root, false); Run(true); return; } // something else took us out
            Rebound();
            if (rebind != null) { Paint(rebinding, true); return; }
            if (Time.frameCount <= quietUntil) return;
            var p = pages[^1];
            if (p.Name == "Map") { MapInput(); return; }
            if (Input.Cancel.WasPressedThisFrame()) { Back(); return; }
            if (p.Tabs != null)
            {
                int t = Input.NextTab.WasPressedThisFrame() ? 1 : Input.PreviousTab.WasPressedThisFrame() ? -1 : 0;
                if (t != 0) SetTab(p, (p.Tab + t + p.Tabs.Length) % p.Tabs.Length);
            }
            var nav = Input.Navigate.ReadValue<Vector2>();
            int v = nav.y > 0.5f ? -1 : nav.y < -0.5f ? 1 : 0, h = nav.x > 0.5f ? 1 : nav.x < -0.5f ? -1 : 0;
            int dir = v != 0 ? v * 10 : h;
            if (dir != 0 && (dir != repeatDir || Time.unscaledTime >= repeatAt))
            {
                repeatAt = Time.unscaledTime + (dir != repeatDir ? 0.4f : 0.08f);
                repeatDir = dir;
                if (v != 0) Choose(Next(p, p.At, v));
                else if (p.Items.Count > 0 && p.Items[p.At].Change != null) { p.Items[p.At].Change(h); Paint(p.Items[p.At], true); }
            }
            else if (dir == 0) repeatDir = 0;
            if (Input.Submit.WasPressedThisFrame()) Activate(p, 1);
            Pointer(p);
            if (Ui.Pad != hintsPad) { hintsPad = Ui.Pad; Hints(); }
        }

        bool hintsPad;

        void SetTab(Page p, int tab)
        {
            p.Tab = tab;
            p.At = 0;
            Show();
        }

        void Activate(Page p, int way)
        {
            if (p.Items.Count == 0) return;
            var r = p.Items[p.At];
            if (r.Press != null) r.Press();
            else if (r.Change != null) r.Change(way);
            if (pages.Count > 0 && pages[^1] == p && p.At < p.Items.Count) Paint(p.Items[p.At], true);
        }

        // The mouse: pointing chooses a row, a click presses it or changes it (left of its value back, right of it on), the wheel scrolls.
        void Pointer(Page p)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var at = mouse.position.ReadValue();
            bool moved = (at - mouseWas).sqrMagnitude > 1;
            mouseWas = at;
            bool click = mouse.leftButton.wasPressedThisFrame;
            if (!moved && !click) { Scroll(mouse); return; }
            var panel = Document.rootVisualElement.panel;
            if (panel == null) return;
            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(at.x, Screen.height - at.y));
            var picked = panel.Pick(point);
            while (picked != null && picked.userData == null) picked = picked.parent;
            if (picked?.userData is int tab && click) { SetTab(p, tab); return; }
            if (!(picked?.userData is Row r) || r.Header) { Scroll(mouse); return; }
            int i = p.Items.IndexOf(r);
            if (i < 0) return;
            if (moved && i != p.At) Choose(i);
            if (!click) return;
            Choose(i);
            if (r.Press != null) r.Press();
            else if (r.Change != null)
            {
                var local = r.Element.WorldToLocal(point);
                r.Change(local.x < r.Element.resolvedStyle.width - 130 ? -1 : 1);
                if (pages[^1] == p) Paint(r, true);
            }
        }

        void Scroll(Mouse mouse)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f) list.scrollOffset += new Vector2(0, -Mathf.Sign(wheel) * 60);
        }

        // ---------------------------------------------------------------- the map screen

        void OpenMap()
        {
            map.Map = CityMap.Active;
            map.Turn = 0;
            map.PerMetre = 0.5f;
            map.ShowNames(22);
            You(out double e, out double n);
            map.East = e;
            map.North = n;
            mapByMouse = false;
            mapCursorAt = new Vector2(-1, -1);
        }

        static bool You(out double east, out double north)
        {
            east = FreeCamera.StartEast;
            north = FreeCamera.StartNorth;
            var o = FloatingOrigin.Active;
            if (o == null || o.Target == null) return false;
            o.Offset.ToWorld(o.Target.position.x, o.Target.position.z, out east, out north);
            return true;
        }

        void MapInput()
        {
            if (Input.Cancel.WasPressedThisFrame()) { Back(); return; }
            if (Ui.Pad != hintsPad) { hintsPad = Ui.Pad; Hints(); }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float w = map.ViewWidth, h = map.ViewHeight;
            if (w <= 0) return;
            // zoom on the triggers or keys, or the wheel at the pointer
            float zoom = Input.Zoom.ReadValue<float>();
            var mouse = Mouse.current;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0;
            if (Mathf.Abs(zoom) > 0.05f) map.PerMetre = Mathf.Clamp(map.PerMetre * Mathf.Pow(2.5f, zoom * dt), 0.15f, 2.5f);
            if (Mathf.Abs(wheel) > 0.01f) map.PerMetre = Mathf.Clamp(map.PerMetre * (wheel > 0 ? 1.2f : 1 / 1.2f), 0.15f, 2.5f);
            // move with a stick or the keys, at a speed in screen pixels
            var pan = Input.Navigate.ReadValue<Vector2>() + Input.Pan.ReadValue<Vector2>();
            if (pan.sqrMagnitude > 0.04f)
            {
                mapByMouse = false;
                map.East += pan.x * 700 * dt / map.PerMetre;
                map.North += pan.y * 700 * dt / map.PerMetre;
            }
            Vector2 pointer = default;
            if (mouse != null)
            {
                var panel = Document.rootVisualElement.panel;
                var at = mouse.position.ReadValue();
                pointer = panel != null ? RuntimePanelUtils.ScreenToPanel(panel, new Vector2(at.x, Screen.height - at.y)) : at;
                if ((at - mouseWas).sqrMagnitude > 1) mapByMouse = true;
                if (dragging && mouse.leftButton.isPressed)
                {
                    var d = pointer - mapCursorAt;
                    map.East -= d.x / map.PerMetre;
                    map.North += d.y / map.PerMetre;
                }
                mouseWas = at;
            }
            // keep the map over the city
            if (map.Map != null)
            {
                map.East = Math.Clamp(map.East, map.Map.West, map.Map.West + map.Map.WidthMetres);
                map.North = Math.Clamp(map.North, map.Map.South, map.Map.South + map.Map.HeightMetres);
            }
            var cursor = mapByMouse ? pointer : new Vector2(w / 2, h / 2);
            bool pressed = Input.Submit.WasPressedThisFrame();
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) { dragging = true; dragFrom = pointer; }
            if (mouse != null && dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                if ((pointer - dragFrom).sqrMagnitude < 25) pressed = true; // a click, not a drag
            }
            mapCursorAt = pointer;
            mapCursor.style.translate = new Translate(cursor.x - 20, cursor.y - 20, 0);
            if (pressed) MarkAt(cursor);
            map.Refresh(Waypoint.Version);
            You(out double ye, out double yn);
            map.Place(mapYou, ye, yn, true, 12);
            if (Waypoint.Active && !Waypoint.ForJob) map.Place(mapWay, Waypoint.East, Waypoint.North, false);
            else Ui.Show(mapWay, false);
            var job = Jobs.Active;
            if (job != null && job.Marked) map.Place(mapJob, job.MarkEast, job.MarkNorth, true, 14);
            else Ui.Show(mapJob, false);
            int k = 0;
            if (Precinct != null)
                foreach (var o in Precinct.Officers)
                {
                    if (o == null || o.Dead || FloatingOrigin.Active == null) continue;
                    FloatingOrigin.Active.Offset.ToWorld(o.transform.position.x, o.transform.position.z, out double oe, out double on);
                    if (k == mapCops.Count) { var dot = Ui.Box(Ui.Police, 6); dot.style.position = Position.Absolute; dot.style.left = dot.style.top = 0; dot.style.width = dot.style.height = 12; Ui.Border(dot, 2, Ui.Ink); map.Add(dot); mapCops.Add(dot); }
                    map.Place(mapCops[k], oe, on, false);
                    k++;
                }
            for (int i = k; i < mapCops.Count; i++) Ui.Show(mapCops[i], false);
        }

        Vector2 dragFrom;

        /// <summary>Sets the waypoint at a point of the map screen, or clears it if it is already there.</summary>
        public void MarkAt(Vector2 view)
        {
            if (Waypoint.Active && (map.ToView(Waypoint.East, Waypoint.North) - view).magnitude < 24)
            {
                if (!Waypoint.ForJob) { Waypoint.Clear(); Notice.Post("Waypoint cleared"); } // a job's waypoint goes when the job does
            }
            else
            {
                map.FromView(view, out double e, out double n);
                Waypoint.Place(e, n);
                if (You(out double ye, out double yn)) Waypoint.Follow(ye, yn, true);
            }
            Hints();
        }
    }
}
