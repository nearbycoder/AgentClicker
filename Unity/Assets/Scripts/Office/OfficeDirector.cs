using System.Linq;
using AgentClicker.Core;
using AgentClicker.UI;
using TMPro;
using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>Keeps the 3D office in sync with the game state: purchases, promotions and day-by-day drift.</summary>
    public class OfficeDirector : MonoBehaviour
    {
        GameModel _model;
        SceneRefs _refs;
        OfficeProp[] _props;

        public void Init(GameModel model, SceneRefs refs)
        {
            _model = model;
            _refs = refs;
            _props = refs.OfficeRoot.GetComponentsInChildren<OfficeProp>(true);
            foreach (var t in new[] { refs.CalendarDay, refs.CalendarWeekday, refs.NamePlate, refs.Plaque, refs.Poster1, refs.Poster2, refs.DoorSign })
                if (t) t.font = UIFonts.Bold;
            Refresh(false);
        }

        /// <summary>Re-evaluate every prop. With `animate`, newly visible props pop in.</summary>
        public void Refresh(bool animate)
        {
            if (_model == null) return;
            foreach (var p in _props)
                p.SetVisible(p.Evaluate(_model), animate);
            UpdateLabels();
        }

        public Transform ShowcaseTarget(string officeId)
        {
            var p = _props.FirstOrDefault(x => x.ShowcaseFor == officeId && x.Visible);
            return p ? p.transform : null;
        }

        void UpdateLabels()
        {
            var s = _model.State;
            Set(_refs.CalendarDay, s.day.ToString());
            Set(_refs.CalendarWeekday, FlavorText.Weekday(s.day).ToUpperInvariant());
            Set(_refs.NamePlate, $"{FlavorText.EmployeeName.ToUpperInvariant()}\n<size=55%>{_model.Title.Name}</size>");
            Set(_refs.Plaque, $"<size=70%>EMPLOYEE OF THE MONTH</size>\n{FlavorText.EmployeeName}\n<size=55%>{_model.Title.Name}</size>");
            Set(_refs.Poster1, "HANG IN\nTHERE\n<size=45%>the agents will\nfinish soon</size>");
            Set(_refs.Poster2, "MOVE FAST\n<size=60%>AND</size>\nPROMPT\nTHINGS");
            Set(_refs.DoorSign, "AI LAB");
        }

        static void Set(TextMeshPro t, string text)
        {
            if (t && t.text != text) t.text = text;
        }
    }
}
