using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Cosmetic only: cross-fades each passenger Animator to the pose implied by the replicated state, plays short one-shot
    /// gestures (drinking, clapping) on request and sprinkles idle flavour so a calm deck never looks frozen.
    /// </summary>
    [DefaultExecutionOrder(160)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypePassengerAnimator : MonoBehaviour
    {
        private const float FadeSeconds = .25f, SitFadeSeconds = .35f, HandledFadeSeconds = .2f, LyingStartOffset = .3f;
        private const PassengerPose None = (PassengerPose)255;
        private static readonly PassengerPose[] Flavours = { PassengerPose.Laughing, PassengerPose.Clapping, PassengerPose.Fidgeting, PassengerPose.Laughing };

        public PrototypeView View;
        public Animator[] Animators = System.Array.Empty<Animator>();
        private PassengerPose[] current;
        private float[] oneShotUntil, nextFlavour;
        private bool[] phased;
        private PassengerMotion[] lastMotion;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            int n = Animators.Length;
            current = new PassengerPose[n]; oneShotUntil = new float[n]; nextFlavour = new float[n]; phased = new bool[n]; lastMotion = new PassengerMotion[n];
            // Identical clips in lockstep look robotic: every body gets its own tempo, and its own phase once it first becomes active.
            for (int i = 0; i < n; i++)
            {
                if (Animators[i] != null) Animators[i].speed = Random.Range(.92f, 1.08f);
                nextFlavour[i] = Random.Range(6f, 20f);
            }
        }

        /// <summary>Plays a gesture for a short time, then returns to the continuous pose. Safe on missing animators.</summary>
        public void PlayOneShot(int index, PassengerPose gesture, float seconds)
        {
            if (index < 0 || index >= Animators.Length || Animators[index] == null || !Animators[index].isActiveAndEnabled) return;
            oneShotUntil[index] = Time.time + seconds;
            Animators[index].CrossFadeInFixedTime(gesture.ToString(), FadeSeconds);
            current[index] = None;
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
                var motion = passenger.Motion;
                bool changed = motion != lastMotion[i];
                lastMotion[i] = motion;
                // A change of handling beats any gesture still playing: a thrown passenger must not keep clapping.
                if (changed && Time.time < oneShotUntil[i]) oneShotUntil[i] = 0;
                if (Time.time < oneShotUntil[i] && passenger.Routine == PassengerRoutine.Seated && motion == PassengerMotion.OnDeck) continue;

                var cue = PassengerPresentation.CueFor(passenger, session.ActiveEvent.Value, session.Voyage.Value, i);
                var pose = PassengerPresentation.PoseFor(passenger, cue);
                if (!animator.HasState(0, Animator.StringToHash(pose.ToString()))) pose = PassengerPose.Seated;
                if (pose == PassengerPose.Seated && passenger.Active && Time.time >= nextFlavour[i])
                {
                    nextFlavour[i] = Time.time + Random.Range(12f, 30f);
                    PlayOneShot(i, Flavours[Random.Range(0, Flavours.Length)], Random.Range(2.5f, 4f));
                    continue;
                }
                if (pose == current[i]) continue;
                current[i] = pose;
                if (pose == PassengerPose.Lying)
                    animator.CrossFadeInFixedTime(pose.ToString(), .08f, 0, LyingStartOffset);       // skip the clip's jump: the body is already falling
                else if (pose == PassengerPose.Seated && changed)
                    animator.CrossFadeInFixedTime(pose.ToString(), SitFadeSeconds, 0, 0);          // plops back onto the seat
                else if (pose == PassengerPose.Walking || pose == PassengerPose.Standing || pose == PassengerPose.Dizzy)
                    animator.CrossFadeInFixedTime(pose.ToString(), HandledFadeSeconds, 0, Random.Range(0f, .5f));
                else
                    animator.CrossFadeInFixedTime(pose.ToString(), FadeSeconds, 0, Random.Range(0f, 1.5f));
            }
        }
    }
}
