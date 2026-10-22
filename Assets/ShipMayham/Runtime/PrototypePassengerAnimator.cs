using UnityEngine;

namespace ShipMayham
{
    /// <summary>Cosmetic only: cross-fades each passenger Animator to the pose implied by the replicated state.</summary>
    [DefaultExecutionOrder(160)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypePassengerAnimator : MonoBehaviour
    {
        private const float FadeSeconds = .25f;

        public PrototypeView View;
        public Animator[] Animators = System.Array.Empty<Animator>();
        private PassengerPose[] current;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            current = new PassengerPose[Animators.Length];
            Desynchronize();
        }

        // Identical clips in lockstep look robotic: every body gets its own tempo, and its own phase once it first becomes active.
        private bool[] phased;

        private void Desynchronize()
        {
            phased = new bool[Animators.Length];
            foreach (var animator in Animators)
            {
                if (animator == null) continue;
                animator.speed = Random.Range(.92f, 1.08f);
            }
        }

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned) return;
            for (int i = 0; i < Animators.Length; i++)
            {
                var animator = Animators[i];
                if (animator == null || !animator.isActiveAndEnabled || i >= session.Passengers.Count) continue;
                if (!phased[i]) { phased[i] = true; animator.Play(0, 0, Random.value); }
                var passenger = session.Passengers[i];
                var cue = PassengerPresentation.CueFor(passenger, session.ActiveEvent.Value, session.Voyage.Value, i);
                var pose = PassengerPresentation.PoseFor(passenger, cue);
                if (pose == current[i]) continue;
                current[i] = pose;
                animator.CrossFadeInFixedTime(pose.ToString(), FadeSeconds, 0, Random.Range(0f, 1.5f));
            }
        }
    }
}
