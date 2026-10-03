using System.Collections.Generic;

/// <summary>
/// **지금 게임플레이 입력을 막는 화면이 떠 있는가.** 이동 입력을 보내는 쪽이 이것만 보면 된다.
///
/// 채팅창에 글을 치는 동안 캐릭터가 걸어가면 안 된다. 그런데 이동을 읽는 곳
/// (<c>PlayerInputProvider</c>)은 채팅창이 있는지도 모르고, 알 필요도 없다.
/// 그래서 "입력 중" 이라는 사실 하나만 여기에 세워 두고 양쪽이 이것만 본다.
///
/// <b>왜 채팅 컴포넌트를 직접 찾지 않는가.</b>
/// 이동을 읽는 곳이 채팅을 이름이나 타입으로 찾으면, 채팅이 없는 씬(미니게임 등)에서도
/// 매번 찾게 되고 채팅 구현이 바뀔 때마다 같이 고쳐야 한다.
///
/// <code>
///   채팅 입력칸이 켜짐   →  Begin(this)  →  Typing = true   →  이동 입력이 0 으로 나간다
///   전송하거나 끄면      →  End(this)    →  Typing = false  →  다시 걷는다
/// </code>
///
/// ⚠ 켠 쪽이 반드시 끈다. 화면이 꺼지거나 씬이 바뀔 때 끄지 않으면
///    **영영 못 움직이는 상태**가 된다. 그래서 OnDisable 에서도 내린다.
///
/// ────────────────────────────────────────────────────────────────
/// <b>왜 bool 하나가 아니라 보유자 집합인가.</b>
///
/// 잠그는 화면이 채팅 하나였을 때는 bool 로 충분했다. 제단 봉헌 UI 가 생기면서
/// <b>둘이 동시에 잠그는 상황</b>이 생겼고, 그때 bool 은 이렇게 깨진다.
///
/// <code>
///   제단 UI 열림    Begin()  → true      이동 막힘
///   채팅칸 클릭     Begin()  → true
///   채팅 바깥 클릭  End()    → false     ⚠ 제단 UI 는 아직 열려 있는데 걸어가진다
/// </code>
///
/// 참조 계수(int)도 답이 아니다. <c>LobbyChatView</c> 의 <c>End()</c> 호출 넷 중 둘은
/// 짝이 되는 <c>Begin()</c> 이 없다(<c>OnDisable</c> · <c>Unfocus</c>). 계수가 음수로
/// 내려가면 제단이 걸어 둔 잠금까지 같이 풀린다.
///
/// 그래서 <b>누가 잠갔는지</b>를 기억한다. <see cref="HashSet{T}.Add"/> 와
/// <see cref="HashSet{T}.Remove"/> 는 몇 번을 불러도 결과가 같아서
/// <b>짝이 안 맞는 기존 호출이 저절로 안전해진다.</b> 그 코드를 고칠 필요가 없다.
///
/// ⚠ <b>이름은 그대로 둔다.</b> 이제 "글자를 치는 중" 보다 "게임플레이 입력을 막는 화면이
///    떠 있음" 에 가깝지만, 바꾸면 <c>PlayerInputProvider</c> 까지 건드려야 한다.
///    <c>GameplayInputLock</c> 등으로 바꾸는 것은 언제든 할 수 있는 후속 작업으로 남긴다.
///
/// 문서: docs/prd/lobby_altar_inventory_system_design.md 6.4.1절
/// </summary>
public static class ChatFocus
{
    /// <summary>
    /// 지금 잠그고 있는 화면들.
    ///
    /// 누가 들어 있는지는 밖에서 볼 수 없다. 밖이 알아야 하는 것은
    /// <see cref="Typing"/> 과 <see cref="HeldByOther"/> 둘뿐이다.
    /// </summary>
    private static readonly HashSet<object> Holders = new HashSet<object>();

    /// <summary>
    /// 지금 **휠**을 쓰고 있는 화면들.
    ///
    /// <see cref="Holders"/> 와 나눠 둔다. 둘은 세기가 다르다. 채팅 기록 위에 마우스만
    /// 올려 둔 상태는 휠만 가져갈 뿐이고, 그동안에도 WASD 로는 걸어다닐 수 있어야 한다.
    /// 하나로 합치면 기록 위에 마우스를 둔 채로는 못 움직이게 된다.
    /// </summary>
    private static readonly HashSet<object> WheelHolders = new HashSet<object>();

    /// <summary>하나라도 잠그고 있는지. 이동을 읽는 쪽이 이것만 본다.</summary>
    public static bool Typing => Holders.Count > 0;

    /// <summary>
    /// <b>휠을 화면이 쓰고 있는가.</b> 카메라 줌을 읽는 쪽이 이것만 본다.
    ///
    /// 채팅 기록을 되짚어 올리는 동안 화면까지 줌되면 안 된다. 그렇다고 카메라 쪽이
    /// 채팅을 알 필요는 없다 — <see cref="Typing"/> 과 같은 이유로 여기 한 곳만 본다.
    /// </summary>
    public static bool WheelHeld => WheelHolders.Count > 0;

    /// <summary>휠을 가져간다. 같은 주인이 여러 번 불러도 결과가 같다.</summary>
    public static void BeginWheel(object owner)
    {
        if (owner != null)
        {
            WheelHolders.Add(owner);
        }
    }

    /// <summary>휠을 돌려준다. 건 적이 없어도, 두 번 불러도 안전하다.</summary>
    public static void EndWheel(object owner)
    {
        if (owner != null)
        {
            WheelHolders.Remove(owner);
        }
    }

    /// <summary>
    /// 입력 잠금을 건다. 같은 주인이 여러 번 불러도 결과가 같다.
    /// </summary>
    /// <param name="owner">거는 쪽. 보통 <c>this</c> 를 넘긴다.</param>
    public static void Begin(object owner)
    {
        if (owner != null)
        {
            Holders.Add(owner);
        }
    }

    /// <summary>
    /// 자기가 건 잠금만 푼다. 화면이 사라질 때도 반드시 부른다.
    ///
    /// 건 적이 없어도, 두 번 불러도 안전하다. 그리고 <b>남이 건 잠금은 풀리지 않는다.</b>
    /// </summary>
    public static void End(object owner)
    {
        if (owner != null)
        {
            Holders.Remove(owner);
        }
    }

    /// <summary>
    /// <b>나 말고 다른 누군가가 잠그고 있는가.</b>
    ///
    /// Esc 처리에 필요하다. 제단 UI 가 열려 있으면 자기 자신이 보유자라
    /// <see cref="Typing"/> 은 언제나 참이고, 그 값으로 Esc 를 거르면
    /// <b>제단 UI 가 Esc 로 영영 닫히지 않는다.</b>
    /// "채팅이 떠 있으면 채팅이 먼저 먹는다" 를 표현하려면 이쪽을 물어야 한다.
    /// </summary>
    public static bool HeldByOther(object me)
    {
        foreach (object holder in Holders)
        {
            if (!ReferenceEquals(holder, me))
            {
                return true;
            }
        }

        return false;
    }
}
