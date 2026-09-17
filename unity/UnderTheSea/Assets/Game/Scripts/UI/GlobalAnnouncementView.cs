using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 확성기로 보낸 말을 화면 맨 위에 띄우는 띠.
///
/// <code>
///        📣  하은: 보물섬 탐험대 모집해요!
/// </code>
///
/// 채팅창은 왼쪽 아래 구석에 있어서 보고 있지 않으면 놓친다. 모집 · 약속처럼
/// **모두가 봐야 하는 말**은 화면 한가운데 위에 잠깐 띄운다.
///
/// <b>한 번에 하나만 띄운다.</b> 여럿이 동시에 확성기를 쓰면 띠가 겹쳐서
/// 아무것도 안 읽힌다. 뒤에 온 것은 <see cref="pending"/> 에 줄을 세웠다가
/// 앞엣것이 사라지면 올린다.
///
/// <b>줄이 길어지면 버린다.</b> 도배를 당했을 때 몇 분 동안 남의 공지가
/// 계속 올라오면 화면을 못 쓴다. <see cref="MaxPending"/> 을 넘으면
/// **오래된 것부터** 버린다 — 늦게 온 것이 더 최신이라 쓸모 있다.
///
/// ⚠ 이 컴포넌트는 네트워크를 모른다. <c>LobbyChatRelay</c> 가 받은 것을
///    <see cref="Current"/> 로 찾아 넣어 준다. 없으면 (미니게임 등) 조용히 넘어간다.
/// </summary>
public class GlobalAnnouncementView : MonoBehaviour
{
    /// <summary>줄 세워 둘 수 있는 최대 개수.</summary>
    private const int MaxPending = 3;

    [Header("연결")]
    [Tooltip("띠 전체. 뜰 때만 켜진다.")]
    [SerializeField] private GameObject banner;

    [SerializeField] private TMP_Text label;

    [SerializeField] private CanvasGroup group;

    [Header("시간")]
    [Tooltip("이만큼 떠 있다가 사라진다.")]
    [SerializeField, Min(1f)] private float showSeconds = 6f;

    [Tooltip("나타나고 사라질 때 흐려지는 시간.")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.35f;

    /// <summary>지금 화면에 있는 공지 띠. 없으면 null.</summary>
    public static GlobalAnnouncementView Current { get; private set; }

    private readonly Queue<string> pending = new Queue<string>();

    private float hideAt;

    private void Awake()
    {
        if (banner != null)
        {
            banner.SetActive(false);
        }
    }

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>공지를 띄운다. 이미 떠 있으면 줄을 세운다.</summary>
    public void Show(string speaker, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string line = string.IsNullOrWhiteSpace(speaker)
            ? message
            : speaker + ": " + message;

        if (banner != null && banner.activeSelf)
        {
            pending.Enqueue(line);

            // 넘치면 오래된 것부터 버린다. 지금 것이 더 쓸모 있다.
            while (pending.Count > MaxPending)
            {
                pending.Dequeue();
            }

            return;
        }

        Raise(line);
    }

    /// <summary>
    /// 배치를 맞출 때 쓴다. 부품 ⋮ 메뉴에서 고른다.
    ///
    /// 띠는 평소 꺼져 있어서 편집 중에는 안 보인다. 켜 두고 저장하면 로비에 늘 떠 있게
    /// 되므로, 여기서 켜고 <b>끄는 것까지 메뉴로</b> 둔다.
    /// </summary>
    [ContextMenu("공지 띠 미리보기")]
    public void PreviewShow()
    {
        if (label != null)
        {
            label.text = "하은: 보물섬 탐험대 모집해요!";
        }

        if (group != null)
        {
            group.alpha = 1f;
        }

        if (banner != null)
        {
            banner.SetActive(true);
        }

        // 글자에 맞춰 띠 길이를 다시 잰다. 이것이 없으면 옛 길이로 보인다.
        Canvas.ForceUpdateCanvases();
    }

    [ContextMenu("공지 띠 미리보기 끄기")]
    public void PreviewHide()
    {
        if (banner != null)
        {
            banner.SetActive(false);
        }
    }

    private void Update()
    {
        if (banner == null || !banner.activeSelf)
        {
            return;
        }

        float left = hideAt - Time.time;

        if (left <= 0f)
        {
            banner.SetActive(false);

            if (pending.Count > 0)
            {
                Raise(pending.Dequeue());
            }

            return;
        }

        if (group == null || fadeSeconds <= 0f)
        {
            return;
        }

        // 들어올 때와 나갈 때 양쪽을 흐리게 한다. 갑자기 나타나면 눈에 거슬린다.
        float shown = showSeconds - left;
        float rising = Mathf.Clamp01(shown / fadeSeconds);
        float falling = Mathf.Clamp01(left / fadeSeconds);

        group.alpha = Mathf.Min(rising, falling);
    }

    private void Raise(string line)
    {
        if (label != null)
        {
            label.text = line;
        }

        hideAt = Time.time + showSeconds;

        if (group != null)
        {
            group.alpha = fadeSeconds > 0f ? 0f : 1f;
        }

        if (banner != null)
        {
            banner.SetActive(true);
        }
    }
}
