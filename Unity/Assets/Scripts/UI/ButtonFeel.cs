using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// How every UIKit button answers the pointer: it brightens on hover (the Button's colour tint), menu-sized buttons
    /// also lift a little and tick, and a press squashes it for a moment. Keyboard and D-pad presses (MenuFocus) get the
    /// same squash. With Reduce motion on, buttons only change colour.
    /// </summary>
    public class ButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        /// <summary>Plays the hover tick (set once by GameManager).</summary>
        public static Action HoverSound;

        /// <summary>Lift on hover (menu, dialog and night-screen buttons; not the dense lists on the monitor).</summary>
        public bool Lift;
        /// <summary>Tick on hover.</summary>
        public bool Tick;

        Selectable _selectable;
        bool _hover, _down;
        float _scale = 1f, _pressT = 1f;
        Vector3 _base = Vector3.one;
        bool _animating;

        const float LiftScale = 1.025f, PressDepth = 0.06f;

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _base = transform.localScale;
        }

        /// <summary>Buttons with their own bounce (SHIP CODE's punch, LOG IN's pulse) keep it and only tick.</summary>
        bool OwnMotion => GetComponent<Punch>() || GetComponent<Pulse>();

        void OnDisable()
        {
            _hover = _down = false;
            _scale = 1f;
            _pressT = 1f;
            _animating = false;
            transform.localScale = _base;
        }

        bool Interactable => _selectable == null || _selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Interactable) return;
            if (Tick) HoverSound?.Invoke();
            if (OwnMotion) return;
            _hover = true;
            _animating = true;
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (OwnMotion) return;
            _hover = false;
            _down = false;
            _animating = true;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!Interactable || e.button != PointerEventData.InputButton.Left || OwnMotion) return;
            _down = true;
            _animating = true;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (OwnMotion) return;
            _down = false;
            _animating = true;
        }

        /// <summary>A keyboard or D-pad press: one quick squash.</summary>
        public void OnSubmit(BaseEventData e) => Squash();

        public void Squash()
        {
            if (UIKit.ReduceMotion || OwnMotion) return;
            _pressT = 0f;
            _animating = true;
        }

        void Update()
        {
            if (!_animating) return;
            if (UIKit.ReduceMotion)
            {
                transform.localScale = _base;
                _animating = false;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            float target = _down ? 1f - PressDepth : _hover && Lift && Interactable ? LiftScale : 1f;
            _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-dt * (_down ? 30f : 16f)));
            float squash = 1f;
            if (_pressT < 1f)
            {
                _pressT = Mathf.Min(1f, _pressT + dt * 6f);
                squash = 1f - PressDepth * Mathf.Sin(_pressT * Mathf.PI);
            }
            transform.localScale = _base * (_scale * squash);
            if (Mathf.Abs(_scale - target) < 0.0005f && _pressT >= 1f)
            {
                _scale = target;
                transform.localScale = _base * _scale;
                _animating = false;
            }
        }
    }
}
