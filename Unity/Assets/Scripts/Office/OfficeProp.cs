using System;
using AgentClicker.Core;
using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>
    /// A piece of the office whose visibility depends on game state.
    ///
    /// Condition syntax: terms joined by '&amp;', each optionally negated with '!':
    ///   office:&lt;id&gt;   owns an office item          day&gt;=N / day&lt;N    current day
    ///   title&gt;=N / title&lt;N   promotion index         factory           software factory built
    ///   always
    /// Example: "office:gaming_chair&amp;!office:recliner"
    /// </summary>
    public class OfficeProp : MonoBehaviour
    {
        [Tooltip("Visibility condition, see class docs")]
        public string Condition = "always";
        [Tooltip("Shown in the purchase showcase camera when this item is bought")]
        public string ShowcaseFor = "";

        Vector3 _baseScale;
        float _anim = 1f;
        bool _appearing;
        bool _initialised;

        public bool Visible { get; private set; } = true;

        void EnsureInit()
        {
            if (_initialised) return;
            _initialised = true;
            _baseScale = transform.localScale;
        }

        public bool Evaluate(GameModel model)
        {
            foreach (var raw in Condition.Split('&'))
            {
                string term = raw.Trim();
                if (term.Length == 0) continue;
                bool negate = term.StartsWith("!");
                if (negate) term = term.Substring(1);
                bool value = EvalTerm(term, model);
                if (value == negate) return false;
            }
            return true;
        }

        static bool EvalTerm(string term, GameModel m)
        {
            if (term == "always") return true;
            if (term == "factory") return m.State.factoryBuilt;
            if (term.StartsWith("office:")) return m.HasOffice(term.Substring(7));
            if (TryCompare(term, "day", m.State.day, out bool d)) return d;
            if (TryCompare(term, "title", m.State.titleIndex, out bool t)) return t;
            Debug.LogWarning($"[OfficeProp] unknown condition term '{term}'");
            return true;
        }

        static bool TryCompare(string term, string key, int actual, out bool result)
        {
            result = false;
            if (!term.StartsWith(key)) return false;
            string rest = term.Substring(key.Length);
            if (rest.StartsWith(">=")) result = actual >= int.Parse(rest.Substring(2));
            else if (rest.StartsWith("<=")) result = actual <= int.Parse(rest.Substring(2));
            else if (rest.StartsWith("<")) result = actual < int.Parse(rest.Substring(1));
            else if (rest.StartsWith(">")) result = actual > int.Parse(rest.Substring(1));
            else if (rest.StartsWith("==")) result = actual == int.Parse(rest.Substring(2));
            else return false;
            return true;
        }

        public void SetVisible(bool visible, bool animate)
        {
            EnsureInit();
            if (visible == Visible && gameObject.activeSelf == visible) return;
            Visible = visible;
            if (visible)
            {
                gameObject.SetActive(true);
                if (animate)
                {
                    _appearing = true;
                    _anim = 0f;
                    transform.localScale = _baseScale * 0.01f;
                }
                else
                {
                    transform.localScale = _baseScale;
                }
            }
            else
            {
                gameObject.SetActive(false);
                transform.localScale = _baseScale;
            }
        }

        void Update()
        {
            if (!_appearing) return;
            _anim = Mathf.Min(1f, _anim + Time.deltaTime * 1.6f);
            // overshooting ease-out ("pop")
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = _anim - 1f;
            float k = 1f + c3 * x * x * x + c1 * x * x;
            transform.localScale = _baseScale * Mathf.Max(0.01f, k);
            if (_anim >= 1f)
            {
                _appearing = false;
                transform.localScale = _baseScale;
            }
        }
    }
}
