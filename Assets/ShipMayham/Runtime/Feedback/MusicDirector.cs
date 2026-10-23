using System.Collections;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Plays the adaptive music: four looping layers that run in step, whose volumes follow the voyage mood (calm harbor,
    /// steady sailing, tension when something is wrong). It installs itself, so scenes and prefabs need no edit, and it follows the
    /// music volume in the settings.
    /// </summary>
    [DefaultExecutionOrder(190)]
    public sealed class MusicDirector : MonoBehaviour
    {
        private const float LookupSeconds = .75f;

        private static MusicDirector instance;

        private readonly AudioSource[] sources = new AudioSource[MusicLogic.LayerCount];
        private readonly float[] current = new float[MusicLogic.LayerCount];
        private readonly float[] targets = new float[MusicLogic.LayerCount];
        private PrototypeSession session;
        private float nextLookup;
        private bool ready;

        /// <summary>The mood the director is currently mixing for (for tests and tools).</summary>
        public MusicMood Mood { get; private set; } = MusicMood.Harbor;

        /// <summary>Current volume of a layer before the music setting is applied.</summary>
        public float LayerVolume(MusicLayer layer) => current[(int)layer];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null || Application.isBatchMode && !Application.isEditor) return;
            var go = new GameObject("MusicDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicDirector>();
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        // The layers are synthesized one per frame so loading never hitches.
        private IEnumerator Start()
        {
            var clips = new AudioClip[MusicLogic.LayerCount];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = MusicSynth.ToClip("Music_" + (MusicLayer)i, MusicSynth.Build((MusicLayer)i));
                yield return null;
            }
            for (int i = 0; i < sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.clip = clips[i];
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.volume = 0;
                source.ignoreListenerPause = true;
                sources[i] = source;
            }
            // Started together so the layers stay locked to each other.
            for (int i = 0; i < sources.Length; i++) { sources[i].timeSamples = 0; sources[i].Play(); }
            ready = true;
        }

        private void Update()
        {
            if (!ready) return;
            if (session == null && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + LookupSeconds;
                session = FindAnyObjectByType<PrototypeSession>();
            }
            Mood = MusicLogic.MoodFor(ReadInput());
            MusicLogic.Targets(Mood, targets);
            MusicLogic.Step(current, targets, Time.unscaledDeltaTime);
            float gain = FeedbackSettings.MusicGain;
            for (int i = 0; i < sources.Length; i++) sources[i].volume = current[i] * gain;
        }

        private MusicInput ReadInput()
        {
            var input = new MusicInput();
            if (session == null || !session.IsSpawned || session.InLobby) return input;
            var voyage = session.Voyage.Value;
            var encounter = session.Encounter.Value;
            input.InVoyage = true;
            input.Phase = voyage.Phase;
            input.Leak = voyage.Leak != 0;
            input.PowerOut = encounter.PowerOut;
            input.Pirate = encounter.Pirate;
            input.ShipEventActive = session.ActiveEvent.Value.Phase == VoyageEventPhase.Active;
            return input;
        }
    }
}
