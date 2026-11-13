// Who you can play (owner, 2026-11-13: "remove isaac, use one of our 20 character, so player can select whatever they wanna play, male or female"). The twenty
// people built in Blender (Docs/Characters/README.md), ten men and ten women, chosen on the start screen. The choice is kept with your progress (Progress.Character).
// Nobody else in the street wears your body: a walker of the crowd who would is left out while you are them (NpcCrowd), and a gang member or officer is given the next
// person of the same sex instead (Fighter).
using System;
using UnityEngine;

namespace Dad.Core
{
    public static class Cast
    {
        public static readonly string[] Men = { "npc_m01", "npc_m02", "npc_m03", "npc_m04", "npc_m05", "npc_m06", "npc_m07", "npc_m08", "npc_m09", "npc_m10" };
        public static readonly string[] Women = { "npc_f01", "npc_f02", "npc_f03", "npc_f04", "npc_f05", "npc_f06", "npc_f07", "npc_f08", "npc_f09", "npc_f10" };
        public static readonly string[] All = Join();

        /// <summary>Who you are before you choose: Man 8, in shirt and jeans.</summary>
        public const string First = "npc_m08";

        // What each wears, from their recipes (make_roster.py), in the order of All.
        static readonly string[] Wears =
        {
            "Plate carrier, T-shirt and cargo trousers", "Plate carrier, helmet and bandana mask", "Plate carrier, patrol cap and trousers",
            "Jacket, jeans, beanie and bandana mask", "Paramedic's uniform", "Coveralls and a knitted hat", "Polo shirt, trousers and patrol cap",
            "Shirt and jeans", "Polo shirt and jeans", "Suit",
            "Plate carrier, T-shirt and cargo trousers", "Plate carrier, helmet and cargo trousers", "Fisherman's sweater, cargo trousers and bandana mask",
            "Paramedic's uniform", "T-shirt, wool trousers and patrol cap", "Coveralls", "Long open cardigan and jeans",
            "Knitted sweater and wool trousers", "Sweater and jeans", "Jacket and skirt",
        };

        /// <summary>Who you are now: the model's name in Resources/People.</summary>
        public static string You { get; private set; } = First;
        /// <summary>Raised when you choose someone else.</summary>
        public static event Action Changed;

        static string[] Join()
        {
            var all = new string[Men.Length + Women.Length];
            Men.CopyTo(all, 0);
            Women.CopyTo(all, Men.Length);
            return all;
        }

        public static bool Has(string model) => Array.IndexOf(All, model) >= 0;
        public static bool IsWoman(string model) => Array.IndexOf(Women, model) >= 0;

        /// <summary>"Man 8", "Woman 3".</summary>
        public static string Name(string model)
        {
            int m = Array.IndexOf(Men, model), w = Array.IndexOf(Women, model);
            return m >= 0 ? "Man " + (m + 1) : w >= 0 ? "Woman " + (w + 1) : model;
        }

        public static string Clothes(string model)
        {
            int i = Array.IndexOf(All, model);
            return i >= 0 ? Wears[i] : "";
        }

        /// <summary>Be this person (an unknown name keeps who you are), kept at once in Progress. Player.Become also changes the body you have.</summary>
        public static void Use(string model)
        {
            if (!Has(model) || model == You) return;
            You = model;
            Progress.Current.Character = model;
            Progress.Save();
            Changed?.Invoke();
        }

        /// <summary>The next or previous of the twenty, round the list: men, then women.</summary>
        public static string Step(int way)
        {
            int i = Array.IndexOf(All, You);
            int n = All.Length;
            return All[((i + (way >= 0 ? 1 : -1)) % n + n) % n];
        }

        /// <summary>From the kept progress: who you were, or the first.</summary>
        public static void Read(Progress p)
        {
            string was = You;
            You = p != null && Has(p.Character) ? p.Character : First;
            if (You != was) Changed?.Invoke();
        }

        /// <summary>The body for someone else in the street: theirs, unless it is yours; then the next of the twenty of the same sex.</summary>
        public static string Other(string model)
        {
            if (model != You) return model;
            var list = IsWoman(model) ? Women : Men;
            int i = Array.IndexOf(list, model);
            return i >= 0 ? list[(i + 1) % list.Length] : model;
        }
    }
}
