using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsScorePopup : MonoBehaviour
    {
        private int points;
        private float expires;
        public void Show(int value) { points = value; expires = Time.time + .8f; }
        private void Update() { transform.position += Vector3.up * (.65f * Time.deltaTime); }
        private void OnGUI()
        {
            if (Time.time >= expires || Camera.main == null) return;
            Vector3 p = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
            if (p.z <= 0f) return;
            GUIStyle style = new(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(p.x - 60, Screen.height - p.y - 18, 120, 36), $"+{points}", style);
        }
    }
}
