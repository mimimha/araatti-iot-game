using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnderTheSea.Network;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 내 캐릭터에만 붙는 로컬 연출. **씬에 이미 있는 카메라를 재사용한다.**
    ///
    /// <b>하는 일</b>
    ///   1. 화면에 그려지는 카메라를 하나로 만든다
    ///   2. 그 카메라가 <b>내 캐릭터</b>를 따라가게 한다
    ///
    /// <b>왜 카메라를 하나로 만들어야 하는가.</b>
    /// <c>PeerMode.Multiple</c> 에서 Fusion 은 씬을 러너 전용 씬으로 인수하면서
    /// **같은 씬이 두 벌 뜬 것처럼 보이는 상태**를 만든다. 카메라가 둘 남으면 같은 depth 로
    /// 둘 다 그려져 엉뚱한 시점이 덮어써지고, AudioListener 도 둘이 되어 경고가 난다.
    ///
    /// <b>왜 카메라가 내 캐릭터를 봐야 하는가.</b>
    /// Warriors 의 카메라는 아레나 프리팹 안에 <b>한 대뿐</b>이고
    /// <c>WarriorsThirdPersonCamera.Configure(target)</c> 로 대상이 정해진다.
    /// 그대로 두면 두 사람이 <b>같은 캐릭터</b>를 보게 된다.
    ///
    /// ⚠ 카메라 태그도 여기서 확인한다. <c>WarriorsInputProvider</c> 가
    ///    <c>Camera.main</c> 의 y 각도를 읽어 서버로 보내므로, 태그가 없으면
    ///    화면을 돌려도 캐릭터는 월드 +z 기준으로 걷는다. 조용히 틀리는 종류다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsLocalView : NetworkBehaviour
    {
        /// <summary>한 프레임에 이만큼(m) 넘게 움직였으면 걸어온 것이 아니라 옮겨진 것이다.</summary>
        private const float TeleportDistance = 3f;

        private WarriorsThirdPersonCamera follow;
        private Vector3 lastPosition;

        public override void Spawned()
        {
            // 내 캐릭터가 아니면 아무것도 하지 않는다. 서버와 남의 캐릭터가 여기서 걸러진다.
            if (!HasInputAuthority)
            {
                return;
            }

            follow = ResolveCamera();

            if (follow == null)
            {
                Debug.LogError(
                    "[WarriorsLocalView] 쓸 수 있는 카메라를 찾지 못했습니다. " +
                    "아레나 프리팹에 WarriorsThirdPersonCamera 가 있는지 확인해 주세요.", this);
                return;
            }

            KeepOnlyThisViewer(follow.GetComponent<Camera>());

            follow.Configure(transform);

            // 곧바로 내 캐릭터 뒤, 캐릭터가 보는 쪽(바다)으로 자리를 잡는다.
            // 원본 WarriorsGameFlow 는 판을 시작할 때 SnapToTarget 을 불렀는데 네트워크 경로에는 빠져 있었다.
            // 그래서 아레나에 놓인 카메라 자리(스폰 줄 바로 뒤 2.8m 높이)에서 캐릭터 머리를 내려다보며
            // 시작해 뒤로 미끄러져 나갔다.
            follow.SnapToTarget(transform.eulerAngles.y);
            lastPosition = transform.position;

            Debug.Log($"[WarriorsLocalView] 카메라를 내 캐릭터에 붙였습니다. ({Object.InputAuthority})");

            StartCoroutine(FinishLoadingWhenPlayable());
        }

        /// <summary>
        /// <b>정말 놀 수 있게 됐을 때</b> 로딩 화면을 걷는다.
        ///
        /// 포탈로 들어오면 <c>MiniGameTransition</c> 이 "게임에 입장 중..." 을 켜 두는데, 그것을
        /// 내리는 쪽은 미니게임이다. 내리지 않으면 <b>로딩 화면에 갇힌다.</b>
        ///
        /// 반대로 너무 일찍 내려도 안 된다. 씬을 여느라 한 프레임이 길게 멈추고 그 뒤 밀린 틱을
        /// 몰아서 따라잡는데, 그 구간을 사용자에게 넘기면 조작이 밀린다고 느낀다. 배 게임에서
        /// 실측으로 겪은 일이라 같은 기준을 쓴다 — 외형이 입혀지고 화면이 한두 프레임 더 흐른 뒤.
        ///
        /// ⚠ <b>어떤 경우에도 갇히지 않는다.</b> 외형이 끝내 오지 않아도 상한을 넘기면 넘긴다.
        ///    조작이 잠깐 어색한 것보다 갇히는 쪽이 훨씬 나쁘다. 배 게임에서 실제로 한 번
        ///    2분 넘게 갇혔고, 그 뒤로 이 상한을 둔다.
        /// </summary>
        private IEnumerator FinishLoadingWhenPlayable()
        {
            NetworkPlayerAppearance appearance = GetComponent<NetworkPlayerAppearance>();

            float waited = 0f;

            while (appearance != null && !appearance.AppearanceReady && waited < GiveUpAfterSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (appearance != null && !appearance.AppearanceReady)
            {
                Debug.LogWarning(
                    $"[WarriorsLocalView] 외형이 {GiveUpAfterSeconds:0}초 안에 오지 않아 그대로 화면을 넘깁니다.", this);
            }

            // 외형을 입히고 그리는 데 한두 프레임이 더 든다. 그 사이를 보여 주지 않는다.
            yield return null;
            yield return null;

            TransitionStatus.SetReady();

            Debug.Log("[WarriorsLocalView] 준비가 끝나 화면을 넘깁니다.");
        }

        /// <summary>외형을 이만큼 기다려도 안 오면 포기하고 넘긴다. (초)</summary>
        private const float GiveUpAfterSeconds = 15f;

        /// <summary>
        /// 서버가 내 캐릭터를 순간이동시켰으면(2 · 3페이즈 자리 배치) 카메라도 같이 뛴다.
        ///
        /// 그대로 두면 카메라가 해변에서 크라켄 앞까지 천천히 미끄러져 오고, 라운드 소개 카드가
        /// 떠 있는 3초 동안 엉뚱한 곳을 비춘다. 옮겨진 순간 캐릭터 뒤에서 캐릭터가 보는 쪽으로 다시 잡는다.
        /// 걷는 동안에는 한 프레임에 3m 를 넘을 수 없어 오작동하지 않는다.
        /// </summary>
        /// <summary>
        /// 아레나 고정 구도의 수치. **1920x1080 화면에 맞춰 잡았다.**
        ///
        /// 바라보는 점을 사람들보다 <see cref="AimUp"/> 만큼 높이 두면 캐릭터가 화면 아래로
        /// 내려가고, 그 위가 리듬 트랙 자리가 된다. 뒤로 <see cref="Back"/> 물러나면
        /// 좌우 ±2.8m 에 선 두 사람이 한 화면에 모두 들어온다.
        /// </summary>
        private const float Back = 9f;

        private const float Up = 3.2f;

        private const float AimUp = 4.4f;

        public override void Render()
        {
            if (!HasInputAuthority || follow == null) return;

            // 2 · 3페이즈는 고정 구도. 두 화면이 같은 그림을 보고, 화면에 고정된 리듬 트랙이
            // 캐릭터 위에 정확히 얹힌다. 1페이즈는 해변을 뛰어다니므로 따라가는 카메라 그대로.
            WarriorsMatchState match = WarriorsMatchState.Current;
            bool arenaShot = match != null && match.Object != null && match.Object.IsValid && match.MovementLocked;

            if (arenaShot)
            {
                follow.FocusArena(ArenaCentre(), Back, Up, AimUp);
                return;
            }

            follow.ReleaseFixed();

            Vector3 now = transform.position;

            if ((now - lastPosition).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                follow.SnapToTarget(transform.eulerAngles.y);
                Debug.Log("[WarriorsLocalView] 캐릭터가 옮겨져 카메라를 다시 잡았습니다.");
            }

            lastPosition = now;
        }

        /// <summary>
        /// 살아 있는 사람들의 한가운데. **좌표를 박아 두지 않고 실제 자리에서 잰다.**
        ///
        /// 담당 자리(<c>stands</c>)가 씬에서 바뀌어도 구도가 따라간다. 아무도 못 찾으면 내 자리를 쓴다.
        /// </summary>
        private Vector3 ArenaCentre()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life == null || !life.IsLive) continue;

                sum += life.transform.position;
                count++;
            }

            return count > 0 ? sum / count : transform.position;
        }

        /// <summary>
        /// 내 캐릭터와 같은 씬에 있는 카메라.
        ///
        /// 후보가 하나뿐이면 그것을 쓴다. 여럿이면 같은 씬을 고르고, 그래도 못 고르면 null.
        /// 씬 이름이나 탐색 순서에 기대지 않는다.
        /// </summary>
        private WarriorsThirdPersonCamera ResolveCamera()
        {
            WarriorsThirdPersonCamera[] all = FindObjectsByType<WarriorsThirdPersonCamera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (all.Length == 0) return null;
            if (all.Length == 1) return all[0];

            List<WarriorsThirdPersonCamera> sameScene = new List<WarriorsThirdPersonCamera>();

            foreach (WarriorsThirdPersonCamera candidate in all)
                if (candidate.gameObject.scene == gameObject.scene) sameScene.Add(candidate);

            if (sameScene.Count == 1) return sameScene[0];

            Debug.LogError(
                $"[WarriorsLocalView] 카메라가 {all.Length}개인데 " +
                $"내 씬('{gameObject.scene.name}')에 있는 것이 {sameScene.Count}개라 고를 수 없습니다.", this);

            return null;
        }

        /// <summary>
        /// 화면에 그려지는 카메라를 하나로 만든다.
        ///
        /// RenderTexture 로 그리는 카메라(반사 · 프리뷰)는 화면을 건드리지 않으므로 놔둔다.
        /// </summary>
        private void KeepOnlyThisViewer(Camera keep)
        {
            if (keep == null) return;

            foreach (Camera other in FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == keep || other.targetTexture != null) continue;

                other.enabled = false;

                AudioListener listener = other.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;

                Debug.Log(
                    $"[WarriorsLocalView] 화면에 겹쳐 그려지던 카메라 '{other.name}'" +
                    $"(씬 '{other.gameObject.scene.name}')를 껐습니다.");
            }

            keep.enabled = true;

            AudioListener keepListener = keep.GetComponent<AudioListener>();
            if (keepListener != null) keepListener.enabled = true;

            if (!keep.CompareTag("MainCamera"))
            {
                keep.tag = "MainCamera";
                Debug.Log($"[WarriorsLocalView] '{keep.name}' 을 MainCamera 로 표시했습니다.");
            }

            Debug.Log(
                $"[WarriorsLocalView] 내 카메라 '{keep.name}'(씬 '{keep.gameObject.scene.name}') 확정. " +
                $"Camera.main = '{(Camera.main != null ? Camera.main.name : "없음")}'");
        }
    }
}
