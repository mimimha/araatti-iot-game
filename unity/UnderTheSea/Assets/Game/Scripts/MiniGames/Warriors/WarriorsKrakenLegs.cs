using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    /// <summary>
    /// 🐙 <b>크라켄 다리를 뼈로 움직인다.</b> 애니메이션 클립은 없다 — 이 부품이 매 프레임 뼈를 돌린다.
    ///
    /// 겉모습(<c>KrakenFinal_Rigged.fbx</c>)에는 다리마다 뼈 사슬이 들어 있다
    /// (<c>Root</c> · <c>Leg0_1 … Leg7_5</c>, 만든 방법은 <c>art/tools/warriors_kraken_rig.py</c>).
    /// 이 부품은 이름으로 그 사슬을 찾아 <b>쉴 때 잔물결 · 맞을 때 움찔 · 포효할 때 벌림</b>을 낸다.
    ///
    /// <code>
    ///   잔물결   늘. 다리마다 위상을 조금씩 어긋내 물속처럼 흐른다
    ///   움찔     노트 정타 (PlayHit). 모든 다리가 같은 방향으로 한 번 튕겼다 돌아온다
    ///   포효     콤보 피니시 · 최종 형태 등장 (PlayRoar). 크게 벌렸다 천천히 돌아온다
    /// </code>
    ///
    /// ⚠ <b>연출은 서버에서 돌리지 않는다.</b> 부르는 쪽(<see cref="WarriorsKrakenBoss"/>)이 이미
    ///    복제된 번호를 보고 각 화면에서 부른다. 데디케이티드 서버에서는
    ///    <c>WarriorsServerCleanup</c> 이 이 부품을 끈다.
    ///
    /// ⚠ <b>다리를 서로 반대로 크게 꺾지 않는다.</b> 모델(Meshy 단일 덩어리)은 팔이 서로 닿는 자리가
    ///    붙어 있어, 이웃한 팔을 반대 방향으로 크게 돌리면 그 자리가 찢어진다. 실측으로 확인했다 —
    ///    이웃끼리 위상만 어긋내고 진폭을 10도 안쪽으로 두면 깨끗하다. 움찔 · 포효는 <b>모든 다리가
    ///    같은 방향</b>으로 움직여 애초에 찢어질 일이 없게 했다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsKrakenLegs : MonoBehaviour
    {
        [Header("휘는 축")]
        [Tooltip("다리가 휘는 회전축. 뼈대 루트의 로컬 방향이다. 크라켄은 팔이 한 면에 펼쳐져 있어 " +
                 "그 면의 법선(기본 Z)으로 돌리면 화면에서 좌우로 물결친다.")]
        [SerializeField] private Vector3 swayAxis = Vector3.forward;

        [Header("잔물결 — 늘")]
        [Tooltip("뼈 하나가 흔들리는 최대 각도(도). 이웃 팔이 붙어 있어 10도를 넘기지 않는다.")]
        [SerializeField, Range(0f, 15f)] private float idleDegrees = 4f;

        [Tooltip("물결 속도(라디안/초).")]
        [SerializeField, Range(0.1f, 4f)] private float idleSpeed = 1.1f;

        [Tooltip("팔이 향한 각도 1도당 위상 몇 도. 0 이면 여덟 팔이 한 몸처럼 움직여 뻣뻣하다. " +
                 "번호 순서가 아니라 각도로 주는 이유는 아래 groupDegrees 를 보라.")]
        [SerializeField, Range(0f, 3f)] private float phaseSpread = 1.4f;

        [Tooltip("이 각도 안에 있는 팔들은 같은 위상으로 묶는다(도). 모델이 한 덩어리라 위쪽 두 팔처럼 " +
                 "서로 겹친 자리는 정점을 나눠 가진다. 그 둘을 따로 흔들면 겹친 데가 접히고 늘어나 이상하게 보인다.")]
        [SerializeField, Range(0f, 90f)] private float groupDegrees = 32f;

        [Header("움찔 — 노트 정타")]
        [Tooltip("맞았을 때 튕기는 각도(도).")]
        [SerializeField, Range(0f, 20f)] private float hitDegrees = 7f;

        [Tooltip("튕김이 잦아드는 시간(초).")]
        [SerializeField, Range(0.05f, 1.5f)] private float hitSeconds = 0.35f;

        [Tooltip("세게 맞았을 때 곱하는 값.")]
        [SerializeField, Range(1f, 3f)] private float strongMultiplier = 1.7f;

        [Header("포효 — 피니시 · 등장")]
        [Tooltip("포효할 때 벌리는 각도(도).")]
        [SerializeField, Range(0f, 25f)] private float roarDegrees = 12f;

        [Tooltip("포효가 잦아드는 시간(초).")]
        [SerializeField, Range(0.2f, 3f)] private float roarSeconds = 1.1f;

        [Header("뿌리 → 끝")]
        [Tooltip("뿌리 뼈가 받는 몫. 끝으로 갈수록 여기에 stepGain 씩 더해진다. 끝이 더 휘어야 팔처럼 보인다.")]
        [SerializeField, Range(0f, 1f)] private float rootGain = 0.35f;

        [SerializeField, Range(0f, 0.5f)] private float stepGain = 0.18f;

        /// <summary>다리 하나. 뿌리부터 끝까지 뼈가 순서대로 들어 있다.</summary>
        private sealed class Leg
        {
            public readonly List<Transform> Bones = new List<Transform>();
            public readonly List<Quaternion> Rest = new List<Quaternion>();
        }

        private readonly List<Leg> _legs = new List<Leg>();

        /// <summary>다리마다의 위상(라디안). 겹친 팔끼리는 같은 값이다.</summary>
        private float[] _phase;
        private Transform _rigRoot;

        private float _hit;        // 1 → 0 으로 잦아드는 움찔 세기 (전체)
        private float _hitPeak;    // 이번 움찔의 각도
        private float _roar;       // 1 → 0 으로 잦아드는 포효 세기

        // 팔 하나만 움직이는 값들. 2라운드에서 베는 팔이 각각 따로 반응해야 한다.
        private float[] _legHit;   // 1 → 0
        private float[] _legHitPeak;
        private float[] _legRaise; // 0 서 있지 않음 … 1 완전히 섰다
        private float[] _legRaiseWant;
        private float[] _legDroop; // 잘려서 늘어진 정도 0 … 1
        private bool[] _legDrooping;
        private Vector3[] _legAim;     // 팔 끝이 가야 할 자리 (월드)
        private float[] _legAimWeight; // 0 안 겨냥 … 1 완전히 겨냥

        [Header("2라운드 — 팔 하나만")]
        [Tooltip("촉수가 올라올 때 드는 각도(도). 베라고 내미는 자세다.")]
        [SerializeField, Range(0f, 40f)] private float raiseDegrees = 14f;

        [Tooltip("드는 데 걸리는 시간(초).")]
        [SerializeField, Range(0.05f, 2f)] private float raiseSeconds = 0.45f;

        [Tooltip("잘렸을 때 늘어지는 각도(도).")]
        [SerializeField, Range(0f, 60f)] private float droopDegrees = 26f;

        /// <summary>찾은 다리 수. 0 이면 뼈를 못 찾은 것이다 (겉모습이 옛 모델일 때).</summary>
        public int LegCount => _legs.Count;

        private void Awake()
        {
            Collect();
        }

        /// <summary>이름(<c>Leg&lt;다리&gt;_&lt;마디&gt;</c>)으로 뼈 사슬을 모은다.</summary>
        private void Collect()
        {
            _legs.Clear();
            var byLeg = new SortedDictionary<int, SortedDictionary<int, Transform>>();

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!n.StartsWith("Leg")) continue;

                int bar = n.IndexOf('_');
                if (bar < 4) continue;
                if (!int.TryParse(n.Substring(3, bar - 3), out int leg)) continue;
                if (!int.TryParse(n.Substring(bar + 1), out int seg)) continue;

                if (!byLeg.TryGetValue(leg, out var segs))
                {
                    segs = new SortedDictionary<int, Transform>();
                    byLeg[leg] = segs;
                }

                segs[seg] = t;
                if (_rigRoot == null && t.parent != null) _rigRoot = t.parent;
            }

            foreach (var pair in byLeg)
            {
                var leg = new Leg();
                foreach (var seg in pair.Value)
                {
                    leg.Bones.Add(seg.Value);
                    leg.Rest.Add(seg.Value.localRotation);   // 쉴 때 자세. 매 프레임 여기서 다시 시작한다
                }

                if (leg.Bones.Count > 0) _legs.Add(leg);
            }

            // 뼈대 루트를 못 찾았으면(= Leg 뼈의 부모가 없으면) 이 오브젝트를 기준으로 쓴다.
            if (_rigRoot == null) _rigRoot = transform;

            _legHit = new float[_legs.Count];
            _legHitPeak = new float[_legs.Count];
            _legRaise = new float[_legs.Count];
            _legRaiseWant = new float[_legs.Count];
            _legDroop = new float[_legs.Count];
            _legDrooping = new bool[_legs.Count];
            _legAim = new Vector3[_legs.Count];
            _legAimWeight = new float[_legs.Count];

            BuildPhases();
        }

        /// <summary>
        /// 팔이 <b>향한 각도</b>로 위상을 정하고, 가까이 붙은 팔끼리는 <b>같은 위상으로 묶는다.</b>
        ///
        /// 번호 순서로 위상을 주면(예전 방식) 겹쳐 있는 두 팔이 서로 반대로 움직일 수 있다.
        /// 모델이 한 덩어리라 겹친 자리는 정점을 나눠 가지므로, 그러면 그 자리가 접히고 늘어난다 —
        /// 화면에서 위쪽 팔 두 쌍이 실제로 그렇게 보였다. 각도로 주면 겹친 팔은 각도도 가까워
        /// 자연히 비슷해지고, <see cref="groupDegrees"/> 안이면 아예 같은 값으로 묶어 함께 움직인다.
        /// </summary>
        private void BuildPhases()
        {
            _phase = new float[_legs.Count];
            if (_legs.Count == 0) return;

            // 팔이 펼쳐진 면 위에서의 각도를 잰다. 면의 법선은 휘는 축과 같다.
            Vector3 n = swayAxis.sqrMagnitude > 0.0001f ? swayAxis.normalized : Vector3.forward;
            Vector3 u = Vector3.Cross(n, Vector3.up);
            if (u.sqrMagnitude < 0.0001f) u = Vector3.Cross(n, Vector3.right);
            u.Normalize();
            Vector3 v = Vector3.Cross(n, u);

            var angle = new float[_legs.Count];
            for (int k = 0; k < _legs.Count; k++)
            {
                Leg leg = _legs[k];
                Vector3 dir = _rigRoot.InverseTransformPoint(leg.Bones[leg.Bones.Count - 1].position)
                            - _rigRoot.InverseTransformPoint(leg.Bones[0].position);
                angle[k] = Mathf.Atan2(Vector3.Dot(dir, v), Vector3.Dot(dir, u)) * Mathf.Rad2Deg;
            }

            // 가까운 각도끼리 한 무리로 묶고, 그 무리의 각도를 함께 쓴다.
            var group = new int[_legs.Count];
            for (int k = 0; k < group.Length; k++) group[k] = -1;

            int groups = 0;
            for (int k = 0; k < _legs.Count; k++)
            {
                if (group[k] >= 0) continue;

                group[k] = groups;
                for (int j = k + 1; j < _legs.Count; j++)
                    if (group[j] < 0 && Mathf.Abs(Mathf.DeltaAngle(angle[k], angle[j])) <= groupDegrees)
                        group[j] = groups;

                groups++;
            }

            // 무리마다 평균 각도. 각도는 도는 값이라 k 번째를 기준으로 차이를 더해 평균 낸다.
            var sum = new float[groups];
            var count = new int[groups];
            var anchor = new float[groups];
            for (int k = 0; k < _legs.Count; k++)
            {
                int g = group[k];
                if (count[g] == 0) anchor[g] = angle[k];
                sum[g] += anchor[g] + Mathf.DeltaAngle(anchor[g], angle[k]);
                count[g]++;
            }

            for (int k = 0; k < _legs.Count; k++)
                _phase[k] = sum[group[k]] / count[group[k]];

            var report = new System.Text.StringBuilder("[KrakenLegs] 다리 ").Append(_legs.Count).Append("개 —");
            for (int k = 0; k < _legs.Count; k++)
                report.Append($" Leg{k}({angle[k]:F0}도, 무리{group[k]})");

            for (int k = 0; k < _phase.Length; k++)
                _phase[k] *= phaseSpread * Mathf.Deg2Rad;

            Debug.Log(report.Append($" · 무리 {groups}개 · 묶는 각도 {groupDegrees:F0}도").ToString(), this);
        }

        /// <summary>노트를 맞았다. 모든 다리가 한 번 튕긴다.</summary>
        public void PlayHit(bool strong)
        {
            _hit = 1f;
            _hitPeak = hitDegrees * (strong ? strongMultiplier : 1f);
        }

        /// <summary>팔 하나가 맞았다. 그 팔만 움찔한다. (2라운드에서 베는 팔)</summary>
        public void PlayHit(int leg, bool strong)
        {
            if (_legHit == null || leg < 0 || leg >= _legHit.Length) return;

            _legHit[leg] = 1f;
            _legHitPeak[leg] = hitDegrees * (strong ? strongMultiplier : 1f);
        }

        /// <summary>팔 하나를 들거나 내린다. 촉수가 올라오는 것이 이것이다.</summary>
        public void SetRaised(int leg, bool up)
        {
            if (_legRaiseWant == null || leg < 0 || leg >= _legRaiseWant.Length) return;

            _legRaiseWant[leg] = up ? 1f : 0f;
            if (up) { _legDrooping[leg] = false; _legDroop[leg] = 0f; }
        }

        /// <summary>팔 하나가 잘렸다. 힘이 빠지듯 늘어진다.</summary>
        public void PlayCut(int leg)
        {
            if (_legDrooping == null || leg < 0 || leg >= _legDrooping.Length) return;

            _legDrooping[leg] = true;
            _legHit[leg] = Mathf.Max(_legHit[leg], 0.6f);
        }

        /// <summary>
        /// 팔 하나가 <b>이 자리를 향해 뻗게</b> 한다. 2라운드에서 베라고 내미는 동작이다.
        ///
        /// 판정 상자는 사람 앞 <b>칼이 닿는 자리</b>에 고정해 두고(실측 — 베기 5.8m · 찌르기 8m),
        /// 팔이 거기까지 뻗어 온다. 반대로 상자를 팔에 붙이면 모델의 뒤쪽 팔이 10m 밖이라 영영 못 벤다.
        /// </summary>
        public void SetAim(int leg, Vector3 worldPoint, float weight)
        {
            if (_legAim == null || leg < 0 || leg >= _legAim.Length) return;

            _legAim[leg] = worldPoint;
            _legAimWeight[leg] = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// 팔 <b>위의 한 점</b>. <paramref name="along"/> 0 이면 뿌리, 1 이면 끝.
        ///
        /// 표식을 팔 끝에 달면 팔 밖에 대롱대롱 매달린 것처럼 보인다. 0.7 쯤이면 팔 기둥 안에 얹힌다.
        /// </summary>
        public Vector3 PointOnArm(int leg, float along)
        {
            if (leg < 0 || leg >= _legs.Count) return transform.position;

            Leg one = _legs[leg];
            float f = Mathf.Clamp01(along) * (one.Bones.Count - 1);
            int i0 = Mathf.Clamp(Mathf.FloorToInt(f), 0, one.Bones.Count - 1);
            int i1 = Mathf.Clamp(i0 + 1, 0, one.Bones.Count - 1);

            return Vector3.Lerp(one.Bones[i0].position, one.Bones[i1].position, f - i0);
        }

        /// <summary>팔 끝이 지금 있는 자리. 판정 상자를 팔에 붙여 두는 데 쓴다.</summary>
        public Vector3 TipOf(int leg)
        {
            if (leg < 0 || leg >= _legs.Count) return transform.position;

            Leg one = _legs[leg];
            return one.Bones[one.Bones.Count - 1].position;
        }

        /// <summary>포효. 콤보 피니시와 최종 형태 등장에 쓴다.</summary>
        public void PlayRoar(bool big = false)
        {
            _roar = big ? 1f : 0.7f;
        }

        private void LateUpdate()
        {
            if (_legs.Count == 0) return;

            float dt = Time.deltaTime;
            if (_hit > 0f) _hit = Mathf.Max(0f, _hit - dt / Mathf.Max(hitSeconds, 0.01f));
            if (_roar > 0f) _roar = Mathf.Max(0f, _roar - dt / Mathf.Max(roarSeconds, 0.01f));

            Vector3 axis = _rigRoot.TransformDirection(swayAxis);
            if (axis.sqrMagnitude < 0.0001f) return;
            axis.Normalize();

            float time = Time.time;

            // 움찔은 짧게 한 번 튕겼다 돌아온다. 사인 반 주기를 세기로 줄여 가며 그린다.
            float hitAngle = _hitPeak * _hit * Mathf.Sin((1f - _hit) * Mathf.PI * 2f);
            // 포효는 벌렸다 천천히 닫힌다. 방향은 모든 다리가 같다 — 반대로 꺾으면 닿은 자리가 찢어진다.
            float roarAngle = roarDegrees * _roar * Mathf.Sin(Mathf.Clamp01(1f - _roar) * Mathf.PI);

            // 1) 모든 뼈를 쉬는 자세로. 2) 겨냥. 3) 흔들기. 순서를 지켜야 겨냥이 지워지지 않는다.
            for (int k = 0; k < _legs.Count; k++)
            {
                Leg one = _legs[k];
                for (int i = 0; i < one.Bones.Count; i++) one.Bones[i].localRotation = one.Rest[i];
            }

            if (_legAimWeight != null)
            {
                for (int k = 0; k < _legs.Count; k++)
                {
                    if (_legAimWeight[k] <= 0.001f) continue;

                    Leg one = _legs[k];
                    Transform root = one.Bones[0];
                    Vector3 from = one.Bones[one.Bones.Count - 1].position - root.position;
                    Vector3 to = _legAim[k] - root.position;
                    if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f) continue;

                    Quaternion turn = Quaternion.Slerp(Quaternion.identity,
                        Quaternion.FromToRotation(from, to), _legAimWeight[k]);
                    root.rotation = turn * root.rotation;
                }
            }

            for (int k = 0; k < _legs.Count; k++)
            {
                Leg leg = _legs[k];
                float phase = _phase != null && k < _phase.Length ? _phase[k] : 0f;
                float idle = idleDegrees * Mathf.Sin(time * idleSpeed + phase);
                float baseAngle = idle + hitAngle + roarAngle;

                // 팔 하나짜리 반응 — 들기 · 움찔 · 늘어짐
                if (_legHit != null)
                {
                    if (_legHit[k] > 0f) _legHit[k] = Mathf.Max(0f, _legHit[k] - dt / Mathf.Max(hitSeconds, 0.01f));
                    _legRaise[k] = Mathf.MoveTowards(_legRaise[k], _legRaiseWant[k], dt / Mathf.Max(raiseSeconds, 0.01f));
                    if (_legDrooping[k]) _legDroop[k] = Mathf.Min(1f, _legDroop[k] + dt / Mathf.Max(raiseSeconds, 0.01f));

                    baseAngle += _legHitPeak[k] * _legHit[k] * Mathf.Sin((1f - _legHit[k]) * Mathf.PI * 2f);
                    baseAngle += -raiseDegrees * Mathf.SmoothStep(0f, 1f, _legRaise[k]);
                    baseAngle += droopDegrees * Mathf.SmoothStep(0f, 1f, _legDroop[k]);
                }

                // ⚠ 뿌리부터 순서대로 돌린다. 자식은 부모가 돌아간 뒤의 자리에서 자기 몫을 더한다
                //    (블렌더의 FK 와 같다). 순서가 바뀌면 사슬이 접힌다.
                for (int s = 0; s < leg.Bones.Count; s++)
                    leg.Bones[s].Rotate(axis, baseAngle * (rootGain + stepGain * s), Space.World);
            }
        }
    }
}
