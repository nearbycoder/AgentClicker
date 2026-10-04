using AgentClicker.Core;
using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>
    /// Chooses the employee's animation from what's happening in the game: typing speed follows your clicking,
    /// idle moments get small variations (coffee, thinking, glancing at the side monitors), and big events get
    /// one-shots (celebrate, facepalm, stretch). Feet go up once the Software Factory is built.
    /// </summary>
    public class EmployeeController : MonoBehaviour
    {
        public const string Idle = "Idle", Typing = "Typing", Stretch = "Stretch", Relax = "Relax", FeetUp = "FeetUp",
                            Celebrate = "Celebrate", Facepalm = "Facepalm", Sip = "Sip", Think = "Think", LookAround = "LookAround",
                            Phone = "Phone";

        public float RelaxAutomation = 0.85f;
        public float FeetUpRollBack = 0.32f;

        GameModel _model;
        Animator _anim;
        Transform _employee, _chair;
        GameObject _heldMug, _heldHandset;
        bool _onPhone;
        Renderer[] _deskMug;
        Vector3 _employeeBase, _chairBase;
        string _current;
        float _oneShotUntil, _lastClick = -10f, _rollBack, _nextFidget;
        readonly float[] _clickTimes = new float[24];
        int _clickHead;

        public void Init(GameModel model, SceneRefs refs)
        {
            _model = model;
            _anim = refs.EmployeeAnimator;
            _employee = refs.Employee;
            _chair = refs.ChairRoot;
            _employeeBase = _employee.localPosition;
            _chairBase = _chair.localPosition;
            _heldMug = refs.HeldMug;
            _heldHandset = refs.HeldHandset;
            if (_heldHandset) _heldHandset.SetActive(false);
            _deskMug = refs.DeskMug ? refs.DeskMug.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            if (_heldMug) _heldMug.SetActive(false);
            _nextFidget = Time.time + 12f;
            Play(Idle, 0f);
        }

        /// <summary>On a call the employee holds the handset to their ear until the call ends.</summary>
        public void SetOnPhone(bool on)
        {
            _onPhone = on;
            if (on) Play(Phone, 0.3f);
            _oneShotUntil = 0;
            if (_heldHandset) _heldHandset.SetActive(on);
        }

        public void NotifyClick()
        {
            _lastClick = Time.time;
            _clickTimes[_clickHead] = Time.time;
            _clickHead = (_clickHead + 1) % _clickTimes.Length;
        }

        float ClicksPerSecond()
        {
            int n = 0;
            float now = Time.time;
            foreach (var t in _clickTimes) if (now - t < 1f) n++;
            return n;
        }

        public void PlayOneShot(string state, float seconds)
        {
            if (_model != null && _model.State.factoryBuilt && state != FeetUp) return;
            if (_onPhone) return;
            Play(state, 0.15f);
            _oneShotUntil = Time.time + seconds;
        }

        void Play(string state, float fade)
        {
            if (_anim == null || _current == state) return;
            _current = state;
            if (fade <= 0) _anim.Play(state, 0, 0f);
            else _anim.CrossFadeInFixedTime(state, fade);
        }

        string Decide()
        {
            if (_model.State.factoryBuilt) return FeetUp;
            if (_model.Phase != GamePhase.Working) return Idle;
            bool typing = Time.time - _lastClick < 0.45f;
            if (typing) return Typing;
            if (_model.Automation >= RelaxAutomation) return Relax;
            return _model.AutoClicksPerSecond > 0 ? Typing : Idle;
        }

        /// <summary>Small idle variations so the employee never looks frozen.</summary>
        bool TryFidget(string baseState)
        {
            if (Time.time < _nextFidget) return false;
            _nextFidget = Time.time + Random.Range(9f, 17f);
            if (baseState != Idle && baseState != Relax) return false;
            float r = Random.value;
            bool hasMug = _model.HasOffice("mug");
            bool hasSideMonitors = _model.HasOffice("monitor2") || _model.HasOffice("monitor3");
            if (hasMug && r < 0.4f) { PlayOneShot(Sip, 3.0f); return true; }
            if (hasSideMonitors && r < 0.7f) { PlayOneShot(LookAround, 4.0f); return true; }
            if (baseState == Idle) { PlayOneShot(Think, Random.Range(4f, 7f)); return true; }
            return false;
        }

        void Update()
        {
            if (_model == null) return;
            if (_onPhone) { Play(Phone, 0.3f); }
            else if (Time.time >= _oneShotUntil)
            {
                string next = Decide();
                if (!TryFidget(next)) Play(next, 0.25f);
            }

            // typing speed follows how fast you're clicking
            if (_anim) _anim.speed = _current == Typing ? Mathf.Clamp(0.75f + ClicksPerSecond() * 0.12f, 0.8f, 2.2f) : 1f;

            bool sipping = _current == Sip;
            if (_heldMug && _heldMug.activeSelf != sipping)
            {
                _heldMug.SetActive(sipping);
                foreach (var r in _deskMug) if (r) r.enabled = !sipping;
            }

            // Roll the chair back when the feet go up on the desk.
            float target = _current == FeetUp ? 1f : 0f;
            _rollBack = Mathf.MoveTowards(_rollBack, target, Time.deltaTime * 0.8f);
            float k = Mathf.SmoothStep(0, 1, _rollBack);
            Vector3 back = new Vector3(0, 0, -FeetUpRollBack * k);
            _employee.localPosition = _employeeBase + back;
            _chair.localPosition = _chairBase + back;
        }
    }
}
