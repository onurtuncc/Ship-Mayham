using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Shows the upgrades on the ship: seats unlock with capacity, smokestacks and stern boosters with the engine, carpet,
    /// planters and bunting with comfort. Modules that appear while the player watches pop in with a bounce; modules already
    /// owned when the scene starts (or a client joins) simply appear. Cosmetic only; reads replicated state.
    /// </summary>
    [DefaultExecutionOrder(190)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypeShipUpgradeView : MonoBehaviour
    {
        private const float PopSeconds = .55f;

        public PrototypeView View;
        public FeedbackFx Fx;
        [Tooltip("SeatVisuals of Passenger_01..08, in order.")] public Transform[] Seats = System.Array.Empty<Transform>();
        public Transform[] Funnels = System.Array.Empty<Transform>();
        public Transform[] Boosters = System.Array.Empty<Transform>();
        public Transform[] ComfortFittings = System.Array.Empty<Transform>();

        private struct Module { public Transform Root; public Vector3 Scale; public float Age; public bool Visible, Popping, Known; }
        private Module[] seats, funnels, boosters, comfort;
        private bool synced;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            seats = Capture(Seats); funnels = Capture(Funnels); boosters = Capture(Boosters); comfort = Capture(ComfortFittings);
        }

        private static Module[] Capture(Transform[] roots)
        {
            var modules = new Module[roots.Length];
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null) modules[i] = new Module { Root = roots[i], Scale = roots[i].localScale };
            return modules;
        }

        /// <summary>Applies the modules that should exist for the given levels; public so tests can drive it without a session.</summary>
        public void Apply(int engineLevel, int capacityLevel, int comfortLevel, bool animate)
        {
            if (seats == null) Awake();
            int unlockedSeats = ShipProgression.UnlockedSeats(capacityLevel), funnelCount = ShipProgression.Funnels(engineLevel);
            bool boost = ShipProgression.HasBoosters(engineLevel), cozy = ShipProgression.HasComfortFittings(comfortLevel);
            for (int i = 0; i < seats.Length; i++) Set(ref seats[i], i < unlockedSeats, animate);
            for (int i = 0; i < funnels.Length; i++) Set(ref funnels[i], i < funnelCount, animate);
            for (int i = 0; i < boosters.Length; i++) Set(ref boosters[i], boost, animate);
            for (int i = 0; i < comfort.Length; i++) Set(ref comfort[i], cozy, animate);
        }

        private void Set(ref Module module, bool visible, bool animate)
        {
            if (module.Root == null || module.Known && module.Visible == visible) return;
            module.Known = true;
            module.Visible = visible;
            module.Popping = visible && animate;
            module.Age = 0;
            module.Root.gameObject.SetActive(visible);
            module.Root.localScale = module.Popping ? Vector3.zero : module.Scale;
            if (module.Popping && Fx != null) Fx.Burst(FxKind.Sparkle, module.Root.position + Vector3.up * .8f, 10);
        }

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned) return;
            var voyage = session.Voyage.Value;
            Apply(voyage.EngineLevel, voyage.CapacityLevel, voyage.ComfortLevel, animate: synced);
            synced = true;
            Tick(seats); Tick(funnels); Tick(boosters); Tick(comfort);
        }

        private static void Tick(Module[] modules)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                if (!modules[i].Popping || modules[i].Root == null) continue;
                modules[i].Age += Time.deltaTime;
                float t = Mathf.Clamp01(modules[i].Age / PopSeconds);
                // Overshoot then settle: classic pop-in.
                float scale = t < .6f ? Mathf.Lerp(0, 1.18f, t / .6f) : Mathf.Lerp(1.18f, 1f, (t - .6f) / .4f);
                modules[i].Root.localScale = modules[i].Scale * scale;
                if (t >= 1) { modules[i].Popping = false; modules[i].Root.localScale = modules[i].Scale; }
            }
        }
    }
}
