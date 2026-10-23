using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Brings every character with movable pupils to life: finds them, tells each what is around (the player's camera and the other
    /// characters) and lets its <see cref="GazeBrain"/> choose. It installs itself, so scenes and prefabs need no edit.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class GazeDirector : MonoBehaviour
    {
        private const float RescanSeconds = 2f;

        private static GazeDirector instance;

        private readonly Dictionary<Animator, CharacterEyes> known = new Dictionary<Animator, CharacterEyes>();
        private readonly HashSet<Animator> withoutEyes = new HashSet<Animator>();
        private readonly List<CharacterEyes> eyes = new List<CharacterEyes>();
        private readonly List<GazeCandidate> candidates = new List<GazeCandidate>();
        private readonly List<Animator> stale = new List<Animator>();
        private float nextScan;
        private Camera cachedCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            var go = new GameObject("GazeDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GazeDirector>();
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private void LateUpdate()
        {
            float now = Time.time, dt = Time.deltaTime;
            if (dt <= 0) return;
            if (now >= nextScan) { nextScan = now + RescanSeconds; Rescan(); }
            if (eyes.Count == 0) return;

            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
            candidates.Clear();
            if (cachedCamera != null)
                candidates.Add(new GazeCandidate { Id = GazeBrain.CameraId, Kind = GazeKind.Camera, Position = cachedCamera.transform.position });
            for (int i = 0; i < eyes.Count; i++)
            {
                var e = eyes[i];
                if (!e.Active) continue;
                e.CollectEyePosition();
                candidates.Add(new GazeCandidate { Id = e.Animator.GetInstanceID(), Kind = GazeKind.Character, Position = e.EyePosition });
            }

            for (int i = 0; i < eyes.Count; i++)
            {
                var e = eyes[i];
                if (!e.Active) continue;
                Vector3 point = e.Brain.Update(now, e.EyePosition, e.Facing, e.IsMoving(), candidates);
                e.LookAt(point, dt);
            }
        }

        private void Rescan()
        {
            // Forget characters that were destroyed (scene reload, session end).
            stale.Clear();
            foreach (var pair in known) if (pair.Key == null || !pair.Value.Valid) stale.Add(pair.Key);
            for (int i = 0; i < stale.Count; i++) known.Remove(stale[i]);
            withoutEyes.RemoveWhere(a => a == null);

            foreach (var animator in FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (known.ContainsKey(animator) || withoutEyes.Contains(animator)) continue;
                var created = CharacterEyes.TryCreate(animator);
                if (created == null) withoutEyes.Add(animator); else known.Add(animator, created);
            }
            eyes.Clear();
            foreach (var pair in known) eyes.Add(pair.Value);
        }
    }
}
