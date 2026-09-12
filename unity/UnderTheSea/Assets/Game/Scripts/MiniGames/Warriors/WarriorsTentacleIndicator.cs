using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsTentacleIndicator : MonoBehaviour
    {
        [SerializeField] private WarriorsTarget target;
        [SerializeField] private Font labelFont;
        [SerializeField] private Vector3 localOffset = new(0f, 2.15f, 5.15f);
        [SerializeField] private Vector3 fixedWorldEuler = new(0f, 180f, 0f);
        private TextMesh arrowLabel;

        private void Awake()
        {
            if (target == null) target = GetComponent<WarriorsTarget>();
            EnsureLabel();
        }

        private void OnEnable()
        {
            EnsureLabel();
            RefreshLabel();
        }

        private void LateUpdate()
        {
            if (arrowLabel == null) EnsureLabel();
            if (arrowLabel == null) return;
            arrowLabel.transform.localPosition = localOffset;
            arrowLabel.transform.rotation = Quaternion.Euler(fixedWorldEuler);
            arrowLabel.gameObject.SetActive(target != null && !target.IsDefeated);
            RefreshLabel();
        }

        private void EnsureLabel()
        {
            Transform existing = transform.Find("DirectionIndicatorAnchor");
            GameObject labelObject;
            if (existing != null)
            {
                labelObject = existing.gameObject;
                arrowLabel = labelObject.GetComponent<TextMesh>();
            }
            else
            {
                labelObject = new GameObject("DirectionIndicatorAnchor");
                labelObject.transform.SetParent(transform, false);
                arrowLabel = labelObject.AddComponent<TextMesh>();
            }
            if (arrowLabel == null) arrowLabel = labelObject.AddComponent<TextMesh>();
            arrowLabel.anchor = TextAnchor.MiddleCenter;
            arrowLabel.alignment = TextAlignment.Center;
            arrowLabel.fontSize = 64;
            arrowLabel.characterSize = .125f;
            arrowLabel.color = Color.white;
            arrowLabel.fontStyle = FontStyle.Bold;
            if (labelFont != null)
            {
                arrowLabel.font = labelFont;
                arrowLabel.GetComponent<MeshRenderer>().sharedMaterial = labelFont.material;
            }
            arrowLabel.transform.localPosition = localOffset;
            arrowLabel.transform.rotation = Quaternion.Euler(fixedWorldEuler);
            arrowLabel.GetComponent<MeshRenderer>().sortingOrder = 20;
        }

        private void RefreshLabel()
        {
            if (arrowLabel == null || target == null) return;
            arrowLabel.text = target.RequiredDirection switch
            {
                WarriorsAttackDirection.HorizontalSlash => "↔",
                WarriorsAttackDirection.VerticalSlash => "↕",
                _ => "⊙"
            };
        }
    }
}
