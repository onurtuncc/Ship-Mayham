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
        private int lastShot, lastEnemyShot, voyage;
        private float shotUntil, enemyShotUntil;
        private MaterialPropertyBlock properties;

        private void Awake() { properties = new MaterialPropertyBlock(); }

        private void LateUpdate()
        {
            if (Session == null || !Session.IsSpawned)
            {
                PirateRoot.gameObject.SetActive(false);
                CannonTrace.enabled = PirateTrace.enabled = false;
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
            // A late join or new voyage must not replay historical shots.
            if (voyage != Session.Voyage.Value.Voyage)
            {
                voyage = Session.Voyage.Value.Voyage;
                lastShot = e.ShotsFired;
                lastEnemyShot = e.PirateShotsFired;
                shotUntil = enemyShotUntil = 0;
            }
            if (lastShot != e.ShotsFired)
            {
                lastShot = e.ShotsFired;
                shotUntil = Time.time + .25f;
                CannonTrace.SetPosition(0, e.ShotFrom);
                CannonTrace.SetPosition(1, e.ShotTo);
            }
            if (lastEnemyShot != e.PirateShotsFired)
            {
                lastEnemyShot = e.PirateShotsFired;
                enemyShotUntil = Time.time + .35f;
                PirateTrace.SetPosition(0, e.EnemyShotFrom);
                PirateTrace.SetPosition(1, e.EnemyShotTo);
            }
            CannonTrace.enabled = Time.time < shotUntil;
            PirateTrace.enabled = Time.time < enemyShotUntil;
        }
    }
}
