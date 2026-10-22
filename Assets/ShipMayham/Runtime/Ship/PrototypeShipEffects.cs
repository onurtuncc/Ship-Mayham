using UnityEngine;

namespace ShipMayham
{
    /// <summary>Continuous ship particles: funnel smoke that grows with speed, booster flames, and smoke/fire when the hull is hurt.</summary>
    [DefaultExecutionOrder(215)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypeShipEffects : MonoBehaviour
    {
        public PrototypeView View;
        public FeedbackFx Fx;
        public Transform[] FunnelSmoke = System.Array.Empty<Transform>();
        public Transform[] BoosterNozzles = System.Array.Empty<Transform>();
        [Tooltip("Deck points (ship-local) where damage smoke and fire appear, worst first.")]
        public Vector3[] DamagePoints = { new Vector3(-2.5f, .7f, -3.5f), new Vector3(3f, .7f, 2.5f), new Vector3(0f, .7f, -7f) };

        private float smokeTimer, flameTimer, damageTimer;

        private void Awake() { if (View == null) View = GetComponent<PrototypeView>(); }

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned || Fx == null || Time.deltaTime <= 0) return;
            var voyage = session.Voyage.Value;
            float speed = voyage.Phase == VoyagePhase.Sailing ? voyage.Speed : 0;

            smokeTimer -= Time.deltaTime;
            if (smokeTimer <= 0)
            {
                smokeTimer = Mathf.Lerp(.9f, .25f, Mathf.Clamp01(speed / 8f));
                for (int i = 0; i < FunnelSmoke.Length; i++)
                    if (FunnelSmoke[i] != null && FunnelSmoke[i].gameObject.activeInHierarchy) Fx.Burst(FxKind.Smoke, FunnelSmoke[i].position, 1);
            }

            flameTimer -= Time.deltaTime;
            if (flameTimer <= 0 && speed > 1.5f)
            {
                flameTimer = .06f;
                for (int i = 0; i < BoosterNozzles.Length; i++)
                    if (BoosterNozzles[i] != null && BoosterNozzles[i].gameObject.activeInHierarchy) Fx.Burst(FxKind.Flame, BoosterNozzles[i].position, 1);
            }

            int stage = ShipProgression.DamageStage(voyage.Health);
            damageTimer -= Time.deltaTime;
            if (stage > 0 && damageTimer <= 0 && session.ShipRoot != null)
            {
                damageTimer = stage == 2 ? .22f : .55f;
                int points = Mathf.Min(DamagePoints.Length, stage == 2 ? 3 : 1);
                for (int i = 0; i < points; i++)
                {
                    Vector3 world = session.ShipRoot.TransformPoint(DamagePoints[i]);
                    Fx.Burst(FxKind.Smoke, world, 1);
                    if (stage == 2) { Fx.Burst(FxKind.Flame, world, 1); if (Random.value < .25f) Fx.Burst(FxKind.Sparkle, world, 2); }
                }
            }
        }
    }
}
