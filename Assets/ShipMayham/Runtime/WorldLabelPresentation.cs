using UnityEngine;

namespace ShipMayham
{
    public static class WorldLabelPresentation
    {
        /// <summary>Keep primitive TextMesh labels readable without filling the first-person view.</summary>
        public static void UpdateLabel(TextMesh label, Camera camera)
        {
            float distance = Vector3.Distance(label.transform.position, camera.transform.position);
            label.characterSize = .02f;
            label.transform.localScale = Vector3.one * Mathf.Clamp(distance / 4, .25f, 2);
            label.transform.rotation = camera.transform.rotation;
        }
    }
}
