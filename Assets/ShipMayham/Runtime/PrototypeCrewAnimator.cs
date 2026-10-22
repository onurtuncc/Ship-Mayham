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

        public PrototypeView View;
        public Animator[] Animators = System.Array.Empty<Animator>();
        private CrewPose[] current;
        private Vector3[] lastPosition;
        private float[] speed, oneShotUntil, nextFlavor;
        private bool[] phased;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            int n = Animators.Length;
            current = new CrewPose[n]; lastPosition = new Vector3[n]; speed = new float[n];
            oneShotUntil = new float[n]; nextFlavor = new float[n]; phased = new bool[n];
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

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned || Time.deltaTime <= 0) return;
            var phase = session.Voyage.Value.Phase;
            for (int i = 0; i < Animators.Length && i < View.CrewRoots.Length; i++)
            {
                Vector3 position = View.CrewRoots[i].position;
                // Ignore vertical motion (jumps) and teleports such as boarding or going ashore.
                Vector3 delta = position - lastPosition[i];
                delta.y = 0;
                float instant = delta.magnitude > 5 ? 0 : delta.magnitude / Time.deltaTime;
                speed[i] = Mathf.Lerp(speed[i], instant, 1 - Mathf.Exp(-12 * Time.deltaTime));
                lastPosition[i] = position;
                var animator = Animators[i];
                if (animator == null || !animator.isActiveAndEnabled || i >= session.Crew.Count) continue;
                if (!phased[i]) { phased[i] = true; animator.Play(0, 0, Random.value); }
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
