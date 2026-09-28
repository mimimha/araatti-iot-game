using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnderTheSea.Character;

namespace UnderTheSea.Network
{
    /// <summary>
    /// 외형을 바이트로 싣고 푸는 곳. **순수 데이터 로직이다.**
    ///
    /// 렌더러 · 프리팹 · UI · 카탈로그를 전혀 모른다. 그래서 서버(-nographics)에서도
    /// 그대로 돌고, 값만 넣어 시험할 수 있다. 카탈로그 대조는 부르는 쪽이 한다.
    ///
    /// <b>왜 바이트인가.</b>
    /// Fusion RPC 페이로드 상한이 <b>512바이트</b>다. (실측: 512·520B 는 거부,
    /// 500B 는 통과) <c>NetworkString&lt;_32&gt;</c> 는 UTF-32 라 한 칸이 128바이트여서
    /// 13칸은커녕 <b>4칸이면 이미 초과</b>한다. (실측: 3칸까지만 도착)
    /// UTF-8 로 직접 실으면 최악 378바이트라 한 번에 보낼 수 있다.
    ///
    /// <b>형식</b> (버전이 있다. 나중에 모양을 바꿀 때 옛 클라이언트를 구분하려고)
    /// <code>
    ///   [0]      버전 1바이트 (지금은 1)
    ///   [1..]    파츠 반복 — 슬롯 1바이트, 키 길이 1바이트, UTF-8 키 (길이만큼)
    ///   [끝-3..] 피부색 R, G, B 각 1바이트
    /// </code>
    /// 파츠 개수를 적지 않는다. <b>뒤에서 3바이트를 뺀 지점까지</b>가 파츠 구간이다.
    ///
    /// 빈 페이로드(0바이트)는 <b>정상</b>이다. "외형 값이 없다" 는 뜻이고,
    /// 받는 쪽은 모델을 프리팹 기본 상태로 둔다. (개발용 Lobby 직접 진입)
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 09-2)
    /// </summary>
    public static class CharacterAppearanceCodec
    {
        /// <summary>지금 형식 번호. 모양이 바뀌면 올린다.</summary>
        public const byte Version = 1;

        /// <summary>슬롯 칸 수. <see cref="OrderedWearSlots"/> 와 같아야 한다.</summary>
        public const int SlotCount = 13;

        /// <summary>
        /// 우리가 스스로 두는 상한. Fusion RPC 한도(512B)보다 넉넉히 낮다.
        ///
        /// 최악을 계산해 보면 1 + 11×(1+1+32) + 3 = <b>378바이트</b> 라 384 안에 들어온다.
        /// (동시 착용 가능한 슬롯이 11개, 카탈로그 키 상한이 32자)
        /// 넘으면 **보내기 전에** 막는다. RPC 가 거부하도록 두지 않는다.
        /// </summary>
        public const int MaxPayloadBytes = 384;

        /// <summary>키 하나의 UTF-8 바이트 상한. 길이 칸이 1바이트라 255 를 넘을 수 없기도 하다.</summary>
        public const int MaxKeyBytes = 32;

        /// <summary>버전 1 + RGB 3. 파츠가 하나도 없어도 이만큼은 있어야 한다.</summary>
        private const int HeaderAndColorBytes = 4;

        /// <summary>
        /// 슬롯 순서표. **배열 인덱스는 이 표의 자리다.**
        ///
        /// ⚠ <see cref="WearSlot"/> 의 숫자값(1 · 2 · 4 · … · 4096)을 인덱스로 쓰지 마라.
        ///    그 값은 비트 플래그이고 프리팹에 <c>slot: 64</c> 처럼 직렬화돼 있다.
        ///    13칸 배열에 넣으려고 캐스팅하면 Body(64) 가 64번 칸을 찾는다.
        /// </summary>
        public static readonly WearSlot[] OrderedWearSlots =
        {
            WearSlot.Body,          // 0
            WearSlot.Face,          // 1
            WearSlot.Hair,          // 2
            WearSlot.Top,           // 3
            WearSlot.Bottom,        // 4
            WearSlot.Shoes,         // 5
            WearSlot.Glasses,       // 6
            WearSlot.Ears,          // 7
            WearSlot.Gloves,        // 8
            WearSlot.Socks,         // 9
            WearSlot.Hat,           // 10
            WearSlot.FaceAccessory, // 11
            WearSlot.Outfit         // 12
        };

        /// <summary>잘못된 바이트를 조용히 넘기지 않고 예외를 던지는 UTF-8 해석기.</summary>
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary><see cref="WearSlot"/> → 배열 자리. 표에 없으면 -1.</summary>
        public static int IndexOf(WearSlot slot)
        {
            for (int i = 0; i < OrderedWearSlots.Length; i++)
            {
                if (OrderedWearSlots[i] == slot)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>푼 결과.</summary>
        public readonly struct Decoded
        {
            /// <summary>자리별 키. 빈 자리는 <c>null</c>.</summary>
            public readonly string[] Keys;

            /// <summary>피부색.</summary>
            public readonly Color32 SkinColor;

            /// <summary>파츠가 하나도 없는 빈 외형인가.</summary>
            public readonly bool IsEmpty;

            /// <summary>버린 항목의 사유. 전체를 실패로 만들지는 않는다.</summary>
            public readonly IReadOnlyList<string> Warnings;

            public Decoded(string[] keys, Color32 skinColor, bool isEmpty, IReadOnlyList<string> warnings)
            {
                Keys = keys;
                SkinColor = skinColor;
                IsEmpty = isEmpty;
                Warnings = warnings;
            }
        }

        // ------------------------------------------------------------
        // 싣기
        // ------------------------------------------------------------

        /// <summary>
        /// 자리별 키와 피부색을 바이트로 싣는다.
        ///
        /// <paramref name="keysBySlotIndex"/> 는 길이 13 이어야 한다. 빈 자리는 <c>null</c> 또는 빈 문자열.
        /// 상한을 넘으면 **싣지 않고 실패**한다. 잘린 것을 보내면 받는 쪽이 알 방법이 없다.
        /// </summary>
        public static bool TryEncode(
            IReadOnlyList<string> keysBySlotIndex,
            Color32 skinColor,
            out byte[] payload,
            out string error)
        {
            payload = null;
            error = null;

            if (keysBySlotIndex == null || keysBySlotIndex.Count != SlotCount)
            {
                error = $"자리 수가 {SlotCount}개가 아닙니다. (받은 값 {keysBySlotIndex?.Count ?? -1})";
                return false;
            }

            List<byte> bytes = new List<byte>(MaxPayloadBytes) { Version };

            for (int i = 0; i < SlotCount; i++)
            {
                string key = keysBySlotIndex[i];
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                byte[] keyBytes;
                try
                {
                    keyBytes = StrictUtf8.GetBytes(key);
                }
                catch (Exception exception)
                {
                    error = $"자리 {i} 의 키 \"{key}\" 를 UTF-8 로 바꾸지 못했습니다. {exception.Message}";
                    return false;
                }

                if (keyBytes.Length > MaxKeyBytes)
                {
                    error =
                        $"자리 {i} 의 키 \"{key}\" 가 UTF-8 {keyBytes.Length}바이트로 " +
                        $"상한({MaxKeyBytes})을 넘습니다.";
                    return false;
                }

                bytes.Add((byte)i);
                bytes.Add((byte)keyBytes.Length);
                bytes.AddRange(keyBytes);
            }

            bytes.Add(skinColor.r);
            bytes.Add(skinColor.g);
            bytes.Add(skinColor.b);

            if (bytes.Count > MaxPayloadBytes)
            {
                error = $"페이로드가 {bytes.Count}바이트로 상한({MaxPayloadBytes})을 넘습니다.";
                return false;
            }

            payload = bytes.ToArray();
            return true;
        }

        /// <summary>외형 값이 없을 때 보내는 빈 페이로드. 서버가 준비 완료로 넘기게 하는 신호다.</summary>
        public static byte[] EmptyPayload()
        {
            return Array.Empty<byte>();
        }

        // ------------------------------------------------------------
        // 풀기
        // ------------------------------------------------------------

        /// <summary>
        /// 바이트를 푼다.
        ///
        /// <b>전체를 버리는 경우</b> — 구조 자체가 깨진 것이다.
        ///   · 상한을 넘는 길이
        ///   · 모르는 버전
        ///   · 길이가 모자라거나 남는다 (중간에서 잘렸다는 뜻)
        ///
        /// <b>그 자리만 비우는 경우</b> — 나머지는 살린다.
        ///   · 자리 번호가 0~12 밖
        ///   · 키 길이가 0 이거나 상한 초과
        ///   · UTF-8 로 읽히지 않는 바이트
        /// </summary>
        public static bool TryDecode(byte[] payload, out Decoded decoded, out string error)
        {
            decoded = default;
            error = null;

            string[] keys = new string[SlotCount];
            List<string> warnings = new List<string>();

            // 빈 페이로드는 정상이다. "외형 값이 없다" 는 뜻.
            if (payload == null || payload.Length == 0)
            {
                decoded = new Decoded(keys, new Color32(255, 255, 255, 255), true, warnings);
                return true;
            }

            if (payload.Length > MaxPayloadBytes)
            {
                error = $"페이로드가 {payload.Length}바이트로 상한({MaxPayloadBytes})을 넘습니다.";
                return false;
            }

            if (payload.Length < HeaderAndColorBytes)
            {
                error = $"페이로드가 {payload.Length}바이트뿐이라 버전과 색을 담을 수 없습니다.";
                return false;
            }

            if (payload[0] != Version)
            {
                error = $"모르는 형식 번호 {payload[0]} 입니다. (이 빌드는 {Version})";
                return false;
            }

            int colorStart = payload.Length - 3;
            int position = 1;
            int found = 0;

            while (position < colorStart)
            {
                // 자리 번호 1바이트 + 길이 1바이트는 있어야 한다.
                if (position + 2 > colorStart)
                {
                    error = "파츠 항목이 중간에서 끊겼습니다. (자리 번호나 길이 칸이 모자랍니다)";
                    return false;
                }

                int slotIndex = payload[position++];
                int keyLength = payload[position++];

                if (position + keyLength > colorStart)
                {
                    error = $"자리 {slotIndex} 의 키가 {keyLength}바이트라고 했는데 그만큼 남아 있지 않습니다.";
                    return false;
                }

                // 여기서부터는 이 항목만 버린다. 바이트는 이미 건너뛴 뒤다.
                int keyStart = position;
                position += keyLength;

                if (slotIndex < 0 || slotIndex >= SlotCount)
                {
                    warnings.Add($"자리 번호 {slotIndex} 는 0~{SlotCount - 1} 밖이라 버렸습니다.");
                    continue;
                }

                if (keyLength == 0 || keyLength > MaxKeyBytes)
                {
                    warnings.Add($"자리 {slotIndex} 의 키 길이 {keyLength} 가 1~{MaxKeyBytes} 밖이라 버렸습니다.");
                    continue;
                }

                string key;
                try
                {
                    key = StrictUtf8.GetString(payload, keyStart, keyLength);
                }
                catch (Exception exception)
                {
                    warnings.Add($"자리 {slotIndex} 의 키를 UTF-8 로 읽지 못해 버렸습니다. {exception.Message}");
                    continue;
                }

                if (keys[slotIndex] != null)
                {
                    warnings.Add($"자리 {slotIndex} 가 두 번 나와 뒤엣것(\"{key}\")으로 덮었습니다.");
                }

                keys[slotIndex] = key;
                found++;
            }

            if (position != colorStart)
            {
                error = "파츠 구간과 색 구간의 경계가 맞지 않습니다.";
                return false;
            }

            Color32 color = new Color32(payload[colorStart], payload[colorStart + 1], payload[colorStart + 2], 255);
            decoded = new Decoded(keys, color, found == 0, warnings);
            return true;
        }

        // ------------------------------------------------------------
        // 색 표현 — 저장 형식("#RRGGBB")과 바이트 사이
        // ------------------------------------------------------------

        /// <summary>"#RRGGBB" → 색. 읽지 못하면 흰색을 돌려주고 false.</summary>
        public static bool TryParseHex(string hex, out Color32 color)
        {
            color = new Color32(255, 255, 255, 255);

            if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out Color parsed))
            {
                return false;
            }

            color = parsed;
            return true;
        }

        /// <summary>색 → "#RRGGBB". 로컬 저장 형식과 같은 모양을 유지한다.</summary>
        public static string ToHex(Color32 color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(new Color32(color.r, color.g, color.b, 255));
        }
    }
}
