using UnityEngine;

/// <summary>
/// 💦 뱃전에 물을 버렸을 때 튀는 물. **연출 전용**이다. 판정 · 수치에 영향이 없다. (SHIPCOOP.md 4장)
///
/// <c>WaterDumpPoint.Dump()</c> 가 실제로 물을 줄였을 때 <see cref="Play"/> 를 부른다.
/// 재생 코드를 <c>WaterDumpPoint</c> 안에 두지 않고 여기로 뺀 이유 — 네트워크에서는 판정이 서버에서만 돌아
/// 클라이언트는 그 순간을 모른다. 서버가 "몇 번 버렸다" 를 복제하면 클라이언트가 같은 <see cref="Play"/> 를
/// 부르면 된다. (<c>ShipCoopStateSync</c>, 11장 — event Action 은 클라이언트에서 안 터진다)
///
/// 파티클은 배치 도구(<c>ShipCoopDeckLayout.DressDumps</c>)가 <c>FX_WaterSplash.prefab</c> 을 자식으로 넣고 연결한다.
/// 소리는 넣지 않는다. 오디오는 따로 정한다.
/// </summary>
[DisallowMultipleComponent]
public class WaterDumpSplash : MonoBehaviour
{
    [Header("연결 — 배치 도구가 채운다")]
    [SerializeField] private ParticleSystem splash;

    /// <summary>한 번 튀긴다. 이미 튀는 중이면 처음부터 다시.</summary>
    public void Play()
    {
        if (splash == null)
        {
            return;
        }

        splash.Play(true);
    }
}
