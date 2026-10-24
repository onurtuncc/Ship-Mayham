using UnityEditor;
using UnityEngine;

namespace ShipMayham.Editor
{
    // Mixamo clips and the rigged passenger FBX must import as Humanoid so clips retarget onto the passenger avatar.
    public sealed class HumanoidModelPostprocessor : AssetPostprocessor
    {
        private const string AnimationFolder = "Assets/ShipMayham/Art/Generated/Animations/";
        private const string RiggedPassengerPrefix = "Assets/ShipMayham/Art/Generated/Models/Character_Human";
        private static readonly string[] LoopingClips = {
            "Idle", "Walking", "SittingIdle", "CheeringWhileSitting", "Talking", "Terrified", "Carrying", "ButtonPushing", "VictoryIdle",
            "HipHopDancing", "SambaDancing", "StandingClap", "RifleAimingIdle", "LookingAround", "SittingClap", "SittingLaughing",
            "Running", "Drinking", "Angry", "SleepingIdle", "DizzyIdle", "CrouchIdle", "ClimbingLadder", "PullingARope", "Pushing", "RepairHammering", "SwimSurface", "TreadWater", "SwimPanic", "FishingIdle", "FishingReel", "Digging", "LockPicking"
        };

        public override uint GetVersion() => 6;

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            bool isClip = assetPath.StartsWith(AnimationFolder);
            bool isRiggedPassenger = assetPath.StartsWith(RiggedPassengerPrefix) && assetPath.EndsWith("_Rigged.fbx");
            if (!isClip && !isRiggedPassenger) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = isClip;
            if (isClip) importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        // defaultClipAnimations is only populated once the model has been read, i.e. in this later callback.
        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationFolder)) return;
            var importer = (ModelImporter)assetImporter;
            string clipName = System.IO.Path.GetFileNameWithoutExtension(assetPath).Replace("Mixamo_", "");
            bool loop = System.Array.IndexOf(LoopingClips, clipName) >= 0;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clipName;
                clip.loopTime = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
