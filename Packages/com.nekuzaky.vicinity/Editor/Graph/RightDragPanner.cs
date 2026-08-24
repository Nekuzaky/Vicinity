using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nekuzaky.Vicinity.Editor.Graph
{
    /// <summary>
    /// Pans the canvas while the right mouse button is held. A right button that is pressed and released
    /// without travelling still opens the context menu, because that is how a node gets added — so the two
    /// are told apart by distance, not by which button was used.
    /// </summary>
    internal sealed class RightDragPanner : MouseManipulator
    {
        #region Main Methods

        internal RightDragPanner(GraphView view)
        {
            _view = view;
        }

        #endregion

        #region Unity API

        /// <inheritdoc />
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseCaptureOutEvent>(OnCaptureLost);
        }

        /// <inheritdoc />
        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnCaptureLost);
        }

        #endregion

        #region Privates

        /// <summary>How far the mouse must travel before this counts as a drag rather than a click.</summary>
        private const float DragThreshold = 4f;

        private const int RightButton = 1;

        private readonly GraphView _view;

        private Vector2 _pressedAt;
        private bool _armed;
        private bool _panning;

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != RightButton)
            {
                return;
            }

            // Nothing is captured yet: a plain right-click must still reach the context menu.
            _pressedAt = evt.mousePosition;
            _armed = true;
            _panning = false;
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_armed)
            {
                return;
            }

            if (!_panning)
            {
                if ((evt.mousePosition - _pressedAt).sqrMagnitude < DragThreshold * DragThreshold)
                {
                    return;
                }

                _panning = true;
                target.CaptureMouse();
            }

            _view.viewTransform.position += (Vector3)evt.mouseDelta;
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button != RightButton || !_armed)
            {
                return;
            }

            _armed = false;

            if (!_panning)
            {
                // Never moved, so leave the event alone and let the context menu open.
                return;
            }

            _panning = false;
            target.ReleaseMouse();

            // Swallowing the release is what stops the context menu appearing at the end of a pan.
            evt.StopPropagation();
        }

        private void OnCaptureLost(MouseCaptureOutEvent evt)
        {
            _armed = false;
            _panning = false;
        }

        #endregion
    }
}
