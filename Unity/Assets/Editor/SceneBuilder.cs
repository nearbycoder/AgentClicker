using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentClicker.Office;
using AgentClicker.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AgentClicker.EditorTools
{
    /// <summary>
    /// Generates Assets/Scenes/Main.unity from the Blender models: the office layout, lights, camera,
    /// employee animator and all runtime references. Re-run any time models or layout change.
    ///   Tools/unity.sh scene
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ModelDir = "Assets/Art/Models/";
        const string AnimDir = "Assets/Art/Animation";
        const string MatDir = "Assets/Art/Materials";

        // Layout constants (Unity metres). The player sits at -Z looking toward the front wall at +Z.
        const float DeskTop = 0.75f;
        static readonly Vector3 SeatPos = new Vector3(0, 0, -0.13f);

        static Transform _office;

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        [MenuItem("Agent Clicker/Build Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.75f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.53f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.26f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var refs = new GameObject("Game").AddComponent<SceneRefs>();
            _office = new GameObject("Office").transform;
            refs.OfficeRoot = _office;

            BuildRoom(refs);
            BuildDesk(refs);
            BuildEmployee(refs);
            BuildWalls(refs);
            BuildFloor();
            BuildLighting(refs);
            BuildCamera(refs);
            BuildSystems(refs);

            MarkStatic();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Scene saved to " + ScenePath);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>
        /// Instantiates a model under a holder object that carries our placement. The FBX instance keeps
        /// its imported local transform (Unity bakes Blender's axis conversion into it, and animated
        /// models drive their own root), so we never overwrite it.
        /// </summary>
        static GameObject Place(string model, Vector3 pos, float yaw = 0, string condition = null, string showcase = null,
                                Transform parent = null, string name = null)
        {
            var holder = new GameObject(name ?? model);
            holder.transform.SetParent(parent ? parent : _office, false);
            holder.transform.localPosition = pos;
            holder.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
            if (asset == null)
            {
                Debug.LogError("[SceneBuilder] missing model " + model);
                return holder;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
            inst.name = model;
            if (condition != null || showcase != null)
            {
                var prop = holder.AddComponent<OfficeProp>();
                prop.Condition = condition ?? "always";
                prop.ShowcaseFor = showcase ?? "";
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                bool glass = r.sharedMaterials.Any(m => m && m.name.StartsWith("GLASS_"));
                r.shadowCastingMode = glass ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            return holder;
        }

        static Transform Child(GameObject go, string name) =>
            go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static TextMeshPro Label(Transform parent, string name, Vector3 localPos, Vector3 localEuler, Vector2 size, Color color,
                                 float maxSize = 10f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localPosition = localPos;
            rt.localRotation = Quaternion.Euler(localEuler);
            t.text = name;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.05f;
            t.fontSizeMax = maxSize;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            return t;
        }

        static Material ColorMaterial(string name, Color c, bool emissive = false)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            MaterialStyle.Apply(m, emissive ? "EMIT_" + name : "MATTE_" + name, c);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Light AddLight(string name, Transform parent, Vector3 localPos, LightType type, Color color, float intensity, float range,
                              LightShadows shadows = LightShadows.None)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;
            if (shadows != LightShadows.None) l.shadowStrength = 0.85f;
            return l;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        /// <summary>Static-batch the architecture and furniture that never moves or toggles.</summary>
        static void MarkStatic()
        {
            string[] names = { "Room", "Window", "Skyline", "Door", "CeilingFixture", "SideTable" };
            foreach (Transform t in _office)
            {
                if (!names.Contains(t.name) || t.GetComponent<OfficeProp>()) continue;
                foreach (var tr in t.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(tr.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }

        // ------------------------------------------------------------------ room
        static void BuildRoom(SceneRefs refs)
        {
            Place("room_shell", Vector3.zero, name: "Room");
            Place("window", Vector3.zero, name: "Window");
            // dropped well below the window so the sky shows above the rooftops
            var sky = Place("skyline", new Vector3(0, -18f, 0), name: "Skyline");
            refs.CityWindows = Child(sky, "CityWindows")?.GetComponent<Renderer>();
            foreach (var r in sky.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            var door = Place("door", new Vector3(3.06f, 0, -2.6f), name: "Door");
            refs.DoorSign = Label(door.transform, "DoorSign", new Vector3(-0.037f, 1.6f, 0f), new Vector3(0, 90, 0), new Vector2(0.2f, 0.06f), Color.white);

            var panels = new List<Renderer>();
            foreach (var z in new[] { 0.0f, -2.2f })
            {
                var fix = Place("ceiling_light", new Vector3(0, 3.0f, z), name: "CeilingFixture");
                panels.AddRange(fix.GetComponentsInChildren<Renderer>().Where(r => r.name == "Panel"));
                foreach (var r in fix.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            refs.CeilingPanels = panels.ToArray();

            // cubicle walls disappear when you're promoted to Senior Developer
            Place("cubicle_partition", new Vector3(-1.95f, 0, 0.3f), 90, "title<2", name: "PartitionL");
            Place("cubicle_partition", new Vector3(1.95f, 0, 0.3f), 90, "title<2", name: "PartitionR");
        }

        // ------------------------------------------------------------------ desk
        static void BuildDesk(SceneRefs refs)
        {
            float z = 0.5f;
            Place("desk_basic", new Vector3(0, 0, z), 0, "!office:exec_desk", name: "DeskBasic");
            Place("desk_executive", new Vector3(0, 0, z), 0, "office:exec_desk", "exec_desk", name: "DeskExecutive");

            // main monitor
            var main = Place("monitor", new Vector3(0, DeskTop, 0.62f), name: "MainMonitor");
            refs.MainScreen = Child(main, "Screen").GetComponent<Renderer>();

            // side monitors with dashboards
            var kinds = new List<SideScreenKind>();
            var screens = new List<Renderer>();
            void Side(GameObject m, SideScreenKind kind)
            {
                var screen = Child(m, "Screen").GetComponent<Renderer>();
                var s = m.AddComponent<SideScreen>();
                s.Screen = screen;
                s.Kind = kind;
                screens.Add(screen);
                kinds.Add(kind);
            }
            Side(Place("monitor", new Vector3(-0.68f, DeskTop, 0.56f), -25, "office:monitor2", "monitor2", name: "Monitor2"), SideScreenKind.AgentLog);
            Side(Place("monitor", new Vector3(0.68f, DeskTop, 0.56f), 25, "office:monitor3", "monitor3", name: "Monitor3"), SideScreenKind.Graph);
            Place("monitor_pole", new Vector3(0, DeskTop, 0.86f), 0, "office:monitor_wall", name: "MonitorPole");
            Side(Place("monitor_nostand", new Vector3(-0.33f, 1.57f, 0.82f), -8, "office:monitor_wall", "monitor_wall", name: "Monitor4"), SideScreenKind.LabStatus);
            Side(Place("monitor_nostand", new Vector3(0.33f, 1.57f, 0.82f), 8, "office:monitor_wall", name: "Monitor5"), SideScreenKind.Kanban);
            refs.SideScreens = screens.ToArray();
            refs.SideScreenKinds = kinds.ToArray();

            Place("keyboard_basic", new Vector3(0, DeskTop, 0.3f), 0, "!office:mech_keyboard", name: "KeyboardBasic");
            Place("keyboard_mech", new Vector3(0, DeskTop, 0.3f), 0, "office:mech_keyboard", "mech_keyboard", name: "KeyboardMech");
            Place("mouse", new Vector3(0.36f, DeskTop, 0.3f), 0, name: "Mouse");
            refs.DeskMug = Place("mug", new Vector3(-0.5f, DeskTop, 0.26f), -30, "office:mug", "mug", name: "Mug").transform;
            Place("rubber_duck", new Vector3(0.5f, DeskTop, 0.76f), 20, "office:duck", "duck", name: "RubberDuck");
            Place("plant_succulent", new Vector3(-0.36f, DeskTop, 0.76f), 0, "office:plant", "plant", name: "Succulent");
            Place("headphones", new Vector3(0.66f, DeskTop, 0.3f), -20, "office:headphones", "headphones", name: "Headphones");
            Place("stream_deck", new Vector3(-0.3f, DeskTop, 0.27f), 0, "office:macropad", "macropad", name: "MacroPad");
            Place("photo_frame", new Vector3(0.2f, DeskTop, 0.74f), 15, "day>=3", name: "PhotoFrame");
            Place("factory_toy", new Vector3(0.36f, DeskTop, 0.5f), -10, "factory", "factory", name: "FactoryToy")
                .AddComponent<Bob>();

            var notes = Place("sticky_notes", Vector3.zero, 0, "day>=2", name: "StickyNotes");
            notes.transform.SetParent(main.transform, false);
            notes.transform.localPosition = new Vector3(-0.335f, 0.29f, -0.017f);
            notes.transform.localRotation = Quaternion.Euler(0, 0, 90);

            var lamp = Place("lava_lamp", new Vector3(-0.7f, DeskTop, 0.82f), 0, "office:lava_lamp", "lava_lamp", name: "LavaLamp");
            var lava = lamp.AddComponent<LavaLamp>();
            lava.Blobs = new[] { Child(lamp, "Blob1"), Child(lamp, "Blob2"), Child(lamp, "Blob3") };
            AddLight("LampGlow", lamp.transform, new Vector3(0, 0.22f, -0.05f), LightType.Point, Hex("#FF7A45"), 0.35f, 0.8f);

            var phone = Place("desk_phone", new Vector3(-0.6f, DeskTop, 0.5f), 18, name: "DeskPhone");
            var phoneProp = phone.AddComponent<PhoneProp>();
            phoneProp.Handset = Child(phone, "Handset");
            phoneProp.Led = Child(phone, "Led")?.GetComponent<Renderer>();
            refs.Phone = phoneProp;

            var plate = Place("name_plate", new Vector3(-0.6f, DeskTop, 0.18f), 10, name: "NamePlate");
            refs.NamePlate = Label(plate.transform, "NamePlateText", new Vector3(0, 0.046f, -0.0105f), new Vector3(18, 0, 0),
                                   new Vector2(0.2f, 0.05f), Hex("#2B1D10"));

            // side cabinet with the espresso machine, the fridge, the rack
            Place("side_table", new Vector3(1.42f, 0, 0.85f), 0, name: "SideTable");
            Place("espresso_machine", new Vector3(1.42f, 0.75f, 0.82f), -15, "office:espresso", "espresso", name: "Espresso");
            Place("mini_fridge", new Vector3(1.42f, 0, 0.22f), -10, "office:minifridge", "minifridge", name: "MiniFridge");
            var rack = Place("server_rack", new Vector3(-1.45f, 0, 0.82f), 0, "office:homelab", "homelab", name: "ServerRack");
            var blink = rack.AddComponent<BlinkLeds>();
            blink.A = Child(rack, "LEDs_A")?.GetComponent<Renderer>();
            blink.B = Child(rack, "LEDs_B")?.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------------ employee & chairs
        static void BuildEmployee(SceneRefs refs)
        {
            var chairs = new GameObject("ChairRoot").transform;
            chairs.SetParent(_office, false);
            chairs.localPosition = SeatPos;
            chairs.localRotation = Quaternion.Euler(0, 180, 0);
            Place("chair_office", Vector3.zero, 0, "!office:gaming_chair&!office:recliner", parent: chairs, name: "ChairOffice");
            Place("chair_gaming", Vector3.zero, 0, "office:gaming_chair&!office:recliner", "gaming_chair", chairs, "ChairGaming");
            Place("chair_recliner", Vector3.zero, 0, "office:recliner", "recliner", chairs, "ChairRecliner");
            refs.ChairRoot = chairs;

            var emp = Place("employee", SeatPos, 180, name: "Employee");
            emp.transform.SetParent(null, true);
            var anim = emp.GetComponentInChildren<Animator>();
            if (anim == null) anim = emp.transform.GetChild(0).gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = BuildAnimator();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            refs.Employee = emp.transform;
            refs.EmployeeAnimator = anim;
            refs.HeldMug = Child(emp, "HeldMug")?.gameObject;
            refs.HeldHandset = Child(emp, "HeldHandset")?.gameObject;
            if (refs.HeldMug == null) Debug.LogWarning("[SceneBuilder] employee has no HeldMug");
        }

        static RuntimeAnimatorController BuildAnimator()
        {
            Directory.CreateDirectory(AnimDir);
            string path = AnimDir + "/Employee.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelDir + "employee.fbx").OfType<AnimationClip>()
                                     .Where(c => !c.name.StartsWith("__preview__")).ToList();
            if (clips.Count == 0) Debug.LogError("[SceneBuilder] employee.fbx has no animation clips");
            foreach (var clip in clips)
            {
                var st = sm.AddState(clip.name);
                st.motion = clip;
                if (clip.name == "Idle") sm.defaultState = st;
            }
            Debug.Log("[SceneBuilder] animator states: " + string.Join(", ", clips.Select(c => c.name)));
            return ctrl;
        }

        // ------------------------------------------------------------------ walls
        static void BuildWalls(SceneRefs refs)
        {
            const float front = 1.25f;
            var clock = Place("wall_clock", new Vector3(1.25f, 2.2f, front - 0.02f), name: "WallClock");
            refs.ClockHour = Child(clock, "HourHand");
            refs.ClockMinute = Child(clock, "MinuteHand");

            var cal = Place("calendar", new Vector3(-1.3f, 2.05f, front - 0.005f), name: "Calendar");
            refs.CalendarWeekday = Label(cal.transform, "Weekday", new Vector3(0, -0.06f, -0.007f), Vector3.zero, new Vector2(0.27f, 0.055f), Color.white);
            refs.CalendarDay = Label(cal.transform, "DayNumber", new Vector3(0, -0.28f, -0.004f), Vector3.zero, new Vector2(0.26f, 0.26f), Hex("#2B2B2B"));

            var neon = Place("neon_sign", new Vector3(0, 2.3f, front - 0.04f), 0, "office:neon", "neon", name: "NeonSign");
            var neonLight = AddLight("NeonGlow", neon.transform, new Vector3(0, -0.1f, -0.35f), LightType.Point, Hex("#FF3EA5"), 1.4f, 2.8f);
            var flick = neon.AddComponent<NeonFlicker>();
            flick.Tube = Child(neon, "Neon")?.GetComponent<Renderer>();
            flick.Glow = neonLight;

            var poster1 = Place("poster", new Vector3(-2.25f, 1.55f, front - 0.015f), 0, "day>=4", name: "Poster1");
            Child(poster1, "Canvas").GetComponent<Renderer>().sharedMaterial = ColorMaterial("PosterTeal", Hex("#2A9D8F"));
            refs.Poster1 = Label(poster1.transform, "Poster1Text", new Vector3(0, 0, -0.017f), Vector3.zero, new Vector2(0.48f, 0.68f), Color.white);

            var plaque = Place("plaque", new Vector3(2.2f, 1.65f, front - 0.012f), 0, "title>=1", name: "Plaque");
            refs.Plaque = Label(plaque.transform, "PlaqueText", new Vector3(0, 0, -0.017f), Vector3.zero, new Vector2(0.21f, 0.24f), Hex("#3A2A10"));

            // right wall (x = +3), facing -X
            Place("whiteboard", new Vector3(2.985f, 1.55f, -0.6f), 90, "office:whiteboard", "whiteboard", name: "Whiteboard");
            var poster2 = Place("poster", new Vector3(2.985f, 1.6f, 0.75f), 90, "day>=7", name: "Poster2");
            Child(poster2, "Canvas").GetComponent<Renderer>().sharedMaterial = ColorMaterial("PosterOrange", Hex("#F28C28"));
            refs.Poster2 = Label(poster2.transform, "Poster2Text", new Vector3(0, 0, -0.017f), Vector3.zero, new Vector2(0.48f, 0.68f), Hex("#1A1A1A"));

            // windowsill trophy
            Place("trophy", new Vector3(-2.9f, 0.85f, -0.3f), 90, "title>=5", name: "Trophy");
        }

        static void BuildFloor()
        {
            Place("rug", new Vector3(0, 0, -0.45f), 0, "title>=2", name: "Rug");
            Place("bookshelf", new Vector3(-2.8f, 0, -2.7f), -90, "title>=3", name: "Bookshelf");
            Place("floor_plant", new Vector3(-2.55f, 0, 0.95f), 0, "title>=3", name: "FloorPlant");
            Place("sofa", new Vector3(2.5f, 0, -0.6f), 90, "title>=4", name: "Sofa");
            Place("pizza_box", new Vector3(-1.05f, 0, -0.5f), 20, "day>=5&day<9", name: "PizzaBoxes");
            var trash = Place("trash_can", new Vector3(-1.0f, 0, 0.1f), 0, name: "TrashCan");
            for (int i = 1; i <= 6; i++)
            {
                var paper = Child(trash, "Paper" + i);
                if (paper == null) continue;
                paper.gameObject.AddComponent<OfficeProp>().Condition = $"day>={1 + i}";
            }
        }

        // ------------------------------------------------------------------ lighting
        static void BuildLighting(SceneRefs refs)
        {
            var lights = new GameObject("Lighting").transform;
            var sun = AddLight("Sun", lights, Vector3.zero, LightType.Directional, Hex("#FFF4E0"), 2f, 0, LightShadows.Soft);
            sun.transform.rotation = Quaternion.Euler(35, 80, 0);
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.3f;
            refs.Sun = sun;

            refs.CeilingLights = new[]
            {
                AddLight("CeilingA", lights, new Vector3(0, 2.75f, 0.1f), LightType.Point, Hex("#FFE6C7"), 2f, 7.5f),
                AddLight("CeilingB", lights, new Vector3(0, 2.75f, -2.1f), LightType.Point, Hex("#FFE6C7"), 2f, 7.5f),
            };
            refs.MonitorGlow = AddLight("MonitorGlow", lights, new Vector3(0, 1.15f, 0.42f), LightType.Point, Hex("#A8E4FF"), 0.6f, 1.6f);

            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(lights, false);
            probeGo.transform.position = new Vector3(0, 1.5f, -0.8f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(6.2f, 3.2f, 5f);
            probe.center = new Vector3(0, 0, 0.0f);
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.intensity = 0.8f;
            probeGo.AddComponent<ProbeRefresher>();

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ProjectSetup.UrpRendererPath);
            refs.Ssao = rendererData ? rendererData.rendererFeatures.FirstOrDefault(f => f is ScreenSpaceAmbientOcclusion) : null;

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectSetup.PostFxPath);
        }

        static void BuildCamera(SceneRefs refs)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#8EC5FF");
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None; // MSAA from the URP asset; SettingsApplier picks FXAA when MSAA is off
            camGo.transform.position = new Vector3(1.1f, 1.6f, -1.5f);
            camGo.transform.LookAt(new Vector3(0, 1.0f, 0.25f));
            refs.MainCamera = cam;

            var pivot = new GameObject("OfficeViewPivot").transform;
            pivot.position = new Vector3(0, 1.0f, 0.3f);
            refs.OfficeViewPivot = pivot;

            // phone-call close-up: in front of the employee's left shoulder, looking back at the handset side
            var call = new GameObject("CallView").transform;
            call.position = new Vector3(-1.0f, 1.42f, 0.62f);
            call.LookAt(new Vector3(0.02f, 1.08f, -0.16f));
            refs.CallView = call;

            var ending = new GameObject("EndingView").transform;
            ending.position = new Vector3(2.05f, 1.55f, -0.95f);
            ending.LookAt(new Vector3(-0.05f, 0.95f, -0.15f));
            refs.EndingView = ending;
        }

        static void BuildSystems(SceneRefs refs)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var gm = refs.gameObject.AddComponent<GameManager>();
            gm.Refs = refs;
        }
    }
}
