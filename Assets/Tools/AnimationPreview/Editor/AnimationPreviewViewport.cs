using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectTools.AnimationPreview
{
    public sealed class AnimationPreviewViewport : ImmediateModeElement
    {
        private Vector2 lastPointerPosition;
        private int pointerButton = -1;

        public Func<Rect, Texture> Render { get; set; }
        public Action<Vector2> Orbit { get; set; }
        public Action<Vector2> Pan { get; set; }
        public Action<float> Zoom { get; set; }
        public Action Focus { get; set; }
        public Action<GameObject> ModelDropped { get; set; }
        public string OverlayMessage { get; set; }

        public AnimationPreviewViewport()
        {
            name = "preview-viewport";
            focusable = true;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        protected override void ImmediateRepaint()
        {
            Rect rect = contentRect;
            Texture texture = Render?.Invoke(rect);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            else EditorGUI.DrawRect(rect, new Color(0.105f, 0.115f, 0.13f));
            if (!string.IsNullOrEmpty(OverlayMessage))
            {
                GUIStyle style = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true };
                Rect messageRect = new Rect(rect.x + rect.width * 0.2f, rect.y + rect.height * 0.4f, rect.width * 0.6f, Math.Max(56f, rect.height * 0.15f));
                GUI.Label(messageRect, OverlayMessage, style);
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 2) return;
            pointerButton = evt.button;
            lastPointerPosition = evt.position;
            Focus();
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (pointerButton < 0 || evt.pressedButtons == 0)
            {
                pointerButton = -1;
                return;
            }
            Vector2 currentPosition = new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = currentPosition - lastPointerPosition;
            lastPointerPosition = currentPosition;
            if (pointerButton == 0) Orbit?.Invoke(delta);
            else if (pointerButton == 2) Pan?.Invoke(delta);
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            pointerButton = -1;
            evt.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            Zoom?.Invoke(evt.delta.y);
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = GetDraggedModel() != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            GameObject model = GetDraggedModel();
            if (model == null) return;
            DragAndDrop.AcceptDrag();
            ModelDropped?.Invoke(model);
            evt.StopPropagation();
        }

        private static GameObject GetDraggedModel()
        {
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length != 1) return null;
            GameObject model = DragAndDrop.objectReferences[0] as GameObject;
            return model != null && EditorUtility.IsPersistent(model) ? model : null;
        }
    }
}
