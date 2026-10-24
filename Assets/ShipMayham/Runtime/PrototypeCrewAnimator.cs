using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Cosmetic only: picks each crew member's Animator pose from replicated state and visible movement, layers short one-shot
    /// gestures on top, and fills idle time with an occasional look around so nobody freezes into a statue.
    /// </summary>
    [DefaultExecutionOrder(160)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypeCrewAnimator : MonoBehaviour
    {
        private const float FadeSeconds = .2f;
        private const CrewPose None = (CrewPose)255;
        private const float TurnDegreesPerSecond = 560f;

        public PrototypeView View;
        public Animator[] Animators = System.Array.Empty<Animator>();
        private CrewPose[] current;
        private float[] speed, oneShotUntil, nextFlavor, facing;
        private Transform[] bodies;
        private bool[] phased, wasSwimming;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            int n = Animators.Length;
            current = new CrewPose[n]; speed = new float[n];
            oneShotUntil = new float[n]; nextFlavor = new float[n]; phased = new bool[n]; wasSwimming = new bool[n]; facing = new float[n]; bodies = new Transform[n];
            // Identical clips in lockstep look robotic: every body gets its own tempo, and its own phase once it first becomes active.
            for (int i = 0; i < n; i++)
            {
                if (Animators[i] != null) Animators[i].speed = Random.Range(.92f, 1.08f);
                nextFlavor[i] = Random.Range(5f, 14f);
            }
        }

        /// <summary>Plays a gesture state for a short time, then returns to the continuous pose. Safe on missing animators.</summary>
        public void PlayOneShot(int index, CrewPose gesture, float seconds)
        {
            if (index < 0 || index >= Animators.Length || Animators[index] == null || !Animators[index].isActiveAndEnabled) return;
            oneShotUntil[index] = Time.time + seconds;
            Animators[index].CrossFadeInFixedTime(gesture.ToString(), FadeSeconds);
            current[index] = None;
        }

        public void PlayOneShotAll(CrewPose gesture, float seconds)
        {
            for (int i = 0; i < Animators.Length; i++) PlayOneShot(i, gesture, seconds);
        }

        // The body turns toward where it is going (quickly, but not instantly) instead of sliding sideways in a fixed pose.
        private void FaceTravelDirection(int i, Animator animator, Vector3 velocity, bool atHelm)
        {
            if (bodies[i] == null) bodies[i] = animator.transform.parent;
            if (bodies[i] == null) return;
            float target = atHelm ? 0 : velocity.sqrMagnitude > .09f ? Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg : facing[i];
            facing[i] = Mathf.MoveTowardsAngle(facing[i], target, TurnDegreesPerSecond * Time.deltaTime);
            bodies[i].localRotation = Quaternion.Euler(0, facing[i], 0);
        }

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned || Time.deltaTime <= 0) return;
            var phase = session.Voyage.Value.Phase;
            for (int i = 0; i < Animators.Length && i < View.CrewRoots.Length; i++)
            {
                // Velocity in the frame the crew member walks in, so a moving ship never makes a standing sailor jog on the spot.
                Vector3 velocity = View.CrewVelocity(i);
                velocity.y = 0;
                speed[i] = Mathf.Lerp(speed[i], velocity.magnitude, 1 - Mathf.Exp(-14 * Time.deltaTime));
                var animator = Animators[i];
                if (animator != null) FaceTravelDirection(i, animator, velocity, session.Crew.Count > i && session.Crew[i].AtHelm);
                if (animator == null || !animator.isActiveAndEnabled || i >= session.Crew.Count) continue;
                if (!phased[i]) { phased[i] = true; animator.Play(0, 0, Random.value); }
                bool swimming = session.Crew[i].Swimming;
                if (wasSwimming[i] && !swimming) PlayOneShot(i, CrewPose.ClimbOut, 1.5f);
                wasSwimming[i] = swimming;
                if (Time.time < oneShotUntil[i]) continue;
                var pose = CrewPresentation.PoseFor(session.Crew[i], speed[i], phase, i);
                if (pose == CrewPose.Idle && Time.time >= nextFlavor[i])
                {
                    nextFlavor[i] = Time.time + Random.Range(9f, 22f);
                    PlayOneShot(i, CrewPose.LookAround, 3.2f);
                    continue;
                }
                if (pose == current[i]) continue;
                current[i] = pose;
                animator.CrossFadeInFixedTime(pose.ToString(), FadeSeconds, 0, Random.Range(0f, 1.5f));
            }
        }
    }
}
