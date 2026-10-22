using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/ShipMayham/Scenes/ShipPrototype.unity";
        private const string MaterialsPath = "Assets/ShipMayham/Art/Materials";

        [MenuItem("Ship Mayham/Open Prototype")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Create();
            RegisterScene();
        }

        /// <summary>First-time authoring only: existing scenes and materials are never overwritten.</summary>
        public static void Create()
        {
            if (File.Exists(ScenePath)) { Debug.Log("Prototype already exists: " + ScenePath); return; }
            Directory.CreateDirectory("Assets/ShipMayham/Scenes");
            Directory.CreateDirectory(MaterialsPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var wood = Material("Deck_Wood", new Color(.45f, .28f, .14f));
            var hull = Material("Hull_Navy", new Color(.06f, .16f, .23f));
            var trim = Material("Trim_Cream", new Color(.85f, .8f, .65f));
            var water = Material("Ocean_Teal", new Color(.04f, .32f, .4f));
            var food = Material("Service_Food_Orange", new Color(1, .48f, .12f));
            var drink = Material("Service_Drink_Blue", new Color(.15f, .6f, 1));
            var repair = Material("Repair_Green", new Color(.15f, .8f, .4f));
            var upgrade = Material("Upgrade_Purple", new Color(.65f, .3f, .9f));
            var warning = Material("Leak_Red", new Color(1, .16f, .1f));

            Transform environment = Root("Environment");
            Box("Ocean_Visual", environment, new Vector3(0, -1.1f, 110), new Vector3(240, .2f, 340), water);
            CreatePort("Port_Departure", environment, new Vector3(-14, 0, 0), wood, trim);
            CreatePort("Port_Destination", environment, new Vector3(-14, 0, 220), wood, trim);
            Transform marker = Root("Destination_DockingMarker", environment);
            marker.position = new Vector3(0, -.3f, 220);
            var ring = Primitive("Visuals", marker, PrimitiveType.Cylinder, Vector3.zero, new Vector3(24, .05f, 24), repair);
            for (int i = 0; i < 12; i++)
            {
                float z = 15 + i * 18;
                Primitive($"RouteBuoy_{i + 1:00}_Visual", environment, PrimitiveType.Sphere, new Vector3(i % 2 == 0 ? -9 : 9, 0, z), Vector3.one, trim);
            }

            Transform ship = Root("Ship_Player");
            Transform visuals = Root("Visuals", ship);
            Box("Hull_Primitive", visuals, new Vector3(0, -.4f, 0), new Vector3(12, 1.5f, 20), hull);
            Box("Deck_Primitive", visuals, new Vector3(0, .25f, 0), new Vector3(12, .5f, 20), wood);
            Box("Rail_Port_Primitive", visuals, new Vector3(-5.85f, 1, 0), new Vector3(.2f, 1, 20), trim);
            Box("Rail_Starboard_Primitive", visuals, new Vector3(5.85f, 1, 0), new Vector3(.2f, 1, 20), trim);
            Box("Rail_Bow_Primitive", visuals, new Vector3(0, 1, 9.85f), new Vector3(12, 1, .2f), trim);
            Box("Rail_Stern_Primitive", visuals, new Vector3(0, 1, -9.85f), new Vector3(12, 1, .2f), trim);
            Transform collision = Root("Collision", ship);
            var deck = Root("DeckCollider", collision).gameObject.AddComponent<BoxCollider>();
            deck.center = new Vector3(0, .25f, 0);
            deck.size = new Vector3(12, .5f, 20);

            Transform stationRoot = Root("Stations", ship);
            var stations = new[]
            {
                Station(stationRoot, StationKind.Manifest, "Board / next voyage", new Vector2(-4,-4), trim),
                Station(stationRoot, StationKind.Helm, "Helm", new Vector2(0,6.8f), hull),
                Station(stationRoot, StationKind.Food, "Food", new Vector2(-4,0), food),
                Station(stationRoot, StationKind.Drink, "Drink", new Vector2(-4,3), drink),
                Station(stationRoot, StationKind.Repair, "Repair leak", new Vector2(4,-3), repair),
                Station(stationRoot, StationKind.EngineUpgrade, "Engine upgrade", new Vector2(3.8f,-7.8f), upgrade),
                Station(stationRoot, StationKind.CapacityUpgrade, "Seat upgrade", new Vector2(0,-7.8f), upgrade),
                Station(stationRoot, StationKind.ComfortUpgrade, "Comfort upgrade", new Vector2(-3.8f,-7.8f), upgrade)
            };
            Transform passengers = Root("Passengers", ship);
            var seats = new Transform[8];
            var passengerVisuals = new Transform[8];
            var passengerLabels = new TextMesh[8];
            for (int i = 0; i < 8; i++)
            {
                seats[i] = Root($"Passenger_{i + 1:00}", passengers);
                seats[i].localPosition = new Vector3(i % 2 == 0 ? -1.9f : 1.9f, .5f, -1.5f + i / 2 * 2.1f);
                Transform seatVisual = Root("SeatVisuals", seats[i]);
                Box("Seat_Primitive", seatVisual, new Vector3(0, .2f, 0), new Vector3(.8f, .4f, .8f), trim);
                Root("InteractionAnchor", seats[i]);
                passengerVisuals[i] = Root("Visuals", seats[i]);
                Primitive("Passenger_Primitive", passengerVisuals[i], PrimitiveType.Capsule, new Vector3(0, 1, 0), new Vector3(.6f, .7f, .6f), trim);
                passengerVisuals[i].gameObject.SetActive(false);
                passengerLabels[i] = Label("RequestLabel", seats[i], new Vector3(0, 2.4f, 0), "Passenger", .09f);
                passengerLabels[i].gameObject.SetActive(false);
            }
            var crewRoots = new Transform[4];
            var crewLabels = new TextMesh[4];
            var carried = new Transform[4];
            Transform crewParent = Root("Crew", ship);
            Color[] colors = { new Color(1,.65f,.1f), new Color(.2f,.8f,1), new Color(.9f,.3f,.65f), new Color(.5f,1,.25f) };
            for (int i = 0; i < 4; i++)
            {
                crewRoots[i] = Root($"CrewSlot_{i + 1:00}", crewParent);
                crewRoots[i].localPosition = new Vector3(-1.5f + i, .5f, -6);
                var crewVisuals = Root("Visuals", crewRoots[i]);
                Primitive("Body_Primitive", crewVisuals, PrimitiveType.Capsule, new Vector3(0, .9f, 0), new Vector3(.65f, .9f, .65f), Material($"Crew_{i + 1:00}", colors[i]));
                carried[i] = Root("CarrySocket", crewRoots[i]);
                carried[i].localPosition = new Vector3(.55f, 1, .2f);
                Box("Visuals", carried[i], Vector3.zero, new Vector3(.35f, .25f, .35f), food);
                carried[i].gameObject.SetActive(false);
                crewLabels[i] = Label("NameLabel", crewRoots[i], new Vector3(0, 2.4f, 0), $"P{i + 1}", .08f);
                crewRoots[i].gameObject.SetActive(false);
            }
            Transform leak = Root("Leak_01", ship);
            leak.localPosition = new Vector3(4, .7f, -3);
            Primitive("Visuals", leak, PrimitiveType.Cylinder, new Vector3(0, -.1f, 0), new Vector3(2.7f, .03f, 2.7f), warning);
            leak.gameObject.SetActive(false);

            var light = Root("Lighting_Sun").gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .65f, .75f);
            var camera = Root("Camera_Prototype").gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f,.23f,.3f);
            camera.fieldOfView = 55;
            camera.farClipPlane = 600;
            camera.transform.position = new Vector3(0,24,-19);
            camera.transform.LookAt(Vector3.zero);
            camera.gameObject.AddComponent<AudioListener>();

            var networkRoot = Root("Systems_Network").gameObject;
            var transport = networkRoot.AddComponent<UnityTransport>();
            var manager = networkRoot.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.PlayerPrefab = null;
            manager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
            manager.NetworkConfig.TickRate = 20;
            manager.NetworkConfig.EnableSceneManagement = true;
            var sessionRoot = Root("Systems_Voyage").gameObject;
            sessionRoot.AddComponent<NetworkObject>();
            var session = sessionRoot.AddComponent<PrototypeSession>();
            session.ShipRoot = ship;
            session.Stations = stations;
            session.PassengerAnchors = seats;
            var presentation = Root("Systems_Presentation").gameObject;
            var view = presentation.AddComponent<PrototypeView>();
            view.Session = session;
            view.Camera = camera;
            view.CrewRoots = crewRoots;
            view.CrewLabels = crewLabels;
            view.CarriedItems = carried;
            view.PassengerVisuals = passengerVisuals;
            view.PassengerLabels = passengerLabels;
            view.LeakVisual = leak;
            view.DestinationMarker = marker;
            var hud = presentation.AddComponent<PrototypeHud>();
            hud.Manager = manager;
            hud.Session = session;
            hud.View = view;
            EnsurePhaseTwo(session, view);
            EnsurePhaseThree(view);
            EnsurePhaseFour(session, view);
            EnsureAmmoFlow(session, view);
            EnsurePassengerCues(view);
            EnsureDockSorting(view);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Created authored prototype scene: " + ScenePath);
        }

        [MenuItem("Ship Mayham/Build Prototype (Windows)")]
        public static void Build()
        {
            if (!File.Exists(ScenePath)) Create();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--prototype-validation") >= 0)
            {
                var validationScene = EditorSceneManager.OpenScene(ScenePath);
                PrepareLabels(validationScene);
                EditorSceneManager.SaveScene(validationScene);
                // Only the isolated validation project needs a pipeline; the real project already has one.
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/ValidationRenderer.asset");
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, "Assets/ValidationRenderer.asset");
                }
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/ValidationPipeline.asset");
                if (pipeline == null)
                {
                    pipeline = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(pipeline, "Assets/ValidationPipeline.asset");
                }
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/ShipPrototype/ShipPrototype.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Prototype build failed: " + report.summary.result);
        }

        private static void RegisterScene()
        {
            // NGO also needs an editor-play scene in Build Settings to synchronize late joins.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int index = scenes.FindIndex(s => s.path == ScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[index].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static PrototypeStation Station(Transform parent, StationKind kind, string display, Vector2 point, Material material)
        {
            Transform root = Root("Station_" + kind, parent);
            root.localPosition = new Vector3(point.x, .5f, point.y);
            var station = root.gameObject.AddComponent<PrototypeStation>();
            station.Kind = kind;
            station.DisplayName = display;
            station.InteractionAnchor = Root("InteractionAnchor", root);
            station.Visuals = Root("Visuals", root);
            Box("Counter_Primitive", station.Visuals, new Vector3(0,.5f,0), new Vector3(1,1,1), material);
            station.Label = Label("StationLabel", root, new Vector3(0,1.8f,0), StationLabel(kind), .09f);
            return station;
        }

        private static void CreatePort(string name, Transform parent, Vector3 position, Material wood, Material trim)
        {
            Transform root = Root(name, parent);
            root.localPosition = position;
            Transform visuals = Root("Visuals", root);
            Box("Pier_Primitive", visuals, new Vector3(0,-.1f,0), new Vector3(12,.8f,24), wood);
            Box("Terminal_Primitive", visuals, new Vector3(-3,2,0), new Vector3(5,4,8), trim);
            Label("PortLabel", root, new Vector3(0,6,0), name == "Port_Departure" ? "DEPARTURE" : "DESTINATION", .15f).transform.rotation = Quaternion.Euler(55,0,0);
        }

        private static Transform Root(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
            => Primitive(name, parent, PrimitiveType.Cube, position, scale, material);

        private static Transform Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        private static TextMesh Label(string name, Transform parent, Vector3 position, string text, float size)
        {
            Transform root = Root(name, parent);
            root.localPosition = position;
            var label = root.gameObject.AddComponent<TextMesh>();
            AssignFont(label);
            label.text = text;
            label.characterSize = size;
            label.fontSize = 64;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            return label;
        }

        private static void AssignFont(TextMesh label)
        {
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }

        private static string StationLabel(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Manifest: return "BOARD";
                case StationKind.EngineUpgrade: return "ENGINE";
                case StationKind.CapacityUpgrade: return "SEATS";
                case StationKind.ComfortUpgrade: return "COMFORT";
                case StationKind.Ammo: return "AMMO\nCarry to CANNON";
                default: return kind.ToString().ToUpperInvariant();
            }
        }

        private static void PrepareLabels(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<TextMesh>(true))
                {
                    AssignFont(label);
                    label.characterSize = label.name == "NameLabel" ? .08f : label.name == "PortLabel" ? .15f : label.name == "HarborLabel" ? .07f : .09f;
                    if (label.name == "PortLabel") label.text = label.transform.parent.name == "Port_Departure" ? "DEPARTURE" : "DESTINATION";
                    EditorUtility.SetDirty(label);
                }
                foreach (var station in root.GetComponentsInChildren<PrototypeStation>(true))
                    station.Label.text = StationLabel(station.Kind);
            }
        }

        /// <summary>Render the layout with sample crew. Sample activation is never saved.</summary>
        public static void CapturePreview()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            PrepareLabels(scene);
            EditorSceneManager.SaveScene(scene);
            var view = Object.FindAnyObjectByType<PrototypeView>();
            foreach (var crew in view.CrewRoots) crew.gameObject.SetActive(true);
            for (int i = 0; i < 4; i++)
            {
                view.PassengerVisuals[i].gameObject.SetActive(true);
                view.PassengerLabels[i].gameObject.SetActive(true);
                view.PassengerLabels[i].text = i % 2 == 0 ? "Food" : "Drink";
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--prototype-passenger-cues-preview") >= 0)
            {
                var cues = view.GetComponent<PrototypePassengerCueView>();
                if (cues == null || cues.Bags.Length < 4) throw new System.InvalidOperationException("Upgrade passenger cues before capturing their preview.");
                for (int i = 0; i < 4; i++) cues.Bags[i].gameObject.SetActive(true);
                cues.Bags[2].localPosition = new Vector3(.05f, .75f, .32f);
                cues.Bags[2].localRotation *= Quaternion.Euler(0, 0, -12);
                cues.Bodies[2].localRotation *= Quaternion.Euler(8, 0, 0);
                view.PassengerLabels[2].text = PassengerPresentation.CueText(PassengerCue.GuardingLuggage);
                view.Camera.transform.position = view.Session.ShipRoot.TransformPoint(new Vector3(-3.5f, 2.8f, 2.5f));
                view.Camera.transform.LookAt(view.Session.ShipRoot.TransformPoint(new Vector3(-1.4f, 1.8f, .2f)));
            }
            bool dockPreview = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--prototype-dock-sort-preview") >= 0;
            if (dockPreview)
            {
                Vector3 origin = VoyageSimulation.HarborOrigin(new VoyageState { Phase = VoyagePhase.Boarding });
                view.Camera.fieldOfView = view.FirstPersonFieldOfView;
                view.Camera.transform.position = origin + new Vector3(2, view.EyeHeight + VoyageSimulation.ShoreHeight, -8.5f);
                view.Camera.transform.LookAt(origin + new Vector3(0, 1, -7));
                foreach (var visual in view.PassengerVisuals) visual.gameObject.SetActive(false);
                foreach (var label in view.PassengerLabels) label.gameObject.SetActive(false);
                view.CrewRoots[0].Find("Visuals").gameObject.SetActive(false);
                Transform socket = view.CarriedItems[0], bag = socket.Find("Model_Suitcase");
                if (bag == null) throw new System.InvalidOperationException("Upgrade dock sorting before capturing its preview.");
                socket.gameObject.SetActive(true); bag.gameObject.SetActive(true);
                socket.Find("Visuals").gameObject.SetActive(false); socket.Find("Ammo_Visual").gameObject.SetActive(false);
                socket.SetPositionAndRotation(view.Camera.transform.TransformPoint(view.HeldItemOffset), view.Camera.transform.rotation);
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", VoyageSimulation.LuggageColor(ServiceItem.LuggageA));
                bag.Find("Tag_Visual").GetComponent<Renderer>().SetPropertyBlock(block);
            }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var label in root.GetComponentsInChildren<TextMesh>(true))
                {
                    AssignFont(label);
                    WorldLabelPresentation.UpdateLabel(label, view.Camera);
                    if (dockPreview && Vector3.Distance(label.transform.position, view.Camera.transform.position) > 10) label.gameObject.SetActive(false);
                }
            var target = new RenderTexture(1280, 800, 24);
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool batching = pipeline != null && pipeline.useSRPBatcher;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                // A one-off editor Camera.Render has no normal frame to initialize SRP material buffers.
                if (pipeline != null) pipeline.useSRPBatcher = false;
                view.Camera.targetTexture = target;
                view.Camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Builds");
                File.WriteAllBytes("Builds/ShipPrototype-Layout.png", texture.EncodeToPNG());
                Debug.Log("Prototype layout preview saved.");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                if (pipeline != null) pipeline.useSRPBatcher = batching;
                view.Camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }
        }

        private static Material Material(string name, Color color)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", .15f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
