using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Cosmetic world life: the hull rocking on the swell, a foam wake that scales with speed, and seagulls circling the ship.
    /// Everything follows replicated state and is local to each client.
    /// </summary>
    [DefaultExecutionOrder(210)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypeAmbientLife : MonoBehaviour
    {
        private const int Gulls = 5;

        public PrototypeView View;
        public FeedbackFx Fx;
        [Tooltip("Opaque lit material for the gulls; serialized so its shader is always in the build.")]
        public Material GullMaterial;
        private AudioSource voice;

        private Transform shipVisuals;
        private Vector3 visualsRestPosition; private Quaternion visualsRestRotation;
        private float foamTimer, callTimer;
        private Transform[] gulls, leftWings, rightWings;
        private float[] radius, height, angle, rate, phase;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            callTimer = Random.Range(6f, 12f);
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = 0;
        }

        private void Start()
        {
            var ship = View != null && View.Session != null ? View.Session.ShipRoot : null;
            if (ship != null)
            {
                shipVisuals = ship.Find("Visuals");
                if (shipVisuals != null) { visualsRestPosition = shipVisuals.localPosition; visualsRestRotation = shipVisuals.localRotation; }
            }
            if (GullMaterial != null) BuildGulls();
        }

        private void LateUpdate()
        {
            var session = View != null ? View.Session : null;
            if (session == null || !session.IsSpawned || session.ShipRoot == null) return;
            var voyage = session.Voyage.Value;
            float time = Time.time;
            float speed = Mathf.Clamp01(voyage.Speed / 10f);
            Rock(time, speed);
            Wake(session.ShipRoot, voyage.Speed);
            Fly(session.ShipRoot.position, time);
        }

        // Tiny amplitude on the hull model only: bodies and stations are separate children, so a large rock would visibly
        // slide the deck under them.
        private void Rock(float time, float speed)
        {
            if (shipVisuals == null) return;
            float roll = Mathf.Sin(time * .85f) * (.35f + .35f * speed) + Mathf.Sin(time * 1.7f + 1) * .08f;
            float pitch = Mathf.Sin(time * .62f + 1.3f) * (.22f + .25f * speed);
            shipVisuals.localRotation = visualsRestRotation * Quaternion.Euler(pitch, 0, roll);
            shipVisuals.localPosition = visualsRestPosition + Vector3.up * (Mathf.Sin(time * 1.1f) * .02f);
        }

        private void Wake(Transform ship, float speed)
        {
            if (Fx == null || speed < .6f) return;
            foamTimer -= Time.deltaTime * Mathf.Clamp(speed / 4f, .5f, 3f);
            if (foamTimer > 0) return;
            foamTimer = .22f;
            // Bow wave and stern wash at the waterline (world y just above the ocean mesh).
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 bow = ship.TransformPoint(new Vector3(side * 2.4f, 0, 11.5f));
                Vector3 stern = ship.TransformPoint(new Vector3(side * 5.4f, 0, -9.5f));
                bow.y = stern.y = .08f;
                Fx.Burst(FxKind.Foam, bow, 2);
                Fx.Burst(FxKind.Foam, stern, 2);
            }
        }

        private void Fly(Vector3 center, float time)
        {
            if (gulls == null) return;
            for (int i = 0; i < gulls.Length; i++)
            {
                angle[i] += rate[i] * Time.deltaTime;
                float a = angle[i];
                Vector3 position = center + new Vector3(Mathf.Cos(a) * radius[i], height[i] + Mathf.Sin(time * .7f + phase[i]) * 1.2f, Mathf.Sin(a) * radius[i]);
                Vector3 forward = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Sign(rate[i]);
                gulls[i].SetPositionAndRotation(position, Quaternion.LookRotation(forward) * Quaternion.Euler(0, 0, -Mathf.Sign(rate[i]) * 14));
                float flap = Mathf.Sin(time * 5.2f + phase[i]) * 32 * (.4f + .6f * Mathf.Max(0, Mathf.Sin(time * .45f + phase[i])));
                leftWings[i].localRotation = Quaternion.Euler(0, 0, flap); rightWings[i].localRotation = Quaternion.Euler(0, 0, -flap);
            }
            callTimer -= Time.deltaTime;
            if (callTimer <= 0 && voice != null)
            {
                callTimer = Random.Range(7f, 16f);
                var cam = View.Camera != null ? View.Camera.transform.position : center;
                float distance = Vector3.Distance(cam, gulls[0].position);
                voice.pitch = Random.Range(.9f, 1.2f);
                voice.PlayOneShot(ProceduralSfx.Get(SfxKind.Gull), Mathf.Clamp01(1f - distance / 70f) * .35f);
            }
        }

        // ---------------------------------------------------------------- gull geometry

        private void BuildGulls()
        {
            gulls = new Transform[Gulls]; leftWings = new Transform[Gulls]; rightWings = new Transform[Gulls];
            radius = new float[Gulls]; height = new float[Gulls]; angle = new float[Gulls]; rate = new float[Gulls]; phase = new float[Gulls];
            var body = BodyMesh(); var wing = WingMesh();
            for (int i = 0; i < Gulls; i++)
            {
                var root = new GameObject("Gull_" + i).transform;
                root.SetParent(transform, false);
                AddMesh(root, "Body", body, Vector3.zero, Quaternion.identity, Vector3.one);
                leftWings[i] = AddMesh(root, "WingL", wing, new Vector3(-.07f, .02f, 0), Quaternion.identity, new Vector3(-1, 1, 1));
                rightWings[i] = AddMesh(root, "WingR", wing, new Vector3(.07f, .02f, 0), Quaternion.identity, Vector3.one);
                root.localScale = Vector3.one * Random.Range(1.5f, 2.1f);
                gulls[i] = root;
                radius[i] = Random.Range(16f, 34f); height[i] = Random.Range(8f, 17f);
                angle[i] = Random.Range(0f, Mathf.PI * 2); rate[i] = Random.Range(.12f, .2f) * (i % 2 == 0 ? 1 : -1); phase[i] = Random.Range(0f, 6f);
            }
        }

        private Transform AddMesh(Transform parent, string name, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GullMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        // Stretched octahedron body, nose along +Z.
        private static Mesh BodyMesh()
        {
            var mesh = new Mesh { name = "GullBody" };
            var v = new[] { new Vector3(0, 0, .3f), new Vector3(0, 0, -.28f), new Vector3(.07f, 0, 0), new Vector3(-.07f, 0, 0), new Vector3(0, .06f, .02f), new Vector3(0, -.05f, 0) };
            mesh.vertices = v;
            mesh.triangles = new[] { 0, 2, 4, 0, 4, 3, 1, 4, 2, 1, 3, 4, 0, 5, 2, 0, 3, 5, 1, 2, 5, 1, 5, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // Double-sided swept wing along +X so one material and no culling tricks are needed.
        private static Mesh WingMesh()
        {
            var mesh = new Mesh { name = "GullWing" };
            var v = new[] { new Vector3(0, 0, .16f), new Vector3(0, 0, -.14f), new Vector3(.62f, 0, -.04f), new Vector3(.3f, .02f, .1f) };
            mesh.vertices = new[] { v[0], v[1], v[2], v[3], v[0], v[1], v[2], v[3] };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1, 4 + 0, 4 + 2, 4 + 3, 4 + 0, 4 + 1, 4 + 2 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
