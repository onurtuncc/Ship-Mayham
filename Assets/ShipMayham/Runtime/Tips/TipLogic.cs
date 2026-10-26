using System.Text;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>First-time tips. The enum order is the priority order.</summary>
    public enum TipId : byte { Welcome, TakeHelm, Harbor, Leak, Pirates, Docking, ServeOrders, CheckTickets, Signals, Hazard, Ladder, Overboard, Activity, Dive, Explore }

    /// <summary>What the tip logic needs to know about the local crew member's situation.</summary>
    public struct TipContext
    {
        public bool InVoyage;
        public VoyagePhase Phase;
        public int Boarded;
        public float DistanceToPort;
        public bool AtHelm, AtCannon, OnShore, Carrying;
        public bool OpenOrders;
        public bool LookingAtPassenger;
        public bool Leak, PiratesNear, HazardWarning;
        public bool Swimming, NearLadder, NearActivity;
    }

    /// <summary>Pure rules of the onboarding tips: when each applies, what it says and how its key hints are written.</summary>
    public static class TipLogic
    {
        public const float DockingHintMeters = 60f;

        /// <summary>Urgent tips may interrupt a calmer one that is on screen.</summary>
        public static bool IsUrgent(TipId id) => id == TipId.Leak || id == TipId.Pirates || id == TipId.Hazard || id == TipId.Overboard;

        /// <summary>Priority order. The enum keeps the stored bit of each tip stable, so new tips are appended there and slotted in here.</summary>
        public static readonly TipId[] Order =
        {
            TipId.Welcome, TipId.TakeHelm, TipId.Overboard, TipId.Harbor, TipId.Leak, TipId.Pirates, TipId.Hazard, TipId.Ladder, TipId.Dive, TipId.Activity, TipId.Explore,
            TipId.Docking, TipId.ServeOrders, TipId.CheckTickets, TipId.Signals
        };

        /// <summary>The highest-priority tip that applies now and has not been seen yet, or null.</summary>
        public static TipId? Pick(in TipContext c, int seenMask)
        {
            if (!c.InVoyage) return null;
            foreach (TipId id in Order)
                if ((seenMask & (1 << (int)id)) == 0 && Applies(id, c)) return id;
            return null;
        }

        public static bool Applies(TipId id, in TipContext c)
        {
            bool aboard = !c.OnShore;
            bool boardingOrSailing = c.Phase == VoyagePhase.Boarding || c.Phase == VoyagePhase.Sailing;
            switch (id)
            {
                case TipId.Welcome: return c.Phase == VoyagePhase.Boarding && c.Boarded == 0 && aboard && !c.AtHelm;
                case TipId.TakeHelm: return c.Phase == VoyagePhase.Boarding && c.Boarded > 0 && aboard && !c.AtHelm;
                case TipId.Harbor: return c.OnShore && (c.Phase == VoyagePhase.Boarding || c.Phase == VoyagePhase.Arrived);
                case TipId.Leak: return c.Leak && c.Phase == VoyagePhase.Sailing && aboard;
                case TipId.Pirates: return c.PiratesNear && c.Phase == VoyagePhase.Sailing && aboard && !c.AtCannon;
                case TipId.Hazard: return c.HazardWarning && c.Phase == VoyagePhase.Sailing && aboard;
                case TipId.Docking: return c.Phase == VoyagePhase.Sailing && c.AtHelm && c.DistanceToPort < DockingHintMeters;
                case TipId.ServeOrders: return boardingOrSailing && c.Boarded > 0 && aboard && c.OpenOrders && !c.Carrying && !c.AtHelm && !c.AtCannon;
                case TipId.CheckTickets: return boardingOrSailing && aboard && c.LookingAtPassenger && !c.Carrying;
                case TipId.Signals: return c.Phase == VoyagePhase.Sailing && aboard && !c.Swimming;
                case TipId.Overboard: return c.Swimming && c.Phase == VoyagePhase.Sailing;
                case TipId.Dive: return c.Swimming && c.Phase != VoyagePhase.Sailing;
                case TipId.Explore: return c.OnShore && !c.Swimming && c.Phase == VoyagePhase.Boarding;
                case TipId.Ladder: return c.NearLadder && !c.Swimming && (c.Phase == VoyagePhase.Boarding || c.Phase == VoyagePhase.Arrived);
                case TipId.Activity: return c.NearActivity && c.OnShore && !c.Swimming && !c.Carrying;
                default: return false;
            }
        }

        /// <summary>Tip text with <c>{Control}</c> placeholders, named after <see cref="HudControl"/>.</summary>
        public static string Template(TipId id)
        {
            switch (id)
            {
                case TipId.Welcome: return "Welcome aboard! Walk to the purser behind the desk and press {Interact} to board passengers.";
                case TipId.TakeHelm: return "Passengers aboard. Take the helm with {Interact}, then press {Jump} to depart. Everyone must be on the ship.";
                case TipId.Harbor: return "Welcome ashore! The stalls sell food, drinks and hull shields. The port office offers routes and jobs (press Esc to use the mouse).";
                case TipId.Leak: return "Leak! Patch it at the repair bench with {Interact}, four times. A quick-patch kit makes it instant.";
                case TipId.Pirates: return "Pirates! Man the cannon with {Interact} and fire with {Fire}. Carry cannonballs from the magazine to the cannon.";
                case TipId.Overboard: return "Overboard! Swim to a rope ladder on the hull and press {Interact} to climb out. Wait too long and you are hauled aboard for a small fine.";
                case TipId.Explore: return "The island goes on past the town: chests, buried treasure and fishing spots are out there, and the beach leads straight into the sea. Walk in to swim.";
                case TipId.Dive: return "Hold {Jump} to dive and let go to float up. Coins rest on the seabed: watch your breath, and stay inside the buoys.";
                case TipId.Ladder: return "Rope ladders let you dive in and swim: press {Interact} at the top. Sunken coins and a few passengers swimming back wait in the bay.";
                case TipId.Activity: return "Chests, sand and quay edges hide skill games: press {Interact} to pick a lock, dig for treasure or go fishing for the galley.";
                case TipId.Hazard: return "Hazard! Steer around the rocks of a shoal. Before a rogue wave hits, turn the ship's bow toward it.";
                case TipId.Docking: return "Almost there. Slow below 1 m/s and press {Jump} at the helm within 12 m of the pier to dock.";
                case TipId.ServeOrders: return "Passengers show what they want above their heads. Take it from its counter with {Interact}, then hand it over while looking at them.";
                case TipId.CheckTickets: return "{Check} checks a passenger's ticket (press again to collect an unpaid fare). {Carry} lifts them.";
                case TipId.Signals: return "Need help? Tap {Signal} to ping what you are looking at; hold it to open the emote wheel.";
                default: return string.Empty;
            }
        }

        /// <summary>Replaces <c>{Control}</c> placeholders with the key for the device in use, highlighted. Unknown names are left as written.</summary>
        public static string Render(string template, bool gamepad)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            var result = new StringBuilder(template.Length + 32);
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] == '{')
                {
                    int end = template.IndexOf('}', i);
                    if (end > i && System.Enum.TryParse(template.Substring(i + 1, end - i - 1), out HudControl control) && control != HudControl.None)
                    {
                        result.Append("<b><color=#FFC847>").Append(HudControls.Label(control, gamepad)).Append("</color></b>");
                        i = end;
                        continue;
                    }
                }
                result.Append(template[i]);
            }
            return result.ToString();
        }
    }

    /// <summary>Which tips this device has already shown, kept as a bit mask. Storage failures are non-fatal.</summary>
    public static class TipStore
    {
        public const string Key = "ship-mayham.tips.seen.v1";
        private static bool loaded;
        private static int mask;

        public static int SeenMask { get { Load(); return mask; } }

        public static bool HasSeen(TipId id) => (SeenMask & (1 << (int)id)) != 0;

        public static void MarkSeen(TipId id)
        {
            Load();
            int bit = 1 << (int)id;
            if ((mask & bit) != 0) return;
            mask |= bit;
            Persist();
        }

        public static void ResetAll()
        {
            Load();
            mask = 0;
            Persist();
        }

        /// <summary>Forgets the cached value so the next read goes to storage again (tests).</summary>
        public static void Reload() { loaded = false; mask = 0; }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            try { mask = PlayerPrefs.GetInt(Key, 0); }
            catch (System.Exception exception) { Debug.LogWarning("Could not read the seen tips: " + exception.Message); }
        }

        private static void Persist()
        {
            try { PlayerPrefs.SetInt(Key, mask); PlayerPrefs.Save(); }
            catch (System.Exception exception) { Debug.LogWarning("Could not save the seen tips: " + exception.Message); }
        }
    }
}
