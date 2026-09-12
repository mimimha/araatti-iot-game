using UnityEngine;
using UnityEngine.UI;

namespace Warriors
{
    public sealed class WarriorsCombatHud : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private Text currentInputText;

        private IWarriorsInputSource InputSource => inputSource as IWarriorsInputSource;
        private string actionLabel;
        private float actionUntil;

        public string ActiveActionLabel => Time.time <= actionUntil ? actionLabel : string.Empty;

        /// <summary>
        /// The swing the player just made, for the short window after it landed.
        /// The HUD lights up the matching attack card with this instead of printing the
        /// attack name across the middle of the screen.
        /// </summary>
        public WarriorsAttackDirection ActiveActionType =>
            Time.time <= actionUntil ? actionType : WarriorsAttackDirection.None;

        private WarriorsAttackDirection actionType = WarriorsAttackDirection.None;
        public void SetLegacyHudVisible(bool visible) => drawLegacyHud = visible;
        [SerializeField] private bool drawLegacyHud = true;

        private void OnEnable()
        {
            if (InputSource != null)
            {
                InputSource.AttackRequested += HandleAttack;
            }
        }

        private void OnDisable()
        {
            if (InputSource != null)
            {
                InputSource.AttackRequested -= HandleAttack;
            }
        }

        private void HandleAttack(WarriorsAttackDirection direction, float strength)
        {
            if (currentInputText == null)
            {
                return;
            }

            currentInputText.text = direction switch
            {
                WarriorsAttackDirection.HorizontalSlash => "CURRENT  1  HORIZONTAL",
                WarriorsAttackDirection.VerticalSlash => "CURRENT  2  VERTICAL",
                _ => "CURRENT  3  THRUST"
            };
            actionType = direction;
            actionLabel = direction switch
            {
                WarriorsAttackDirection.HorizontalSlash => "↔  가로베기",
                WarriorsAttackDirection.VerticalSlash => "↕  세로베기",
                _ => "⊙  찌르기"
            };
            actionUntil = Time.time + .55f;
        }

        private void OnGUI()
        {
            if (!drawLegacyHud) return;
            if (Time.time > actionUntil || string.IsNullOrEmpty(actionLabel)) return;
            GUIStyle style = new(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 900f, 1f, 1.6f) * 30f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width * .5f - 150, Screen.height * .72f, 300, 58), string.Empty);
            GUI.Label(new Rect(Screen.width * .5f - 145, Screen.height * .72f + 4, 290, 50), actionLabel, style);
        }

    }
}
