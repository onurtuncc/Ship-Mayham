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
        // The battery upgrade's extra barrels (copies of the main one, beside it) and the rounds they fire.
        private readonly Transform[] extraBarrels = new Transform[CannonProgression.MaxBattery];
        private readonly Transform[] extraProjectiles = new Transform[CannonProgression.MaxBattery];
        private readonly LineRenderer[] extraTraces = new LineRenderer[CannonProgression.MaxBattery];
        private const float BarrelSpacing = .62f, BossScale = 1.7f;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            BuildExtras();
        }

        private void BuildExtras()
        {
            Transform model = CannonPivot != null && CannonPivot.childCount > 0 ? CannonPivot.GetChild(0) : null;
            for (int i = 0; i < extraBarrels.Length; i++)
            {
                if (model != null)
                {
                    var barrel = Instantiate(model, CannonPivot);
                    barrel.name = "Battery_Barrel_" + (i + 1);
                    barrel.localPosition = model.localPosition + new Vector3(i == 0 ? BarrelSpacing : -BarrelSpacing, -.08f, -.1f);
                    barrel.gameObject.SetActive(false);
                    extraBarrels[i] = barrel;
                }
                if (CrewProjectileVisual != null)
                {
                    extraProjectiles[i] = Instantiate(CrewProjectileVisual, CrewProjectileVisual.parent);
                    extraProjectiles[i].gameObject.SetActive(false);
                }
                if (CannonTrace != null)
                {
                    extraTraces[i] = Instantiate(CannonTrace, CannonTrace.transform.parent);
                    extraTraces[i].enabled = false;
                }
            }
        }

        private void LateUpdate()
        {
            if (Session == null || !Session.IsSpawned)
            {
                PirateRoot.gameObject.SetActive(false);
                CannonTrace.enabled = PirateTrace.enabled = false;
                if (CrewProjectileVisual != null) CrewProjectileVisual.gameObject.SetActive(false);
                if (PirateProjectileVisual != null) PirateProjectileVisual.gameObject.SetActive(false);
                for (int i = 0; i < extraBarrels.Length; i++) HideExtra(i);
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
            string raider = e.Boss ? "FLAGSHIP" : "PIRATES";
            PirateLabel.text = e.Pirate == PiratePhase.Warning ? $"{raider}\nAttack in {e.WarningRemaining:0.0}s" : $"{raider} {e.PirateHealth:0}%";
            PirateRoot.localScale = Vector3.one * (e.Boss ? BossScale : 1f);
            PirateLabel.transform.rotation = View.Camera.transform.rotation;
            PowerIndicator.GetPropertyBlock(properties);
            Color color = e.PowerOut ? new Color(1, .35f, .05f) : new Color(.15f, .8f, .4f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            PowerIndicator.SetPropertyBlock(properties);
            // Active replicated flight, not a historical 90 m hitscan flash.
            ShowBall(e.CrewBall, CrewProjectileVisual, CannonTrace);
            ShowBall(e.PirateBall, PirateProjectileVisual, PirateTrace);
            int barrels = CannonProgression.ExtraBarrels(Session.Voyage.Value.BatteryLevel);
            for (int i = 0; i < extraBarrels.Length; i++)
            {
                if (extraBarrels[i] != null) extraBarrels[i].gameObject.SetActive(i < barrels);
                if (extraProjectiles[i] != null && extraTraces[i] != null) ShowBall(i == 0 ? e.CrewBallB : e.CrewBallC, extraProjectiles[i], extraTraces[i]);
            }
        }

        private void HideExtra(int i)
        {
            if (extraBarrels[i] != null) extraBarrels[i].gameObject.SetActive(false);
            if (extraProjectiles[i] != null) extraProjectiles[i].gameObject.SetActive(false);
            if (extraTraces[i] != null) extraTraces[i].enabled = false;
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
