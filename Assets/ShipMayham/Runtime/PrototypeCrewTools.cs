using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Puts the tool of the skill game a crew member is playing in their hand: the rod while fishing, the shovel while digging and
    /// the hammer over a leak. Follows the right hand of the animated body, so it needs no animation of its own. Never shown for the
    /// local player in first person, whose body is hidden.
    /// </summary>
    [DefaultExecutionOrder(230)]
    public sealed class PrototypeCrewTools : MonoBehaviour
    {
        public PrototypeView View;
        public PrototypeCrewAnimator CrewAnimator;
        public Transform[] Rods = System.Array.Empty<Transform>(), Shovels = System.Array.Empty<Transform>(), Hammers = System.Array.Empty<Transform>();

        // Where each tool sits in the hand bone's space: position and rotation chosen by eye against the Mixamo humanoid rig.
        public Vector3 RodPosition = new Vector3(0, .02f, 0), RodEuler = new Vector3(0, 0, 90);
        public Vector3 ShovelPosition = new Vector3(0, .02f, 0), ShovelEuler = new Vector3(0, 0, 90);
        public Vector3 HammerPosition = new Vector3(0, .02f, 0), HammerEuler = new Vector3(0, 0, 90);

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            bool live = session != null && session.IsSpawned && CrewAnimator != null;
            int local = live ? session.LocalCrewIndex : -1;
            int count = live ? Mathf.Min(CrewAnimator.Animators.Length, session.Crew.Count) : 0;
            for (int i = 0; i < Rods.Length || i < Shovels.Length || i < Hammers.Length; i++)
            {
                MiniGameKind doing = i < count && session.Crew[i].Active && !session.Crew[i].Swimming && i != local ? session.Crew[i].Doing : MiniGameKind.None;
                var animator = i < count ? CrewAnimator.Animators[i] : null;
                var hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                Place(Rods, i, doing == MiniGameKind.Fishing, hand, RodPosition, RodEuler);
                Place(Shovels, i, doing == MiniGameKind.Dig, hand, ShovelPosition, ShovelEuler);
                Place(Hammers, i, doing == MiniGameKind.Hammer, hand, HammerPosition, HammerEuler);
            }
        }

        private static void Place(Transform[] tools, int index, bool show, Transform hand, Vector3 position, Vector3 euler)
        {
            if (index >= tools.Length || tools[index] == null) return;
            var tool = tools[index];
            bool visible = show && hand != null;
            if (tool.gameObject.activeSelf != visible) tool.gameObject.SetActive(visible);
            if (!visible) return;
            tool.SetPositionAndRotation(hand.TransformPoint(position), hand.rotation * Quaternion.Euler(euler));
        }
    }
}
