using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// The movable pupils of one character. Each pupil object (Pupil_L / Pupil_R, authored with its origin at the eyeball centre and
    /// parented to the head bone) is aimed by rotating it, which slides the pupil over the eyeball. Presentation only.
    /// </summary>
    public sealed class CharacterEyes
    {
        public const string LeftName = "Pupil_L", RightName = "Pupil_R";
        /// <summary>How far a pupil may swing from straight ahead before it would leave the eyeball's front.</summary>
        public const float MaxAngle = 26f;
        /// <summary>Eyes snap between targets; this is the angular speed of that snap.</summary>
        public const float SaccadeDegreesPerSecond = 720f;

        public readonly Animator Animator;
        public readonly GazeBrain Brain;
        private readonly Transform[] pupils;
        private readonly Quaternion[] restRotation;
        private readonly Vector3[] restForward;            // in the pupil's parent space
        private readonly string[] movingStates = { "Walking", "Carrying" };

        public bool Valid
        {
            get
            {
                if (Animator == null) return false;
                for (int i = 0; i < pupils.Length; i++) if (pupils[i] == null) return false;
                return true;
            }
        }

        public bool Active => Valid && Animator.isActiveAndEnabled && Animator.gameObject.activeInHierarchy;
        public Vector3 EyePosition { get; private set; }
        public Vector3 Facing => Animator.transform.forward;

        private CharacterEyes(Animator animator, Transform left, Transform right, int seed)
        {
            Animator = animator;
            Brain = new GazeBrain(seed);
            pupils = new[] { left, right };
            restRotation = new Quaternion[2]; restForward = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                restRotation[i] = pupils[i].localRotation;
                restForward[i] = RestDirection(pupils[i]);
            }
        }

        /// <summary>Finds the pupils under an animated character; null when the model has none (older art, props).</summary>
        public static CharacterEyes TryCreate(Animator animator)
        {
            if (animator == null) return null;
            Transform left = null, right = null;
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == LeftName) left = t;
                else if (t.name == RightName) right = t;
            }
            if (left == null || right == null || RestDirection(left).sqrMagnitude < .5f) return null;
            return new CharacterEyes(animator, left, right, animator.GetInstanceID());
        }

        // The pupil mesh sits in front of its origin, so the direction from origin to the mesh centre is "straight ahead".
        private static Vector3 RestDirection(Transform pupil)
        {
            var filter = pupil.GetComponent<MeshFilter>();
            var skinned = pupil.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = filter != null ? filter.sharedMesh : skinned != null ? skinned.sharedMesh : null;
            if (mesh == null) return Vector3.zero;
            Vector3 center = mesh.bounds.center;
            return center.sqrMagnitude < 1e-6f ? Vector3.zero : pupil.localRotation * center.normalized;
        }

        public bool IsMoving()
        {
            var state = Animator.GetCurrentAnimatorStateInfo(0);
            for (int i = 0; i < movingStates.Length; i++) if (state.IsName(movingStates[i])) return true;
            return false;
        }

        /// <summary>Aims both pupils at a world point, each from its own eyeball (so close targets cross the eyes a little).</summary>
        public void LookAt(Vector3 worldPoint, float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                Transform pupil = pupils[i];
                Transform parent = pupil.parent;
                Vector3 toTarget = parent.InverseTransformDirection(worldPoint - pupil.position);
                Quaternion swing = Quaternion.identity;
                if (toTarget.sqrMagnitude > 1e-6f)
                    swing = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(restForward[i], toTarget.normalized), MaxAngle);
                Quaternion wanted = swing * restRotation[i];
                pupil.localRotation = Quaternion.RotateTowards(pupil.localRotation, wanted, SaccadeDegreesPerSecond * dt);
            }
            EyePosition = (pupils[0].position + pupils[1].position) * .5f;
        }

        /// <summary>Straight ahead again (hidden or reset characters).</summary>
        public void Rest()
        {
            for (int i = 0; i < 2; i++) if (pupils[i] != null) pupils[i].localRotation = restRotation[i];
        }

        public void CollectEyePosition()
        {
            if (Valid) EyePosition = (pupils[0].position + pupils[1].position) * .5f;
        }
    }
}
