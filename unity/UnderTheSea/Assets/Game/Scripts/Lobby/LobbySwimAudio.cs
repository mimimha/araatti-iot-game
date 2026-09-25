using UnderTheSea.Audio;
using UnderTheSea.Network;
using UnityEngine;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 로비 바다 **헤엄 · 잠수 소리.** 내 캐릭터 것만 낸다.
    ///
    ///   · 헤엄치며 팔을 저으면  <see cref="swimStrokes"/> 를 한 번씩 (달리면 더 자주)
    ///   · 머리가 물에 잠기는 순간  <see cref="diveIn"/>,  다시 나오는 순간  <see cref="surfaceOut"/>
    ///   · 잠겨 있는 동안        <see cref="underwaterLoop"/> 를 깔아 둔다
    ///
    /// <b>왜 내 것만인가.</b> 허브는 2D 라 남의 캐릭터 소리도 바로 귀 옆에서 난다(AUDIO.md 2장).
    /// 여럿이 헤엄치면 누구 소리인지 모른 채 시끄럽기만 하다.
    ///
    /// 헤엄 여부는 서버가 정해 복제하는 <see cref="NetworkPlayerMover.Swimming"/> · <see cref="NetworkPlayerMover.AnimAxis"/> ·
    /// <see cref="NetworkPlayerMover.Ascending"/> 를 본다(AUDIO.md 4.2). 머리가 잠겼는지는
    /// 물속 화면과 같은 기준(<see cref="LobbyUnderwaterView.IsSubmerged"/>)을 쓴다. 소리와 화면이 따로 바뀌면 어색하다.
    ///
    /// <c>Resources/LobbyUnderwater.prefab</c> 에 <see cref="LobbyUnderwaterView"/> 와 함께 붙는다.
    /// 로비를 벗어나면 프리팹이 꺼지고 루프도 같이 끈다. 클립은 <c>Tools/아라아띠/로비 물속 연출 프리팹 만들기</c> 가
    /// <c>Assets/Game/Audio/Lobby/</c> 에서 필드 이름과 같은 파일을 찾아 채운다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LobbyUnderwaterView))]
    public class LobbySwimAudio : MonoBehaviour
    {
        [Header("🏊 헤엄")]
        [Tooltip("팔을 한 번 저을 때 나는 물소리. 여러 개면 돌아가며 낸다(같은 것이 연달아 나지 않게).")]
        [SerializeField] private AudioClip[] swimStrokes = System.Array.Empty<AudioClip>();

        [Tooltip("젓는 간격(초). 헤엄 모션 한 바퀴와 맞춘다. NetworkPlayerMover 의 모션 속도 1 기준이다.")]
        [SerializeField] private float strokeInterval = 1.1f;

        [Tooltip("Shift 로 빨리 헤엄칠 때의 간격(초). 모션 속도 1.4 에 맞춰 짧다.")]
        [SerializeField] private float sprintStrokeInterval = 0.8f;

        [Header("🤿 잠수")]
        [Tooltip("머리가 물에 잠기는 순간.")]
        [SerializeField] private AudioClip diveIn;

        [Tooltip("잠겼던 머리가 물 밖으로 나오는 순간.")]
        [SerializeField] private AudioClip surfaceOut;

        [Tooltip("잠겨 있는 동안 깔리는 먹먹한 물속 소리(루프).")]
        [SerializeField] private AudioClip underwaterLoop;

        [Header("🔉 크기")]
        [SerializeField, Range(0f, 1f)] private float strokeLevel = 0.6f;

        [Tooltip("물속에서 젓는 소리는 먹먹하게 줄인다. strokeLevel 에 곱한다.")]
        [SerializeField, Range(0f, 1f)] private float underwaterStrokeScale = 0.5f;

        [SerializeField, Range(0f, 1f)] private float diveLevel = 0.8f;
        [SerializeField, Range(0f, 1f)] private float underwaterLoopLevel = 0.5f;

        /// <summary>허브 루프 이름. "씬.용도" 로 다른 게임과 안 겹치게 한다.</summary>
        private const string UnderwaterLoopKey = "lobby.underwater";

        private AudioHub hub;
        private AudioHub.LoopHandle underwater;
        private LobbyUnderwaterView view;

        private NetworkPlayerMover mover;
        private Transform moverFor;

        private bool submergedSeen;
        private float nextStrokeAt;
        private int lastStroke = -1;

        private void Awake()
        {
            view = GetComponent<LobbyUnderwaterView>();
        }

        private void OnEnable()
        {
            hub = AudioHub.Instance;

            if (hub == null || !hub.CanHear)
            {
                // 서버 · 앱 종료 중. 이 프리팹은 화면이 있는 곳에만 뜨지만 한 번 더 막는다.
                enabled = false;
                return;
            }

            underwater = hub.Loop(UnderwaterLoopKey, underwaterLoop, 0.6f);

            // 켜지자마자 "잠겼다" 로 치지 않는다. 로비로 돌아오자마자 첨벙 소리가 나면 이상하다.
            submergedSeen = false;
            nextStrokeAt = 0f;
        }

        private void OnDisable()
        {
            // 로비를 벗어나면 꺼진다. 물속 소리가 미니게임까지 따라가면 안 된다.
            // StopAllLoops 는 쓰지 않는다. 로비의 다른 루프까지 끄게 된다.
            if (underwater != null)
            {
                underwater.Target = 0f;
            }

            mover = null;
            moverFor = null;
        }

        private void Update()
        {
            NetworkPlayerMover me = FindLocalMover();
            bool swimming = me != null && me.Object != null && me.Object.IsValid && me.Swimming;
            bool submerged = swimming && view.IsSubmerged(me);

            // 1) 잠기고 나온 순간. 캐릭터가 사라져서 풀린 것은 나온 게 아니니 소리 없이 넘긴다.
            if (submerged != submergedSeen)
            {
                submergedSeen = submerged;

                if (me != null)
                {
                    hub.PlayOneShot(submerged ? diveIn : surfaceOut, diveLevel);
                }
            }

            // 2) 잠겨 있는 동안 루프 — 목표만 정하면 허브가 페이드한다
            underwater.Target = submerged ? underwaterLoopLevel : 0f;

            // 3) 젓는 소리. 제자리에 떠 있을 때는 조용히 둔다.
            bool stroking = swimming && (me.AnimAxis.sqrMagnitude > 0.0001f || me.Ascending);

            if (!stroking)
            {
                // 다시 젓기 시작하면 바로 한 번 나게 한다.
                nextStrokeAt = 0f;
                return;
            }

            if (Time.time < nextStrokeAt)
            {
                return;
            }

            nextStrokeAt = Time.time + (me.Running ? sprintStrokeInterval : strokeInterval);
            hub.PlayOneShot(PickStroke(), strokeLevel * (submerged ? underwaterStrokeScale : 1f));
        }

        private NetworkPlayerMover FindLocalMover()
        {
            Transform me = LocalPlayer.Transform;

            if (me != moverFor)
            {
                moverFor = me;
                mover = me != null ? me.GetComponent<NetworkPlayerMover>() : null;
            }

            return mover;
        }

        /// <summary>같은 소리가 연달아 나지 않게 고른다. 비어 있으면 null — 허브가 무시한다.</summary>
        private AudioClip PickStroke()
        {
            if (swimStrokes == null || swimStrokes.Length == 0)
            {
                return null;
            }

            int pick = Random.Range(0, swimStrokes.Length);

            if (swimStrokes.Length > 1 && pick == lastStroke)
            {
                pick = (pick + 1) % swimStrokes.Length;
            }

            lastStroke = pick;
            return swimStrokes[pick];
        }
    }
}
