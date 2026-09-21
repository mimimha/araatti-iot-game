using UnityEngine;

/// <summary>
/// <b>Dedicated Server 에서 파티클을 멈춘다.</b> 로비·광산·검·배가 모두 이 부품을 쓴다.
///
/// <b>왜 서버가 파티클을 돌리고 있었나.</b> <c>-nographics</c> 는 <b>그리는 일</b>만 건너뛴다.
/// 파티클은 시뮬레이션과 렌더링이 별개 단계라, 서버에서도 입자를 매 프레임 굴린다.
/// 아무도 보지 않고 아무 데도 보내지 않는 계산이다.
///
/// <b>얼마나 비쌌나 — 로비 서버 실측(22코어 기준).</b>
/// <code>
///                              접속 0명
///   파티클 켜짐 · 워커 기본     코어 1.67
///   파티클 끔   · 워커 기본     코어 0.65
///   파티클 끔   · 워커 2개      코어 0.12   ← 광산 서버와 같은 수준
/// </code>
/// 파티클이 <b>잡(Job)</b> 으로 돌기 때문에 값이 두 배로 커진다. 잡이 뜨면 Unity 가 워커
/// 스레드를 깨우는데, 코어가 많은 기계일수록 워커가 많이 생기고 그것들이 일감을 기다리며
/// 돈다. 실측에서 워커 21개가 각자 9~14%씩 태우고 있었다. <b>한 놈이 범인이 아니라
/// 스무 개가 조금씩 먹는 구조라 작업 관리자로는 안 보인다.</b>
///
/// <b>왜 꺼도 되는가.</b> 코드베이스 전체에 <c>OnParticleCollision</c> 도
/// <c>GetCollisionEvents</c> 도 없다. 파티클이 게임 로직에 관여하지 않는다는 뜻이다.
/// 로비 파티클 122개를 조사했을 때 <b>리지드바디에 힘을 주는 것 0개, 충돌 메시지 0개,
/// Trigger 0개</b> 였다. 물리에도 영향이 없다.
///
/// ⚠ <b>렌더러만 꺼서는 안 멈춘다.</b> 배 서버는 <c>ParticleSystemRenderer</c> 만 끄고 있어서
///    "껐다고 믿는데 시뮬레이션은 도는" 상태였다. 비용은 그리는 쪽이 아니라 <b>구르는 쪽</b>에 있다.
///
/// ⚠ <b><c>ParticleSystem</c> 에는 <c>enabled</c> 가 없다.</b> <c>Behaviour</c> 가 아니라
///    <c>Component</c> 다. 그래서 부품을 끄는 대신 <b>일감을 없앤다</b> —
///    살아 있는 입자를 전부 비우고 방출을 잠근다.
///
/// ⚠ <b>씬 파일을 세면 안 된다.</b> 로비는 <c>PrefabInstance</c> 가 5,532개라 파티클이 전부
///    프리팹 안에 있다. 씬 YAML 만 세면 <b>0개로 보인다.</b> 실제로 그 착각 때문에 원인을
///    한참 못 찾았다. 그래서 여기서는 <b>실행 중인 오브젝트</b>를 찾는다.
/// </summary>
public static class DedicatedServerParticles
{
    /// <summary>
    /// 씬 전체의 파티클을 멈춘다. 끈 개수를 돌려준다.
    ///
    /// ⚠ <b>이 부품이 붙은 가지만 보지 않는다.</b> 파티클은 씬 어디에나 있으므로
    ///    <c>FindObjectsByType</c> 으로 전부 찾는다.
    ///
    /// ⚠ <b>스폰되는 오브젝트는 못 잡는다.</b> 이 함수는 부르는 시점에 존재하는 것만 본다.
    ///    다행히 플레이어 프리팹에는 파티클이 없어 사람이 늘어도 다시 쌓이지 않는다.
    ///    나중에 이펙트가 붙은 무언가를 런타임에 스폰하게 되면 그때 다시 봐야 한다.
    /// </summary>
    public static int DisableAll()
    {
        ParticleSystem[] found = FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int turnedOff = 0;

        foreach (ParticleSystem one in found)
        {
            if (one == null) continue;

            // 살아 있는 입자를 비운다. 굴릴 것이 0개가 되면 프레임당 비용이 사라진다.
            one.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 다시 켜지지 않게 방출을 잠근다. 어딘가에서 Play() 를 불러도 안 나온다.
            ParticleSystem.EmissionModule emission = one.emission;
            emission.enabled = false;

            // 그리는 쪽도 같이 끈다. 서버에는 카메라가 없지만 표시를 남겨 둔다.
            if (one.TryGetComponent(out ParticleSystemRenderer renderer))
            {
                renderer.enabled = false;
            }

            turnedOff++;
        }

        return turnedOff;
    }

    /// <summary>
    /// <see cref="FindObjectsByType{T}(FindObjectsInactive, FindObjectsSortMode)"/> 를 감싼다.
    /// 이 클래스는 <c>MonoBehaviour</c> 가 아니라 그 메서드를 물려받지 않는다.
    /// </summary>
    private static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort)
        where T : Object
    {
        return Object.FindObjectsByType<T>(inactive, sort);
    }
}
