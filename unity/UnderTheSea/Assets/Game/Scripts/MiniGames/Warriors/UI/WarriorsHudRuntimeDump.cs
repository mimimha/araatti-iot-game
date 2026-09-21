using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warriors
{
    /// <summary>
    /// 🔎 <b>실행 중인 화면을 그대로 로그로 찍는다.</b> 조사용이다.
    ///
    /// <b>왜 필요한가.</b> 프리팹만 들여다봐서는 알 수 없는 것이 있다. 화면에 보이는데
    /// 프리팹에는 없는 조각은 <b>코드가 실행 중에 만든 것</b>이기 때문이다. 실제로
    /// "안내 문구 옆 동그라미" 를 프리팹에서 세 번 찾았는데 없었다.
    ///
    /// 켜는 법은 실행 인자 하나다.
    /// <code>
    ///   AraAtti-WarriorsClient.exe -huddump
    /// </code>
    /// 인자가 없으면 <see cref="Awake"/> 에서 스스로 꺼지므로 평소에는 아무 비용도 없다.
    ///
    /// ⚠ 키 입력으로 켜지 않는다. 사용자의 창에 키를 대신 눌러 넣지 않는다는 약속이 있다.
    ///    시간이 지나면 저절로 한 번 찍고 끝난다.
    /// </summary>
    public sealed class WarriorsHudRuntimeDump : MonoBehaviour
    {
        private const string Flag = "-huddump";

        /// <summary>이만큼 지난 뒤 찍는다. 라운드가 시작되고 HUD 가 자리를 잡을 시간이다.</summary>
        [SerializeField, Min(1f)] private float afterSeconds = 25f;

        private float left;

        private void Awake()
        {
            bool asked = false;

            foreach (string arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == Flag) asked = true;
            }

            if (!asked)
            {
                enabled = false;
                return;
            }

            left = afterSeconds;
        }

        private void Update()
        {
            left -= Time.unscaledDeltaTime;
            if (left > 0f) return;

            enabled = false;   // 한 번만 찍는다
            Dump();
        }

        private void Dump()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : transform.root;

            StringBuilder text = new StringBuilder();
            text.Append("[화면 덤프] ───── 지금 그려지는 것 전부 ─────\n");

            foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                // 안 보이는 것은 건너뛴다. 찾는 것은 "화면에 있는" 조각이다.
                if (!rt.gameObject.activeInHierarchy) continue;

                Graphic drawn = rt.GetComponent<Graphic>();
                if (drawn == null || !drawn.enabled) continue;

                // 화면 좌표(캔버스 가운데 기준)로 바꿔야 스크린샷과 대조할 수 있다.
                Vector3 mid = rt.TransformPoint(rt.rect.center);
                Vector3 local = root.InverseTransformPoint(mid);

                string what = drawn is Image img
                    ? $"Image {(img.sprite == null ? "단색" : img.sprite.name)}"
                    : drawn is RawImage raw
                        ? $"RawImage {(raw.texture == null ? "비어 있음" : raw.texture.name)}"
                        : drawn is TMP_Text tmp
                            ? $"글자 \"{Short(tmp.text)}\" {tmp.fontSize:F0}pt"
                            : drawn.GetType().Name;

                text.Append($"  {Path(rt, root)}\n")
                    .Append($"      가운데 {local.x:F0},{local.y:F0} · 크기 {rt.rect.width:F0}×{rt.rect.height:F0} · ")
                    .Append($"{what} · 색 #{ColorUtility.ToHtmlStringRGBA(drawn.color)}\n");
            }

            Debug.Log(text.ToString());
        }

        private static string Short(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace("\n", "↵");
            return value.Length > 20 ? value.Substring(0, 20) + "…" : value;
        }

        private static string Path(Transform t, Transform root)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
