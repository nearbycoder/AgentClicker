using AgentClicker.Core;
using AgentClicker.Office;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AgentClicker.Util
{
    /// <summary>
    /// The parts of Graphics fidelity that live in a volume rather than the pipeline asset: bloom quality and depth of
    /// field. A runtime volume above the game's own profile overrides only those parameters, so the look (bloom strength,
    /// grading, vignette) stays the profile's at every step. Depth of field follows what the camera is looking at and fades
    /// to nothing on the monitor, whose text has to stay sharp.
    /// </summary>
    public class FidelityEffects : MonoBehaviour
    {
        public static FidelityEffects Instance { get; private set; }

        SceneRefs _refs;
        CameraRig _rig;
        Volume _volume;
        Bloom _bloom;
        DepthOfField _dof;
        ReflectionProbe _probe;
        bool _dofOn;
        float _focus = 2.4f, _focal = FocalLength;

        // a portrait lens: Sam and his monitors sharp, the chair in front and the far wall soft
        const float FocalLength = 50f, Aperture = 2.8f, MonitorFocalLength = 1f;

        public void Init(SceneRefs refs, CameraRig rig)
        {
            Instance = this;
            _refs = refs;
            _rig = rig;
            var go = new GameObject("Fidelity Volume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Fidelity (runtime)";
            _bloom = profile.Add<Bloom>();
            _dof = profile.Add<DepthOfField>();
            _dof.mode.Override(DepthOfFieldMode.Off);
            _dof.focalLength.Override(FocalLength);
            _dof.aperture.Override(Aperture);
            _dof.bladeCount.Override(7);
            _dof.bladeCurvature.Override(1f);
            _dof.focusDistance.Override(_focus);
            _volume.sharedProfile = profile;
            _probe = FindAnyObjectByType<ReflectionProbe>();
        }

        public void Apply(FidelityStep p)
        {
            if (_volume == null) return;
            _bloom.highQualityFiltering.Override(p.BloomHighQuality);
            _bloom.downscale.Override(p.BloomQuarterRes ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
            _bloom.maxIterations.Override(p.BloomIterations);
            _dofOn = p.DepthOfField;
            _dof.mode.Override(_dofOn ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Off);
            if (_probe && _probe.resolution != p.ReflectionRes)
            {
                _probe.resolution = p.ReflectionRes;
                var refresher = _probe.GetComponent<ProbeRefresher>();
                if (refresher) refresher.RequestRender();
            }
            SnapFocus();
        }

        /// <summary>Jumps the lens to the current view (after a settings change, or for a screenshot of a held frame).</summary>
        public void SnapFocus()
        {
            if (!_dofOn || _refs == null) return;
            Target(out _focus, out _focal);
            _dof.focusDistance.Override(_focus);
            _dof.focalLength.Override(_focal);
        }

        void Target(out float focus, out float focal)
        {
            var cam = _refs.MainCamera;
            focus = Mathf.Max(0.3f, Vector3.Distance(cam.transform.position, _rig.FocusPoint));
            focal = _rig.Mode == CamMode.Monitor ? MonitorFocalLength : FocalLength;
        }

        void LateUpdate()
        {
            if (!_dofOn || _refs == null) return;
            Target(out float focus, out float focal);
            // the lens follows the camera's own moves; leaving the monitor eases the blur in, sitting down clears it fast
            float dt = Time.unscaledDeltaTime;
            _focus = Mathf.Lerp(_focus, focus, 1f - Mathf.Exp(-dt * 8f));
            _focal = Mathf.Lerp(_focal, focal, 1f - Mathf.Exp(-dt * (focal < _focal ? 10f : 2.5f)));
            _dof.focusDistance.Override(_focus);
            _dof.focalLength.Override(_focal);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_volume && _volume.sharedProfile) Destroy(_volume.sharedProfile);
        }
    }
}
