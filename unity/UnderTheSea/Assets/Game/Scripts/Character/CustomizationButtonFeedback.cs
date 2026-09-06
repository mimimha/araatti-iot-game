using UnityEngine;
using UnityEngine.EventSystems;

namespace UnderTheSea.Character
{
    // Scale instead of moving layout-driven RectTransforms; scrolling remains owned by ScrollRect.
    public sealed class CustomizationButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private bool hovered;
        private bool pressed;
        private void Update()
        {
            float size = pressed ? .965f : hovered ? 1.025f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * size, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        }
        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }
        private void OnDisable() { hovered = pressed = false; transform.localScale = Vector3.one; }
    }
}
