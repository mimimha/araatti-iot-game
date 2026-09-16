using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnderTheSea.Account;
using UnderTheSea.Character;

namespace UnderTheSea.Network
{
    /// <summary>
    /// 캐릭터 외형을 서버 권위로 복제한다.
    ///
    /// <b>흐름</b>
    /// <code>
    ///   서버   NetworkPlayer 스폰. AppearanceReady = false → 모든 피어가 모델을 감춘다
    ///   내 것  Rpc_SubmitAppearance(바이트) 로 자기 외형을 한 번 제출한다
    ///   서버   카탈로그로 검증 → [Networked] 에 기록 → AppearanceReady = true
    ///   모두   값이 바뀐 것을 보고 외형을 입힌 뒤 모델을 보여 준다
    /// </code>
    ///
    /// <b>왜 RPC 를 한 번만 보내는가.</b>
    /// <see cref="Fusion.NetworkArray{T}"/> 같은 <c>[Networked]</c> 상태는 **늦게 들어온 사람에게도
    /// 현재 값이 자동으로 간다.** RPC 로만 알리면 그때 접속해 있던 사람만 받는다.
    /// 그래서 전달은 RPC 로 한 번, 보관과 전파는 <c>[Networked]</c> 로 한다.
    ///
    /// <b>신뢰 경계 (PRD 09 은 플레이테스트 단계다)</b>
    /// 서버는 클라이언트가 보낸 값을 **그대로 믿는다.** JWT 검증과 REST 재조회는 PRD 10 이다.
    /// 다만 위조의 범위는 <b>자기 외형</b>으로 갇혀 있다.
    ///   · RPC 가 <c>PlayerRef</c> 를 인자로 받지 않는다 — 대상을 지정할 방법이 없다
    ///   · <c>RpcSources.InputAuthority</c> + <see cref="RpcInfo.Source"/> 대조로 보낸 사람을 확정한다
    ///   · <c>[Networked]</c> 쓰기 권한은 StateAuthority(서버)만 갖는다
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 09-2)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerAppearance : NetworkBehaviour
    {
        [Header("카탈로그")]
        [Tooltip("파츠 키를 검증하고 되찾는 데 쓴다. 서버와 모든 클라이언트가 같은 에셋을 본다.")]
        [SerializeField] private CharacterPartCatalog catalog;

        [Header("모델")]
        [Tooltip("외형을 입히는 컴포넌트. 보통 같은 오브젝트에 있다.")]
        [SerializeField] private CharacterAppearanceApplier applier;

        [Tooltip("준비되기 전까지 감출 렌더러. 비어 있으면 자식에서 모두 찾는다.")]
        [SerializeField] private Renderer[] hiddenUntilReady;

        /// <summary>
        /// 자리별 파츠 키. 자리 순서는 <see cref="CharacterAppearanceCodec.OrderedWearSlots"/> 다.
        ///
        /// ⚠ <c>WearSlot</c> 의 숫자값을 인덱스로 쓰지 않는다. Body 는 64 지만 0번 칸이다.
        /// </summary>
        [Networked, Capacity(CharacterAppearanceCodec.SlotCount)]
        private NetworkArray<NetworkString<_32>> PartKeys { get; }

        /// <summary>"#RRGGBB". 로컬 저장 형식과 같은 모양을 유지한다.</summary>
        [Networked]
        private NetworkString<_8> SkinColor { get; set; }

        /// <summary>
        /// 서버가 외형을 채웠는가.
        ///
        /// <b>빈 외형을 제출해도 true 가 된다.</b> 개발용 직접 진입처럼 값이 없을 때
        /// 이것이 false 로 남으면 Overlay 가 영영 닫히지 않고 원격 캐릭터도 계속 숨겨진다.
        /// </summary>
        [Networked]
        public NetworkBool AppearanceReady { get; private set; }

        private ChangeDetector changes;
        private bool submitted;

        /// <summary>
        /// 마지막으로 입힌 값의 서명.
        ///
        /// 한 번 입히면 끝나는 래치를 쓰지 않는 이유:
        /// 그러면 서버가 나중에 값을 고쳐도 화면이 따라가지 않는다.
        /// 같은 값이면 건너뛰고, 바뀌었을 때만 다시 입힌다.
        /// </summary>
        private string appliedSignature;

        // ------------------------------------------------------------
        // 스폰 — 감추고, 내 것이면 제출한다
        // ------------------------------------------------------------

        public override void Spawned()
        {
            CacheRenderers();
            changes = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);

            // 준비되기 전에는 아무에게도 보이지 않는다.
            // 기본 외형을 잠깐 보여 주면 "옷이 바뀌는 버그" 처럼 보인다.
            SetModelVisible(false);

            if (HasInputAuthority)
            {
                SubmitMine();
            }

            // 늦게 들어온 경우 이미 값이 차 있다. 변화 감지를 기다리지 않고 바로 입힌다.
            if (AppearanceReady)
            {
                ApplyFromState("늦은 접속");
            }
        }

        /// <summary>
        /// **기본 외형으로 확정한다.** 서버에서만 통한다.
        ///
        /// 파츠 없음 + 흰색으로 <c>[Networked]</c> 를 채우고 <see cref="AppearanceReady"/> 를 켠다.
        /// 받는 쪽은 파츠가 0개이므로 <b>프리팹에 원래 들어 있던 모습 그대로</b> 보인다.
        /// (<c>CharacterAppearanceApplier</c> 를 거쳐 입은 것이 없으면 벗길 것도 없다)
        ///
        /// 제출이 이미 반영된 뒤에 부르면 아무것도 하지 않는다. 사람의 외형을 덮지 않는다.
        ///
        /// ⚠ <b>이 컴포넌트는 스스로 이것을 부르지 않는다.</b> "몇 초 뒤에 포기한다" 는 정책을
        ///    여기 두지 않기 때문이다. 부를지 말지는 부르는 쪽이 정한다.
        ///
        ///    <c>Login → Lobby</c> 정상 경로에는 부르는 곳이 <b>하나도 없다.</b>
        ///    거기서는 사람이 만든 외형이 늦더라도 끝까지 기다리는 것이 맞다.
        ///    지금 부르는 곳은 로그인을 거치지 않는 ShipCoop 직접 접속
        ///    (<c>ShipCoopDefaultAppearance</c>) 하나뿐이다.
        /// </summary>
        public void ConfirmDefaultAppearance(string because)
        {
            if (!HasStateAuthority)
            {
                Debug.LogWarning(
                    "[NetworkPlayerAppearance] 기본 외형 확정은 서버만 할 수 있습니다. 무시합니다.", this);
                return;
            }

            if (AppearanceReady)
            {
                return;
            }

            Debug.Log(
                $"[NetworkPlayerAppearance] {Object.InputAuthority} 를 기본 외형으로 확정합니다. ({because})", this);

            WriteState(new string[CharacterAppearanceCodec.SlotCount], "#FFFFFF");
        }

        public override void Render()
        {
            if (changes == null)
            {
                return;
            }

            foreach (string changed in changes.DetectChanges(this))
            {
                if (changed == nameof(AppearanceReady)
                    || changed == nameof(SkinColor)
                    || changed == nameof(PartKeys))
                {
                    ApplyFromState("상태 변경");
                    return;
                }
            }
        }

        // ------------------------------------------------------------
        // 제출 — 내 캐릭터에서만
        // ------------------------------------------------------------

        private void SubmitMine()
        {
            if (submitted)
            {
                return;
            }

            submitted = true;

            CharacterDto current = AccountServiceLocator.IsReady && AccountServiceLocator.Characters != null
                ? AccountServiceLocator.Characters.CurrentCharacter
                : null;

            if (current == null)
            {
                // 개발용 Lobby 직접 진입이다. 값이 없으니 빈 페이로드를 보낸다.
                //
                // ⚠ 정상 Login 경로는 여기 도달하지 않는다. CurrentCharacter 가 없으면
                //    FusionNetworkService 가 아예 접속을 시작하지 않는다.
                //    그래서 여기서 빈 값을 보내는 것이 "조용한 Dummy 대체" 가 되지 않는다.
                Debug.Log("[NetworkPlayerAppearance] 캐릭터 값이 없어 빈 외형을 제출합니다. (개발용 직접 진입)");
                Rpc_SubmitAppearance(CharacterAppearanceCodec.EmptyPayload());
                return;
            }

            if (!TryBuildPayload(current, out byte[] payload, out string error))
            {
                // 실을 수 없는 외형이다. 빈 값으로라도 제출해 화면이 멈추지 않게 한다.
                Debug.LogError(
                    $"[NetworkPlayerAppearance] 외형을 싣지 못해 빈 값으로 제출합니다. {error}", this);
                Rpc_SubmitAppearance(CharacterAppearanceCodec.EmptyPayload());
                return;
            }

            Debug.Log($"[NetworkPlayerAppearance] 내 외형을 제출합니다. {payload.Length}바이트");
            Rpc_SubmitAppearance(payload);
        }

        /// <summary>서버 캐릭터의 외형을 바이트로 만든다. 자리 번호는 카탈로그의 WearSlot 에서 얻는다.</summary>
        private bool TryBuildPayload(CharacterDto character, out byte[] payload, out string error)
        {
            payload = null;
            error = null;

            if (catalog == null)
            {
                error = "파츠 카탈로그가 연결되지 않았습니다.";
                return false;
            }

            CharacterAppearanceSnapshot snapshot = CharacterAppearanceMapping.ToSnapshot(character);
            string[] keys = new string[CharacterAppearanceCodec.SlotCount];

            if (snapshot.parts != null)
            {
                foreach (CharacterPartSnapshot part in snapshot.parts)
                {
                    if (string.IsNullOrEmpty(part.prefabName))
                    {
                        continue;
                    }

                    if (!catalog.TryFind(part.prefabName, out CharacterPartCatalog.Entry entry))
                    {
                        Debug.LogWarning(
                            $"[NetworkPlayerAppearance] 파츠 \"{part.prefabName}\" 가 카탈로그에 없어 보내지 않습니다.", this);
                        continue;
                    }

                    int index = CharacterAppearanceCodec.IndexOf(entry.slot);
                    if (index < 0)
                    {
                        Debug.LogWarning(
                            $"[NetworkPlayerAppearance] \"{part.prefabName}\" 의 자리 {entry.slot} 가 순서표에 없습니다.", this);
                        continue;
                    }

                    keys[index] = entry.Key;
                }
            }

            CharacterAppearanceCodec.TryParseHex(snapshot.bodyColorHex, out Color32 color);
            return CharacterAppearanceCodec.TryEncode(keys, color, out payload, out error);
        }

        // ------------------------------------------------------------
        // 서버 — 받고, 검증하고, 기록한다
        // ------------------------------------------------------------

        /// <summary>
        /// 자기 외형을 제출한다.
        ///
        /// ⚠ <b><c>PlayerRef</c> 를 인자로 받지 않는다.</b> 대상을 고를 수 있게 만들면
        ///    남의 외형을 바꾸는 길이 열린다. 보낸 사람은 <see cref="RpcInfo.Source"/> 뿐이다.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_SubmitAppearance(byte[] payload, RpcInfo info = default)
        {
            // 권한 확인. Fusion 이 이미 InputAuthority 만 보내도록 막지만,
            // 이 오브젝트의 주인이 맞는지는 우리가 한 번 더 본다.
            if (info.Source != Object.InputAuthority)
            {
                Debug.LogWarning(
                    $"[NetworkPlayerAppearance] {info.Source} 가 남의 캐릭터" +
                    $"({Object.InputAuthority})의 외형을 제출하려 했습니다. 무시합니다.", this);
                return;
            }

            ApplySubmission(payload, info.Source);
        }

        /// <summary>
        /// 제출을 검증해 <c>[Networked]</c> 에 기록한다. **서버에서만 불린다.**
        ///
        /// 렌더러 · 프리팹 · UI 를 건드리지 않는다. 카탈로그의 데이터만 본다.
        /// 그래서 <c>-nographics</c> 서버에서 그대로 돈다.
        /// </summary>
        private void ApplySubmission(byte[] payload, PlayerRef source)
        {
            int received = payload != null ? payload.Length : 0;

            if (!CharacterAppearanceCodec.TryDecode(payload, out CharacterAppearanceCodec.Decoded decoded,
                    out string error))
            {
                // 구조가 깨졌다. 전체를 버리되 **준비 완료로는 넘긴다.**
                // 여기서 멈추면 그 사람은 영원히 보이지 않고 Overlay 도 닫히지 않는다.
                Debug.LogWarning(
                    $"[NetworkPlayerAppearance] {source} 의 외형을 풀지 못했습니다. 기본 외형으로 둡니다. " +
                    $"({received}바이트) {error}", this);
                WriteState(new string[CharacterAppearanceCodec.SlotCount], "#FFFFFF");
                return;
            }

            foreach (string warning in decoded.Warnings)
            {
                Debug.LogWarning($"[NetworkPlayerAppearance] {source}: {warning}", this);
            }

            string[] accepted = new string[CharacterAppearanceCodec.SlotCount];
            int kept = 0;
            int dropped = 0;

            for (int i = 0; i < CharacterAppearanceCodec.SlotCount; i++)
            {
                string key = decoded.Keys[i];
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!Validate(i, key, out string reason))
                {
                    // 이 자리만 비운다. 나머지 파츠는 살린다.
                    Debug.LogWarning($"[NetworkPlayerAppearance] {source}: {reason}", this);
                    dropped++;
                    continue;
                }

                accepted[i] = key;
                kept++;
            }

            string hex = CharacterAppearanceCodec.ToHex(decoded.SkinColor);
            WriteState(accepted, hex);

            Debug.Log(
                $"[NetworkPlayerAppearance] {source} 외형 반영 — {received}바이트, " +
                $"채택 {kept}개" + (dropped > 0 ? $", 버림 {dropped}개" : string.Empty) +
                $", 피부색 {hex}");
        }

        /// <summary>파츠 하나가 쓸 만한지. 순수 데이터 검사다.</summary>
        private bool Validate(int slotIndex, string key, out string reason)
        {
            reason = null;

            if (key.Length > CharacterPartCatalog.MaxNetworkKeyLength)
            {
                // NetworkString<_32> 는 넘치면 조용히 자른다. 자른 값을 복제하면
                // 받는 쪽이 카탈로그에서 못 찾는다. 그래서 쓰기 전에 막는다.
                reason =
                    $"자리 {slotIndex} 의 키 \"{key}\" 가 {key.Length}자로 " +
                    $"상한({CharacterPartCatalog.MaxNetworkKeyLength})을 넘어 버렸습니다.";
                return false;
            }

            if (catalog == null)
            {
                reason = $"자리 {slotIndex}: 파츠 카탈로그가 없어 검증할 수 없습니다.";
                return false;
            }

            if (!catalog.TryFind(key, out CharacterPartCatalog.Entry entry))
            {
                reason = $"자리 {slotIndex} 의 키 \"{key}\" 가 카탈로그에 없어 버렸습니다.";
                return false;
            }

            WearSlot expected = CharacterAppearanceCodec.OrderedWearSlots[slotIndex];
            if (entry.slot != expected)
            {
                reason =
                    $"\"{key}\" 는 {entry.slot} 자리인데 {expected}({slotIndex}번) 으로 왔습니다. 버렸습니다.";
                return false;
            }

            return true;
        }

        private void WriteState(string[] keys, string skinColorHex)
        {
            for (int i = 0; i < CharacterAppearanceCodec.SlotCount; i++)
            {
                PartKeys.Set(i, keys[i] ?? string.Empty);
            }

            SkinColor = skinColorHex;
            AppearanceReady = true;
        }

        // ------------------------------------------------------------
        // 모든 피어 — 복제받은 값을 입힌다
        // ------------------------------------------------------------

        private void ApplyFromState(string because)
        {
            if (!AppearanceReady)
            {
                return;
            }

            string signature = BuildSignature();
            if (signature == appliedSignature)
            {
                // 값이 그대로다. 다시 입히면 파츠를 지웠다 다시 만들기만 한다.
                return;
            }

            appliedSignature = signature;

            if (applier == null)
            {
                Debug.LogError(
                    "[NetworkPlayerAppearance] 외형 적용 컴포넌트가 연결되지 않았습니다. " +
                    "모델만 보여 줍니다.", this);
                SetModelVisible(true);
                NotifyLocalViewer();
                return;
            }

            List<CharacterPartSnapshot> parts = new List<CharacterPartSnapshot>();

            for (int i = 0; i < CharacterAppearanceCodec.SlotCount; i++)
            {
                string key = PartKeys.Get(i).ToString();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                // Applier 는 커마 화면의 카테고리 이름으로 순서를 정한다.
                // 그 이름은 카탈로그가 알고 있으므로 여기서 되찾아 넣는다.
                if (catalog != null && catalog.TryFind(key, out CharacterPartCatalog.Entry entry))
                {
                    parts.Add(new CharacterPartSnapshot(entry.category, key));
                }
                else
                {
                    Debug.LogWarning(
                        $"[NetworkPlayerAppearance] 복제받은 키 \"{key}\" 를 카탈로그에서 찾지 못했습니다.", this);
                }
            }

            CharacterAppearanceSnapshot snapshot = new CharacterAppearanceSnapshot
            {
                nickname = string.Empty,
                bodyColorHex = parts.Count > 0 ? SkinColor.ToString() : string.Empty,
                parts = parts.ToArray()
            };

            if (parts.Count == 0)
            {
                // ⚠ 빈 상태로 돌아가는 경우다. (깨진 제출을 서버가 비웠거나, 사람이 모두 벗은 경우)
                //
                //    Applier.ApplySnapshot 은 빈 스냅샷을 받으면 **아무것도 하지 않는다.**
                //    개발용 직접 진입에서 프리팭 기본 외형을 지키려고 둔 규칙이다.
                //    그러니 여기서 그냥 넣으면 **이미 입어 놓은 파츠가 그대로 남는다.**
                //    상태는 비었는데 화면은 예전 옷을 입고 있는 상황이 된다.
                //    그래서 입고 있던 것을 명시적으로 벗긴다.
                RemoveAllEquipped();
            }
            else
            {
                applier.ApplySnapshot(snapshot);
            }

            SetModelVisible(true);

            Debug.Log(
                $"[NetworkPlayerAppearance] 외형을 입혔습니다. ({because}) " +
                $"파츠 {parts.Count}개, 내 것={HasInputAuthority}");

            NotifyLocalViewer();
        }

        /// <summary>
        /// 입고 있는 파츠를 모두 벗긴다. 모델은 프리팭 기본 상태로 돌아간다.
        ///
        /// Applier 의 공개 API 만 쓴다. 카탈로그로 키 → 프리팭 을 되찾아 하나씩 벗긴다.
        /// </summary>
        private void RemoveAllEquipped()
        {
            if (applier == null || catalog == null)
            {
                return;
            }

            List<string> worn = new List<string>(applier.EquippedKeys);
            int removed = 0;

            foreach (string key in worn)
            {
                if (catalog.TryFind(key, out CharacterPartCatalog.Entry entry)
                    && applier.TryUnequipPart(entry.prefab))
                {
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log($"[NetworkPlayerAppearance] 입고 있던 파츠 {removed}개를 벗겨 기본 외형으로 되돌렸습니다.", this);
            }
        }

        /// <summary>지금 복제된 값을 한 줄로 요약한다. 같은 값인지 보기만 하면 되므로 모양은 중요하지 않다.</summary>
        private string BuildSignature()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            for (int i = 0; i < CharacterAppearanceCodec.SlotCount; i++)
            {
                builder.Append(PartKeys.Get(i).ToString()).Append('|');
            }

            return builder.Append(SkinColor.ToString()).ToString();
        }

        /// <summary>내 캐릭터라면 화면을 넘겨도 된다고 알린다.</summary>
        private void NotifyLocalViewer()
        {
            if (!HasInputAuthority)
            {
                return;
            }

            LocalPlayerView viewer = GetComponent<LocalPlayerView>();
            if (viewer != null)
            {
                viewer.NotifyAppearanceReady();
            }
        }

        // ------------------------------------------------------------
        // 보이기 / 감추기
        // ------------------------------------------------------------

        private void CacheRenderers()
        {
            if (hiddenUntilReady != null && hiddenUntilReady.Length > 0)
            {
                return;
            }

            hiddenUntilReady = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// 모델을 보이거나 감춘다.
        ///
        /// <c>enabled</c> 가 아니라 <see cref="Renderer.forceRenderingOff"/> 를 쓴다.
        /// <see cref="CharacterAppearanceApplier"/> 가 슬롯별로 <c>enabled</c> 를 켜고 끄기 때문에,
        /// 여기서 같은 값을 만지면 서로 덮어쓴다.
        /// </summary>
        private void SetModelVisible(bool visible)
        {
            if (hiddenUntilReady == null)
            {
                return;
            }

            foreach (Renderer renderer in hiddenUntilReady)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = !visible;
                }
            }
        }
    }
}
