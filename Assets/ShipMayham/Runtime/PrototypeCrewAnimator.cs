using UnityEngine;

namespace ShipMayham
{
    /// <summary>Cosmetic only: picks each crew member's Animator pose from replicated carry state and visible movement.</summary>
    [DefaultExecutionOrder(160)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypeCrewAnimator : MonoBehaviour
    {
        private const float FadeSeconds = .2f;

        public PrototypeView View;
        public Animator[] Animators = System.Array.Empty<Animator>();
        private CrewPose[] current;
        private Vector3[] lastPosition;
        private float[] speed;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            current = new CrewPose[Animators.Length];
            Desynchronize();
            lastPosition = new Vector3[Animators.Length];
            speed = new float[Animators.Length];
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
            if (session == null || !session.IsSpawned || Time.deltaTime <= 0) return;
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
                var pose = CrewPresentation.PoseFor(session.Crew[i], speed[i]);
                if (pose == current[i]) continue;
                current[i] = pose;
                animator.CrossFadeInFixedTime(pose.ToString(), FadeSeconds, 0, Random.Range(0f, 1.5f));
            }
        }
    }
}
