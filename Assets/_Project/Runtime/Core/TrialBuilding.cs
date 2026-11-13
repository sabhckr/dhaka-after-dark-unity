// A downloaded building standing in for one city model, to try it and measure it before any swap across the map (owner 2026-11-11, Docs/PRODUCTION/BUILDING_ASSETS_AUDIT.md section 7).
// Player command line (GameRoot adds it when the flag is present):
//   -dad-building <name>@<east>,<north>,<turn>
//     <name>: the prefab Resources/Buildings/<name>/<name> (Editor/BuildingImport).
//     <east>,<north>: the place of the city model it replaces, in metres, as its chunk stores it (two decimals). That model is not drawn (CityView.Hidden).
//     <turn>: degrees clockwise from north, seen from above. The old footprint's collider stays: the new building stands on it.
using System;
using System.Globalization;
using UnityEngine;

namespace Dad.Core
{
    public sealed class TrialBuilding : MonoBehaviour
    {
        public string Name;
        public double East, North;
        public float Turn;
        public GameObject Placed { get; private set; }

        public static TrialBuilding FromCommandLine(GameObject host)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-dad-building");
            if (i < 0 || i + 1 >= args.Length) return null;
            var parts = args[i + 1].Split('@');
            var f = parts.Length == 2 ? parts[1].Split(',') : null;
            if (f == null || f.Length != 3)
            {
                Debug.LogError("[Dad] -dad-building wants name@east,north,turn");
                return null;
            }
            var inv = CultureInfo.InvariantCulture;
            var t = host.AddComponent<TrialBuilding>();
            t.Name = parts[0];
            t.East = double.Parse(f[0], inv);
            t.North = double.Parse(f[1], inv);
            t.Turn = float.Parse(f[2], inv);
            CityView.Hidden.Add(((int)Math.Round(t.East * 100), (int)Math.Round(t.North * 100)));
            return t;
        }

        // In Start, once the floating origin is set up; after that it moves the building with the rest of the scene.
        void Start()
        {
            var prefab = Resources.Load<GameObject>("Buildings/" + Name + "/" + Name);
            if (prefab == null)
            {
                Debug.LogError("[Dad] no building Resources/Buildings/" + Name + "/" + Name);
                return;
            }
            var offset = FloatingOrigin.Active != null ? FloatingOrigin.Active.Offset : null;
            float x = 0, z = 0;
            if (offset != null) offset.ToScene(East, North, out x, out z);
            else Debug.LogError("[Dad] no floating origin: the trial building is put at the scene's origin");
            Placed = Instantiate(prefab, new Vector3(x, 0, z), Quaternion.Euler(0, Turn, 0));
            Placed.name = "Trial " + Name;
            Debug.Log($"[Dad] trial building {Name} at east {East:F2} north {North:F2}, turn {Turn:F1}");
        }
    }
}
