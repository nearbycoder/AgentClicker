using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AgentClicker.Office
{
    public enum SideScreenKind { AgentLog, Graph, LabStatus, Kanban }

    /// <summary>References into the generated scene. Populated by the editor SceneBuilder.</summary>
    public class SceneRefs : MonoBehaviour
    {
        [Header("Camera")]
        public Camera MainCamera;
        public Transform OfficeViewPivot;
        public Transform EndingView;

        [Header("Computer")]
        public Renderer MainScreen;
        public Renderer[] SideScreens;
        public SideScreenKind[] SideScreenKinds;

        [Header("Employee")]
        public Transform Employee;
        public Animator EmployeeAnimator;
        public Transform ChairRoot;
        public GameObject HeldMug, HeldHandset;
        public PhoneProp Phone;
        public Transform CallView;
        public Transform DeskMug;

        [Header("Lighting")]
        public Light Sun;
        public Light[] CeilingLights;
        public Light MonitorGlow;
        public Renderer CityWindows;
        public Renderer[] CeilingPanels;
        public ScriptableRendererFeature Ssao;

        [Header("Props")]
        public Transform ClockHour;
        public Transform ClockMinute;
        public Transform OfficeRoot;
        public TextMeshPro CalendarDay, CalendarWeekday, NamePlate, Plaque, Poster1, Poster2, DoorSign;

        /// <summary>
        /// World-space frame of a monitor "Screen" mesh. Imported child transforms carry Blender's axis
        /// conversion, so orientation comes from the model root (whose front faces -Z) and the size
        /// from the mesh's two largest dimensions.
        /// </summary>
        public static void ScreenFrame(Renderer screen, out Vector3 center, out Quaternion rotation, out Vector2 size)
        {
            center = screen.bounds.center;
            var root = screen.transform.parent != null ? screen.transform.parent : screen.transform;
            rotation = root.rotation;
            var mesh = screen.GetComponent<MeshFilter>().sharedMesh;
            Vector3 s = Vector3.Scale(mesh.bounds.size, screen.transform.lossyScale);
            float a = Mathf.Abs(s.x), b = Mathf.Abs(s.y), c = Mathf.Abs(s.z);
            // two largest of three, without allocating (this runs every frame in monitor view)
            float max = Mathf.Max(a, Mathf.Max(b, c));
            float min = Mathf.Min(a, Mathf.Min(b, c));
            size = new Vector2(max, a + b + c - max - min);
        }
    }
}
