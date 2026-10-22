using UnityEngine;

namespace ShipMayham
{
    /// <summary>Authored model anchors and snapshot-driven effects, with no damage authority.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PrototypeCombatView : MonoBehaviour
    {
        public PrototypeSession Session;
        public PrototypeView View;
        public Transform CannonPivot, MuzzleAnchor, PirateRoot;
        public TextMesh PirateLabel;
        public Renderer PowerIndicator;
        public LineRenderer CannonTrace, PirateTrace;
        public Transform[] ReadyAmmoVisuals = System.Array.Empty<Transform>();
        public Transform CrewProjectileVisual, PirateProjectileVisual;
        private MaterialPropertyBlock properties;

        private void Awake() { properties = new MaterialPropertyBlock(); }

        private void LateUpdate()
        {
            if (Session == null || !Session.IsSpawned)
            {
                PirateRoot.gameObject.SetActive(false);
                CannonTrace.enabled = PirateTrace.enabled = false;
                if (CrewProjectileVisual != null) CrewProjectileVisual.gameObject.SetActive(false);
                if (PirateProjectileVisual != null) PirateProjectileVisual.gameObject.SetActive(false);
                foreach (var round in ReadyAmmoVisuals) if (round != null) round.gameObject.SetActive(false);
                return;
            }
            var e = Session.Encounter.Value;
            for (int i = 0; i < ReadyAmmoVisuals.Length; i++)
                if (ReadyAmmoVisuals[i] != null) ReadyAmmoVisuals[i].gameObject.SetActive(i < e.ReadyAmmo);
            CannonPivot.localRotation = Quaternion.Euler(e.CannonPitch, e.CannonYaw, 0);
            bool visible = e.Pirate == PiratePhase.Warning || e.Pirate == PiratePhase.Attacking;
            PirateRoot.gameObject.SetActive(visible);
            PirateRoot.position = new Vector3(e.PiratePosition.x, 0, e.PiratePosition.y);
            Vector3 towardShip = Session.ShipRoot.position - PirateRoot.position;
            if (towardShip.sqrMagnitude > .01f) PirateRoot.rotation = Quaternion.LookRotation(towardShip);
            PirateLabel.text = e.Pirate == PiratePhase.Warning ? $"PIRATES\nAttack in {e.WarningRemaining:0.0}s" : $"PIRATES {e.PirateHealth:0}%";
            PirateLabel.transform.rotation = View.Camera.transform.rotation;
            PowerIndicator.GetPropertyBlock(properties);
            Color color = e.PowerOut ? new Color(1, .35f, .05f) : new Color(.15f, .8f, .4f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            PowerIndicator.SetPropertyBlock(properties);
            // Active replicated flight, not a historical 90 m hitscan flash.
            ShowBall(e.CrewBall, CrewProjectileVisual, CannonTrace);
            ShowBall(e.PirateBall, PirateProjectileVisual, PirateTrace);
        }

        private static void ShowBall(CannonballState ball, Transform visual, LineRenderer trail)
        {
            if (visual != null)
            {
                visual.gameObject.SetActive(ball.Active);
                if (ball.Active) visual.position = ball.Position;
            }
            trail.enabled = ball.Active;
            if (!ball.Active) return;
            trail.SetPosition(0, ball.Position - ball.Velocity.normalized * .8f);
            trail.SetPosition(1, ball.Position);
        }
    }
}
