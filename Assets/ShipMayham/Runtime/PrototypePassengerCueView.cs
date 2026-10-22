using UnityEngine;

namespace ShipMayham
{
    /// <summary>Cosmetic poses only; never moves seats, interactions or authoritative NPC positions.</summary>
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypePassengerCueView : MonoBehaviour
    {
        public PrototypeView View;
        public Transform[] Bags = System.Array.Empty<Transform>();
        public Transform[] Bodies = System.Array.Empty<Transform>();
        private Vector3[] bagPositions;
        private Quaternion[] bagRotations, bodyRotations;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            bagPositions = new Vector3[Bags.Length];
            bagRotations = new Quaternion[Bags.Length];
            bodyRotations = new Quaternion[Bodies.Length];
            for (int i = 0; i < Bags.Length; i++)
                if (Bags[i] != null) { bagPositions[i] = Bags[i].localPosition; bagRotations[i] = Bags[i].localRotation; }
            for (int i = 0; i < Bodies.Length; i++) if (Bodies[i] != null) bodyRotations[i] = Bodies[i].localRotation;
        }

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            bool spawned = session != null && session.IsSpawned;
            for (int i = 0; i < Bags.Length; i++)
            {
                bool show = spawned && i < session.Passengers.Count && session.Passengers[i].Active && session.Passengers[i].Motion == PassengerMotion.OnDeck;
                if (Bags[i] != null) Bags[i].gameObject.SetActive(show);
                if (!show) { ResetPose(i); continue; }
                var cue = PassengerPresentation.CueFor(session.Passengers[i], session.ActiveEvent.Value, session.Voyage.Value, i);
                float wave = Mathf.Sin(session.Voyage.Value.Elapsed * 3 + i);
                bool guarding = cue == PassengerCue.GuardingLuggage;
                if (Bags[i] != null)
                {
                    Vector3 position = guarding ? new Vector3(.05f, .75f, .32f) : bagPositions[i];
                    Quaternion rotation = bagRotations[i] * (guarding ? Quaternion.Euler(0, 0, -12) : Quaternion.identity);
                    float blend = 1 - Mathf.Exp(-12 * Time.deltaTime);
                    Bags[i].localPosition = Vector3.Lerp(Bags[i].localPosition, position, blend);
                    Bags[i].localRotation = Quaternion.Slerp(Bags[i].localRotation, rotation, blend);
                }
                if (i < Bodies.Length && Bodies[i] != null)
                {
                    float roll = cue == PassengerCue.Impatient ? Mathf.Sin(session.Voyage.Value.Elapsed * 9 + i) * 5 :
                        cue == PassengerCue.CheckingPockets ? wave * 7 : wave * 1.5f;
                    Bodies[i].localRotation = bodyRotations[i] * Quaternion.Euler(guarding ? 8 : 0, 0, roll);
                }
            }
        }

        private void ResetPose(int i)
        {
            if (i < Bodies.Length && Bodies[i] != null) Bodies[i].localRotation = bodyRotations[i];
            if (Bags[i] == null) return;
            Bags[i].localPosition = bagPositions[i];
            Bags[i].localRotation = bagRotations[i];
        }

        private void OnDisable()
        {
            if (bagPositions == null) return;
            for (int i = 0; i < Bags.Length; i++)
            {
                ResetPose(i);
                if (Bags[i] != null) Bags[i].gameObject.SetActive(false);
            }
        }
    }
}
