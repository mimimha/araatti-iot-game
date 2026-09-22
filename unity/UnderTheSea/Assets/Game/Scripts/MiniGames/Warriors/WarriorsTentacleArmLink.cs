using UnityEngine;

namespace Warriors
{
    /// <summary>
    /// 🐙 <b>베는 촉수 하나를 크라켄 머리의 진짜 팔에 묶는다.</b>
    ///
    /// 예전에는 촉수 자리마다 프리팹이 만든 임시 튜브(막대)가 서 있었고, 뒤의 크라켄 머리와
    /// 따로 놀았다. 이제 머리 모델(<c>KrakenPhase2_Rigged</c>)이 자기 팔 넷을 뼈로 갖고 있으므로,
    /// <b>그 팔이 곧 베는 대상</b>이다. 이 부품은 둘을 이어 준다.
    ///
    /// <code>
    ///   올라옴     촉수 오브젝트가 켜지면 그 팔이 들린다 (SetRaised)
    ///   맞음       그 팔만 움찔한다 (PlayHit)
    ///   잘림       그 팔이 늘어진다 (PlayCut)
    ///   판정 상자  팔 끝을 따라다닌다 — 팔이 휘면 상자도 같이 간다
    /// </code>
    ///
    /// ⚠ <b>규칙은 하나도 건드리지 않는다.</b> 판정 · 약점 · 반격은 전부 <see cref="WarriorsTarget"/> 과
    ///    서버가 하던 그대로다. 이 부품은 <b>어디에 서서 어떻게 움직이는가</b>만 바꾼다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsTentacleArmLink : MonoBehaviour
    {
        [Tooltip("머리 겉모습에 붙은 뼈 부품. 생성 도구가 꽂는다.")]
        [SerializeField] private WarriorsKrakenLegs head;

        [Tooltip("이 촉수가 맡은 팔 번호 (머리 모델의 Leg<번호>).")]
        [SerializeField, Min(0)] private int leg;

        [Tooltip("표식을 팔 어디에 얹을까. 0 뿌리 … 1 끝. 끝에 달면 팔 밖에 매달린 것처럼 보인다.")]
        [SerializeField, Range(0f, 1f)] private float alongArm = 0.84f;

        [Tooltip("거기서 조금 더 띄우는 값(m). 팔 표면에 파묻히지 않을 만큼만.")]
        [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0f, -0.3f);


        /// <summary>이 촉수가 맡은 팔 번호.</summary>
        public int Leg => leg;

        /// <summary>머리 뼈 부품을 찾았는가.</summary>
        public bool Linked => head != null;

        /// <summary>
        /// 표식(↔ ↕ ⊙)이 붙어야 할 자리 — <b>이 촉수가 맡은 팔의 끝.</b>
        ///
        /// 판정 상자는 제자리에 둔다. 상자는 칼 사거리(베기 5.8m · 찌르기 8m) 안에 있어야 하는데
        /// 팔 끝은 그보다 멀다. 표식만 팔을 따라가면 <b>무엇을 베는지</b>는 그대로 읽힌다.
        ///
        /// ⚠ 이 자리를 쓰는 쪽은 <see cref="WarriorsTentacleIndicator"/> 다. 거기서 매 프레임
        ///    표식을 고정 오프셋으로 되돌려 놓기 때문에, 이쪽에서 옮겨 봐야 덮어써진다.
        /// </summary>
        public bool TryTipPosition(out Vector3 where)
        {
            if (head == null)
            {
                where = transform.position;
                return false;
            }

            where = head.PointOnArm(leg, alongArm) + transform.TransformVector(markerOffset);
            return true;
        }

        /// <summary>
        /// 맞았을 때 팔은 <b>움직이지 않는다.</b>
        ///
        /// 한때 맞을 때마다 팔을 움찔하게 했는데, 네 팔이 한꺼번에 반응해 무엇을 맞혔는지 알 수 없었다.
        /// 맞은 표시는 <b>표식이 터지는 것</b>으로 충분하다 — 팔은 늘 하던 잔물결만 친다.
        /// </summary>
        public void PlayHit(WarriorsAttackDirection direction) { }

        /// <summary>잘렸을 때도 팔은 움직이지 않는다. (위와 같은 이유)</summary>
        public void PlayCut() { }
    }
}
