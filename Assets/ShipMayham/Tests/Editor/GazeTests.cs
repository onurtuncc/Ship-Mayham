using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class GazeTests
    {
        private static readonly Vector3 Eye = new Vector3(0, 1.6f, 0);

        private static GazeCandidate Camera(Vector3 position) => new GazeCandidate { Id = GazeBrain.CameraId, Kind = GazeKind.Camera, Position = position };

        // Runs a brain for a while at 30 steps per second and reports how often each kind was the target, and the longest time the
        // aimed direction stayed put (beyond the small fixation jitter).
        private static Dictionary<GazeKind, float> Share(GazeBrain brain, Vector3 facing, bool moving, IReadOnlyList<GazeCandidate> candidates, float seconds, out float longestHold)
        {
            var counts = new Dictionary<GazeKind, int>();
            int steps = Mathf.RoundToInt(seconds * 30);
            Vector3 anchor = Vector3.zero; float holdStart = 0; longestHold = 0;
            for (int i = 0; i < steps; i++)
            {
                float now = i / 30f;
                Vector3 direction = (brain.Update(now, Eye, facing, moving, candidates) - Eye).normalized;
                counts[brain.CurrentKind] = (counts.TryGetValue(brain.CurrentKind, out int c) ? c : 0) + 1;
                if (i == 0 || Vector3.Angle(anchor, direction) > 4f) { longestHold = Mathf.Max(longestHold, now - holdStart); holdStart = now; anchor = direction; }
            }
            var share = new Dictionary<GazeKind, float>();
            foreach (var pair in counts) share[pair.Key] = (float)pair.Value / steps;
            return share;
        }

        private static float Of(Dictionary<GazeKind, float> share, GazeKind kind) => share.TryGetValue(kind, out float v) ? v : 0;

        [Test]
        public void ACharacterSometimesLooksAtThePlayerButNotAllTheTime()
        {
            var share = Share(new GazeBrain(1), Vector3.forward, false, new[] { Camera(Eye + new Vector3(0, 0, 4)) }, 600, out _);
            Assert.That(Of(share, GazeKind.Camera), Is.InRange(.2f, .65f), "Eye contact is frequent enough to notice and never constant.");
        }

        [Test]
        public void NobodyLooksAtAPlayerBehindThemOrFarAway()
        {
            var behind = Share(new GazeBrain(2), Vector3.forward, false, new[] { Camera(Eye + new Vector3(0, 0, -3)) }, 300, out _);
            Assert.AreEqual(0, Of(behind, GazeKind.Camera), "Pupils cannot turn that far.");
            var far = Share(new GazeBrain(3), Vector3.forward, false, new[] { Camera(Eye + new Vector3(0, 0, GazeBrain.CameraRange + 5)) }, 300, out _);
            Assert.AreEqual(0, Of(far, GazeKind.Camera));
        }

        [Test]
        public void WalkingCharactersLookWhereTheyAreGoing()
        {
            var still = Share(new GazeBrain(4), Vector3.forward, false, new GazeCandidate[0], 300, out _);
            var walking = Share(new GazeBrain(4), Vector3.forward, true, new GazeCandidate[0], 300, out _);
            Assert.AreEqual(0, Of(still, GazeKind.Ahead));
            Assert.Greater(Of(walking, GazeKind.Ahead), .3f);
        }

        [Test]
        public void NoTargetIsHeldForever()
        {
            Share(new GazeBrain(5), Vector3.forward, true, new[] { Camera(Eye + new Vector3(2, .6f, 3)) }, 300, out float longestHold);
            Assert.Less(longestHold, 5.5f, "Eyes keep moving: even a repeated look in the same direction never lasts more than two holds.");
        }

        [Test]
        public void TheAimedPointStaysInFrontOfTheCharacter()
        {
            var brain = new GazeBrain(6);
            var others = new[] { Camera(Eye + new Vector3(2, 0, 2)), new GazeCandidate { Id = 9, Kind = GazeKind.Character, Position = Eye + new Vector3(-3, 0, 1) } };
            for (int i = 0; i < 900; i++)
            {
                Vector3 point = brain.Update(i / 30f, Eye, Vector3.forward, i % 200 < 100, others);
                Assert.LessOrEqual(Vector3.Angle(Vector3.forward, point - Eye), GazeBrain.MaxAngleFromFacing + 5f, "step " + i);
            }
        }

        [Test]
        public void NeighboursAreGlancedAtWhenInRangeAndInView()
        {
            var near = new GazeCandidate { Id = 11, Kind = GazeKind.Character, Position = Eye + new Vector3(1.5f, 0, 2) };
            var far = new GazeCandidate { Id = 12, Kind = GazeKind.Character, Position = Eye + new Vector3(0, 0, GazeBrain.CharacterRange + 4) };
            Assert.Greater(Of(Share(new GazeBrain(7), Vector3.forward, false, new[] { near }, 300, out _), GazeKind.Character), .2f);
            Assert.AreEqual(0, Of(Share(new GazeBrain(7), Vector3.forward, false, new[] { far }, 300, out _), GazeKind.Character));
        }

        // ---------------------------------------------------------------- the authored pupils

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RiggedModelsCarryTwoMovablePupilsAtTheEyes(int variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ShipMayham/Art/Generated/Models/Character_Human{variant}_Rigged.fbx");
            Assert.NotNull(prefab);
            var instance = (GameObject)Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponent<Animator>();
                Assert.NotNull(animator, "Humanoid model has an Animator.");
                var eyes = CharacterEyes.TryCreate(animator);
                Assert.NotNull(eyes, "Pupil_L and Pupil_R exist as separate objects with a mesh.");
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                Assert.NotNull(head);
                foreach (var pupil in instance.GetComponentsInChildren<Transform>())
                {
                    if (pupil.name != CharacterEyes.LeftName && pupil.name != CharacterEyes.RightName) continue;
                    Assert.Less(Vector3.Distance(pupil.position, head.position), .45f, pupil.name + " hangs at the head. pupil " + pupil.position.ToString("F3") + " head " + head.position.ToString("F3") + " local " + pupil.localPosition.ToString("F3") + " rot " + pupil.localRotation.eulerAngles.ToString("F1") + " scale " + pupil.lossyScale.ToString("F3") + " parent " + pupil.parent.name);
                    Assert.IsTrue(pupil.IsChildOf(head), pupil.name + " follows head animation.");
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void PupilsTurnTowardsATargetAndStopAtTheEyeball()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShipMayham/Art/Generated/Models/Character_Human1_Rigged.fbx");
            var instance = (GameObject)Object.Instantiate(prefab);
            try
            {
                var eyes = CharacterEyes.TryCreate(instance.GetComponent<Animator>());
                Assert.NotNull(eyes);
                var left = FindPupil(instance, CharacterEyes.LeftName);
                Vector3 ahead = left.position + instance.transform.forward * 5;
                eyes.LookAt(ahead, 1f);
                Vector3 rest = Look(left.parent, left);

                Vector3 sideways = left.position + instance.transform.forward * 5 + instance.transform.right * 1.2f;       // about 13 degrees
                eyes.LookAt(sideways, 1f);
                Vector3 aimed = Look(left.parent, left);
                Assert.Less(Vector3.Angle(aimed, sideways - left.position), 3f, "Pupil points at a reachable target.");
                Assert.Greater(Vector3.Angle(aimed, rest), 5f, "and it actually moved.");

                eyes.LookAt(left.position - instance.transform.forward * 5, 1f);
                Assert.LessOrEqual(Vector3.Angle(Look(left.parent, left), rest), CharacterEyes.MaxAngle + .5f, "Cannot roll behind the head.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void SaccadesAreFastButNotInstant()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShipMayham/Art/Generated/Models/Character_Human1_Rigged.fbx");
            var instance = (GameObject)Object.Instantiate(prefab);
            try
            {
                var eyes = CharacterEyes.TryCreate(instance.GetComponent<Animator>());
                var left = FindPupil(instance, CharacterEyes.LeftName);
                eyes.LookAt(left.position + instance.transform.forward * 5, 1f);
                Vector3 before = Look(left.parent, left);
                Vector3 target = left.position + instance.transform.forward * 5 + instance.transform.right * 2.2f;           // about 24 degrees
                eyes.LookAt(target, 1 / 60f);
                float moved = Vector3.Angle(before, Look(left.parent, left));
                Assert.That(moved, Is.InRange(5f, CharacterEyes.SaccadeDegreesPerSecond / 60f + .5f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static Transform FindPupil(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == name) return t;
            return null;
        }

        // World direction in which the pupil mesh currently points away from its eyeball centre.
        private static Vector3 Look(Transform parent, Transform pupil)
        {
            var filter = pupil.GetComponent<MeshFilter>();
            return (pupil.TransformPoint(filter.sharedMesh.bounds.center) - pupil.position).normalized;
        }
    }
}
