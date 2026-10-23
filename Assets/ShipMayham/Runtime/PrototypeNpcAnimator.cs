using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Keeps the standing NPCs (the purser, the harbor residents) from freezing: they idle, now and then talk or glance around,
    /// and wave when the local player walks up. Presentation only; the gaze of their eyes is handled by <see cref="GazeDirector"/>.
    /// </summary>
    public sealed class PrototypeNpcAnimator : MonoBehaviour
    {
        public const string IdleState = "Idle", TalkingState = "Talking", LookingState = "LookingAround", WavingState = "Waving";
        public const float GreetDistance = 5f, GreetCooldown = 25f, WaveSeconds = 2.6f, FadeSeconds = .25f;

        public Camera Camera;
        public Animator[] Animators;

        private float[] nextChange, greetReadyAt, returnToIdleAt;
        private bool[] near;

        private void Awake() => Allocate();

        private void Allocate()
        {
            int count = Animators != null ? Animators.Length : 0;
            nextChange = new float[count]; greetReadyAt = new float[count]; returnToIdleAt = new float[count]; near = new bool[count];
            for (int i = 0; i < count; i++) nextChange[i] = Time.time + Random.Range(2f, 8f);
        }

        private void Update()
        {
            if (Animators == null) return;
            if (nextChange == null || nextChange.Length != Animators.Length) Allocate();
            float now = Time.time;
            Vector3 viewer = Camera != null ? Camera.transform.position : Vector3.zero;
            for (int i = 0; i < Animators.Length; i++)
            {
                var animator = Animators[i];
                if (animator == null || !animator.isActiveAndEnabled) continue;

                bool closeEnough = Camera != null && (animator.transform.position - viewer).sqrMagnitude < GreetDistance * GreetDistance;
                if (closeEnough && !near[i] && now >= greetReadyAt[i])
                {
                    greetReadyAt[i] = now + GreetCooldown;
                    Play(animator, WavingState, now, i, WaveSeconds);
                    Babble.Say(animator.transform.position + Vector3.up * 1.4f, i, BabbleMood.Happy, .6f);
                }
                near[i] = closeEnough;

                if (returnToIdleAt[i] > 0 && now >= returnToIdleAt[i])
                {
                    returnToIdleAt[i] = 0;
                    animator.CrossFadeInFixedTime(IdleState, FadeSeconds);
                    nextChange[i] = now + Random.Range(4f, 10f);
                }
                else if (returnToIdleAt[i] <= 0 && now >= nextChange[i])
                {
                    bool talk = Random.value < .6f;
                    Play(animator, talk ? TalkingState : LookingState, now, i, Random.Range(3f, 6f));
                }
            }
        }

        private void Play(Animator animator, string state, float now, int index, float seconds)
        {
            animator.CrossFadeInFixedTime(state, FadeSeconds);
            returnToIdleAt[index] = now + seconds;
        }
    }
}
