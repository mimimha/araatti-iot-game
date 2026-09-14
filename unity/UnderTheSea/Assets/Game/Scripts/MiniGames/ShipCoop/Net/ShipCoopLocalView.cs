using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 내 캐릭터에만 붙는 로컬 연출. **씬에 이미 있는 카메라를 재사용한다.**
    ///
    /// Lobby 의 <c>LocalPlayerView</c> 와 역할은 같지만 **다른 클래스다.**
    /// 그쪽은 <c>LobbyGameplayCamera</c> 표식을 찾고 없으면 오류를 내며, 포털과 Loading Overlay 에
    /// 묶여 있다. ShipCoop 에는 그 셋이 모두 없다. 섞으면 서로의 사정을 끌고 들어온다.
    ///
    /// <b>하는 일</b>
    ///   1. 화면에 그려지는 카메라를 하나로 만든다
    ///   2. 내 <c>TaskWorker</c> 를 HUD 에 알린다 — 카메라가 비출 갑판이 여기서 정해진다
    ///   3. <b>내 컨트롤러만</b> 켠다 — 카메라 Q/E 회전이 여기서 나온다
    ///
    /// <b>왜 카메라를 하나로 만들어야 하는가.</b>
    /// <c>PeerMode.Multiple</c> 에서 Fusion 은 씬을 러너 전용 씬으로 인수하면서
    /// **같은 씬이 두 벌 뜬 것처럼 보이는 상태**를 만든다. 카메라가 둘 남으면 같은 depth 로
    /// 둘 다 그려져 엉뚱한 시점이 덮어써지고, AudioListener 도 둘이 되어 경고가 난다.
    /// Lobby 에서 실제로 겪은 일이다. (PRD 08-3)
    ///
    /// <b>어떻게 내 카메라를 고르는가.</b>
    /// 씬 이름이나 탐색 순서에 기대지 않는다. <c>ShipCoopCamera</c> 컴포넌트가 붙어 있는 것이
    /// ShipCoop 카메라라는 선언이고, 그중 <b>내 캐릭터와 같은 씬</b>에 있는 것을 고른다.
    /// 고를 수 없으면 아무거나 집지 않고 오류를 남긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopLocalView : NetworkBehaviour
    {
        private ShipCoopCamera boundCamera;

        public override void Spawned()
        {
            // 내 캐릭터가 아니면 아무것도 하지 않는다. 서버와 남의 캐릭터가 여기서 걸러진다.
            if (!HasInputAuthority)
            {
                return;
            }

            boundCamera = ResolveCamera();

            if (boundCamera == null)
            {
                Debug.LogError(
                    "[ShipCoopLocalView] 쓸 수 있는 ShipCoop 카메라를 찾지 못했습니다. " +
                    "씬의 Main Camera 에 ShipCoopCamera 가 붙어 있는지 확인해 주세요.", this);
                return;
            }

            KeepOnlyThisViewer(boundCamera.GetComponent<Camera>());
            EnableMyController();
            BindLocalWorker();
        }

        /// <summary>
        /// 내 캐릭터의 컨트롤러를 켠다. **이 컴퓨터의 키보드는 내 캐릭터 것만 읽는다.**
        ///
        /// <b>왜 프리팹에는 꺼서 넣는가.</b> 켜서 넣으면 같은 화면에 있는
        /// <b>남의 캐릭터 복사본</b>도 내 키보드를 읽는다. 컨트롤러는 사람마다 하나씩이지
        /// 컴퓨터마다 하나가 아니다.
        ///
        /// <b>왜 켜야 하는가.</b> <c>ShipCoopCamera</c> 의 좌우 회전(Q/E)은
        /// <c>hud.LocalWorker.Input.Look.x</c> 에서 온다. 그 값은
        /// <c>KeyboardPlayerController.Update</c> 가 채우므로, 꺼 두면 Q/E 가 통째로 죽는다.
        /// 1단계 QA 에서 실제로 카메라가 전혀 돌지 않았다.
        ///
        /// ⚠ <c>TaskWorker</c> 는 여전히 꺼 둔다. 작업 입력은 아직 서버로 가지 않아,
        ///    켜면 자기 화면에서만 자리에 붙는다. 자리 붙기는 2단계다.
        /// </summary>
        private void EnableMyController()
        {
            MonoBehaviour controller = GetComponent<IPlayerController>() as MonoBehaviour;

            if (controller == null)
            {
                Debug.LogWarning(
                    "[ShipCoopLocalView] 내 IPlayerController 가 없습니다. 카메라 Q/E 가 동작하지 않습니다.", this);
                return;
            }

            controller.enabled = true;
            Debug.Log($"[ShipCoopLocalView] 내 컨트롤러 '{controller.GetType().Name}' 를 켰습니다. (Q/E 카메라 회전)");
        }

        /// <summary>
        /// 내 캐릭터와 같은 씬에 있는 ShipCoop 카메라.
        ///
        /// 후보가 하나뿐이면 그것을 쓴다. 여럿이면 같은 씬을 고르고, 그래도 못 고르면 null.
        /// </summary>
        private ShipCoopCamera ResolveCamera()
        {
            ShipCoopCamera[] all = FindObjectsByType<ShipCoopCamera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (all.Length == 0)
            {
                return null;
            }

            if (all.Length == 1)
            {
                return all[0];
            }

            List<ShipCoopCamera> sameScene = new List<ShipCoopCamera>();
            foreach (ShipCoopCamera candidate in all)
            {
                if (candidate.gameObject.scene == gameObject.scene)
                {
                    sameScene.Add(candidate);
                }
            }

            if (sameScene.Count == 1)
            {
                return sameScene[0];
            }

            Debug.LogError(
                $"[ShipCoopLocalView] ShipCoop 카메라가 {all.Length}개인데 " +
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
            if (keep == null)
            {
                return;
            }

            foreach (Camera other in FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == keep || other.targetTexture != null)
                {
                    continue;
                }

                other.enabled = false;

                AudioListener listener = other.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }

                Debug.Log(
                    $"[ShipCoopLocalView] 화면에 겹쳐 그려지던 카메라 '{other.name}'" +
                    $"(씬 '{other.gameObject.scene.name}')를 껐습니다.");
            }

            keep.enabled = true;

            AudioListener keepListener = keep.GetComponent<AudioListener>();
            if (keepListener != null)
            {
                keepListener.enabled = true;
            }

            // ⚠ PlayerInputProvider 가 Camera.main 의 y 각도를 읽어 서버로 보낸다.
            //    태그가 없으면 Camera.main 이 null 이라 LookYaw 가 0 으로 가고,
            //    화면을 돌려도 캐릭터는 월드 +z 기준으로 걷는다. 조용히 틀리는 종류다.
            if (!keep.CompareTag("MainCamera"))
            {
                keep.tag = "MainCamera";
                Debug.Log($"[ShipCoopLocalView] '{keep.name}' 을 MainCamera 로 표시했습니다.");
            }

            Debug.Log(
                $"[ShipCoopLocalView] 내 카메라 '{keep.name}'(씬 '{keep.gameObject.scene.name}') 확정. " +
                $"Camera.main = '{(Camera.main != null ? Camera.main.name : "없음")}'");
        }

        /// <summary>
        /// HUD 에 "이 사람이 화면을 보는 사람" 이라고 알린다.
        ///
        /// <c>ShipCoopCamera</c> 가 <c>hud.LocalWorker</c> 로 어느 갑판을 비출지 정하므로,
        /// 이것을 안 하면 **남의 캐릭터가 선 갑판**을 비춘다.
        /// HUD 가 없는 구성(서버 · 최소 씬)에서는 조용히 넘어간다.
        /// </summary>
        private void BindLocalWorker()
        {
            TaskWorker mine = GetComponent<TaskWorker>();

            if (mine == null)
            {
                Debug.LogWarning("[ShipCoopLocalView] 내 TaskWorker 가 없습니다. HUD 에 알리지 못했습니다.", this);
                return;
            }

            ShipCoopHud hud = FindAnyObjectByType<ShipCoopHud>(FindObjectsInactive.Include);

            if (hud == null)
            {
                Debug.Log("[ShipCoopLocalView] 이 씬에는 HUD 가 없습니다. 카메라는 기본 대상을 비춥니다.");
                return;
            }

            hud.SetLocalWorker(mine);
            Debug.Log($"[ShipCoopLocalView] HUD 에 내 TaskWorker 를 알렸습니다. ({Object.InputAuthority})");
        }
    }
}
