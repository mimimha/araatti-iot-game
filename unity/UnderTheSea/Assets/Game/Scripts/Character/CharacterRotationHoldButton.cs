using UnityEngine;
using UnityEngine.EventSystems;

namespace UnderTheSea.Character
{
    public sealed class CharacterRotationHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Transform target;
        [SerializeField] private float degreesPerSecond = 90f;

        private bool isPressed;

        private void Update()
        {
            if (isPressed && target != null)
                target.Rotate(0f, degreesPerSecond * Time.unscaledDeltaTime, 0f, Space.World);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPressed = false;
        }
    }
}
