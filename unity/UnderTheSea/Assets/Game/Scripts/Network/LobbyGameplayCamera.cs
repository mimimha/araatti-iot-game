using System.Collections.Generic;
using UnityEngine;
using ithappy.Cute_Characters.Controller;

/// <summary>
/// "이 카메라가 로컬 플레이어를 따라가는 게임플레이 카메라다" 라고 <b>명시하는 표식</b>.
///
/// Lobby 의 <c>MainCamera</c> 에 붙여 둔다. 붙어 있는 것 자체가 선언이다.
///
/// <b>왜 씬 이름으로 찾으면 안 되는가.</b>
/// Fusion 은 세션을 시작할 때 씬을 러너 전용 씬으로 인수한다. 그때 카메라 오브젝트도 같이
/// 옮겨져 <c>gameObject.scene.name</c> 이 바뀐다. 실제로 두 경로의 결과가 달랐다.
///   개발용 직접 실행  → 씬 이름 "Lobby"
///   정상 Login 경로   → 씬 이름 "FusionRunner (Client)_[Player:2]"
/// 그래서 씬 이름을 기준으로 삼으면 한쪽에서 반드시 빗나간다.
/// 오브젝트가 어느 씬으로 옮겨지든 <b>표식은 함께 따라다닌다.</b>
///
/// <b>왜 FindFirstObjectByType 도 안 되는가.</b>
/// 찾는 순서가 보장되지 않는다. 카메라가 둘 있을 때 어느 쪽을 집을지가 실행마다 달라진다.
/// 여기서는 등록 목록을 직접 들고 있으므로 후보를 전부 보고 규칙대로 고를 수 있다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-3)
/// </summary>
[RequireComponent(typeof(Camera))]
public class LobbyGameplayCamera : MonoBehaviour
{
    private static readonly List<LobbyGameplayCamera> Registered = new List<LobbyGameplayCamera>();

    /// <summary>지금 등록된 게임플레이 카메라들. 보통 하나지만 씬 인수 과정에서 둘이 될 수 있다.</summary>
    public static IReadOnlyList<LobbyGameplayCamera> All => Registered;

    /// <summary>이 표식이 붙은 카메라.</summary>
    public Camera Camera { get; private set; }

    /// <summary>플레이어를 따라가는 스크립트. 없으면 null.</summary>
    public PlayerCamera Follower { get; private set; }

    private void Awake()
    {
        Camera = GetComponent<Camera>();
        Follower = GetComponent<PlayerCamera>();

        if (Follower == null)
        {
            Debug.LogError(
                $"[LobbyGameplayCamera] '{name}' 에 PlayerCamera(ThirdPersonCamera) 가 없습니다. " +
                "이 표식은 플레이어를 따라가는 카메라에만 붙입니다.", this);
        }

        if (!Registered.Contains(this))
        {
            Registered.Add(this);
        }
    }

    private void OnDestroy()
    {
        Registered.Remove(this);
    }

    /// <summary>
    /// 플레이 모드에 들어갈 때마다 비운다.
    /// 에디터에서 "Reload Domain" 을 꺼 두면 static 이 이전 플레이의 값을 들고 있다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Registered.Clear();
    }

    /// <summary>
    /// 따라갈 대상과 <b>같은 씬</b>에 있는 게임플레이 카메라를 고른다.
    ///
    /// 후보가 둘 이상일 수 있다. Fusion 이 씬을 인수하면서 원래 씬의 카메라와
    /// 다시 로드된 씬의 카메라가 함께 남기 때문이다. 이때 <b>플레이어가 있는 씬</b>의
    /// 카메라를 고르는 것이 옳다 — 그 카메라가 실제로 그 월드를 비추고 있다.
    ///
    /// 고를 수 없으면 <c>null</c> 을 돌려주고 이유를 남긴다. 아무거나 집지 않는다.
    /// </summary>
    public static LobbyGameplayCamera ResolveFor(Transform follow)
    {
        // 파괴된 것이 목록에 남아 있을 수 있다.
        Registered.RemoveAll(c => c == null);

        if (Registered.Count == 0)
        {
            Debug.LogError(
                "[LobbyGameplayCamera] 게임플레이 카메라를 찾지 못했습니다. " +
                "Lobby 의 MainCamera 에 LobbyGameplayCamera 표식이 붙어 있는지 확인해 주세요.");
            return null;
        }

        if (Registered.Count == 1)
        {
            return Registered[0];
        }

        // 후보가 여럿이다. 따라갈 대상과 같은 씬에 있는 것을 고른다.
        if (follow != null)
        {
            List<LobbyGameplayCamera> sameScene = Registered
                .FindAll(c => c.gameObject.scene == follow.gameObject.scene);

            if (sameScene.Count == 1)
            {
                return sameScene[0];
            }

            if (sameScene.Count > 1)
            {
                Debug.LogError(
                    $"[LobbyGameplayCamera] 같은 씬('{follow.gameObject.scene.name}')에 " +
                    $"게임플레이 카메라가 {sameScene.Count}개 있습니다. 어느 것을 쓸지 정할 수 없습니다. " +
                    "표식이 중복으로 붙어 있는지 확인해 주세요.");
                return null;
            }
        }

        Debug.LogError(
            $"[LobbyGameplayCamera] 게임플레이 카메라가 {Registered.Count}개인데 " +
            $"따라갈 대상(씬 '{(follow == null ? "없음" : follow.gameObject.scene.name)}')과 " +
            "같은 씬에 있는 것이 없습니다. 후보: " +
            string.Join(", ", Registered.ConvertAll(c => $"{c.name}(씬 '{c.gameObject.scene.name}')")));
        return null;
    }
}
