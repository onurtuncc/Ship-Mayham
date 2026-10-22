using UnityEditor;
using UnityEngine;

namespace ShipMayham.Editor
{
    // Mixamo clips and the rigged passenger FBX must import as Humanoid so clips retarget onto the passenger avatar.
    public sealed class HumanoidModelPostprocessor : AssetPostprocessor
    {
        private const string AnimationFolder = "Assets/ShipMayham/Art/Generated/Animations/";
        private const string RiggedPassengerPrefix = "Assets/ShipMayham/Art/Generated/Models/Character_Human";
        private static readonly string[] LoopingClips = { "Idle", "Walking", "SittingIdle", "CheeringWhileSitting", "Talking", "Terrified", "Carrying" };

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            bool isClip = assetPath.StartsWith(AnimationFolder);
            bool isRiggedPassenger = assetPath.StartsWith(RiggedPassengerPrefix) && assetPath.EndsWith("_Rigged.fbx");
            if (!isClip && !isRiggedPassenger) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = isClip;
            importer.materialImportMode = isClip ? ModelImporterMaterialImportMode.None : importer.materialImportMode;
            if (!isClip) return;

            string clipName = System.IO.Path.GetFileNameWithoutExtension(assetPath).Replace("Mixamo_", "");
            var clips = importer.defaultClipAnimations;
            bool loop = System.Array.IndexOf(LoopingClips, clipName) >= 0;
            foreach (var clip in clips)
            {
                clip.name = clipName;
                clip.loopTime = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false;
            }
            importer.clipAnimations = clips;
        }
    }
}
