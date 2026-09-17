/// <summary>
/// **지금 글자를 치고 있는가.** 이동 입력을 보내는 쪽이 이것만 보면 된다.
///
/// 채팅창에 글을 치는 동안 캐릭터가 걸어가면 안 된다. 그런데 이동을 읽는 곳
/// (<c>PlayerInputProvider</c>)은 채팅창이 있는지도 모르고, 알 필요도 없다.
/// 그래서 "입력 중" 이라는 사실 하나만 여기에 세워 두고 양쪽이 이것만 본다.
///
/// <b>왜 채팅 컴포넌트를 직접 찾지 않는가.</b>
/// 이동을 읽는 곳이 채팅을 이름이나 타입으로 찾으면, 채팅이 없는 씬(미니게임 등)에서도
/// 매번 찾게 되고 채팅 구현이 바뀔 때마다 같이 고쳐야 한다. 나중에 아이템창·상점처럼
/// 글자를 받는 화면이 늘어도 여기에 한 줄 얹으면 끝이다.
///
/// <code>
///   채팅 입력칸이 켜짐   →  ChatFocus.Typing = true   →  이동 입력이 0 으로 나간다
///   전송하거나 끄면      →  ChatFocus.Typing = false  →  다시 걷는다
/// </code>
///
/// ⚠ 켠 쪽이 반드시 끈다. 채팅창이 꺼지거나 씬이 바뀔 때 끄지 않으면
///    **영영 못 움직이는 상태**가 된다. 그래서 OnDisable 에서도 내린다.
/// </summary>
public static class ChatFocus
{
    /// <summary>지금 어딘가에 글자를 치고 있는지.</summary>
    public static bool Typing { get; private set; }

    /// <summary>글자 입력을 시작했다.</summary>
    public static void Begin()
    {
        Typing = true;
    }

    /// <summary>글자 입력을 끝냈다. 화면이 사라질 때도 반드시 부른다.</summary>
    public static void End()
    {
        Typing = false;
    }
}
