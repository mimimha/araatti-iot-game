using UnityEngine;
using UnderTheSea.Inventory;
using UnderTheSea.Lobby;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 지금 쓰는 계정 · 캐릭터 서비스를 담아두는 곳.
    ///
    /// 화면 쪽 코드는 여기서 꺼내 쓴다.
    ///
    ///     if (AccountServiceLocator.IsReady)
    ///     {
    ///         AccountServiceLocator.Auth.LogIn(email, password);
    ///     }
    ///
    /// 기존 NetworkServiceLocator 와 같은 방식이다. 다만 **다른 홀더**다.
    /// 실시간 접속과 계정 기능은 서로 독립적으로 Fake ↔ 진짜를 갈아끼울 수 있어야 한다.
    ///
    /// 가짜를 진짜로 바꾸려면 AccountServiceBootstrap 한 파일만 고치면 된다.
    /// </summary>
    public static class AccountServiceLocator
    {
        /// <summary>현재 등록된 인증 서비스. 없으면 null.</summary>
        public static IAuthService Auth { get; private set; }

        /// <summary>현재 등록된 캐릭터 서비스. 없으면 null.</summary>
        public static ICharacterService Characters { get; private set; }

        /// <summary>인벤토리 조회. 등록 전이면 null.</summary>
        public static IInventoryService Inventory { get; private set; }

        /// <summary>제단 조회 · 봉헌. 등록 전이면 null.</summary>
        public static IAltarService Altar { get; private set; }

        /// <summary>둘 다 준비되었는지.</summary>
        public static bool IsReady => Auth != null && Characters != null;

        public static void Register(IAuthService service)
        {
            if (service == null)
            {
                Debug.LogWarning("[AccountServiceLocator] null 은 등록할 수 없습니다.");
                return;
            }

            WarnIfReplacing(Auth, service);
            Auth = service;
        }

        public static void Register(ICharacterService service)
        {
            if (service == null)
            {
                Debug.LogWarning("[AccountServiceLocator] null 은 등록할 수 없습니다.");
                return;
            }

            WarnIfReplacing(Characters, service);
            Characters = service;
        }

        public static void Register(IInventoryService service)
        {
            if (service == null)
            {
                Debug.LogWarning("[AccountServiceLocator] null 은 등록할 수 없습니다.");
                return;
            }

            WarnIfReplacing(Inventory, service);
            Inventory = service;
        }

        public static void Register(IAltarService service)
        {
            if (service == null)
            {
                Debug.LogWarning("[AccountServiceLocator] null 은 등록할 수 없습니다.");
                return;
            }

            WarnIfReplacing(Altar, service);
            Altar = service;
        }

        /// <summary>
        /// 로그아웃. 인증 세션과 그 세션에 딸린 캐시를 **함께** 비운다.
        ///
        /// 화면 코드는 어느 서비스가 무엇을 지워야 하는지 알 필요가 없다.
        /// 세션에 딸린 상태가 늘어나도 화면은 이 메서드만 계속 부른다.
        /// 둘을 따로 부르면 한쪽만 지워진 채로 남는 실수가 생긴다.
        ///
        /// ⚠ 저장된 계정 · 캐릭터 · 외형 데이터는 지우지 않는다. 메모리에 있는 것만 비운다.
        ///    그래서 같은 계정으로 다시 로그인하면 캐릭터가 그대로 있다.
        /// </summary>
        public static void LogOut()
        {
            string email = Auth?.CurrentUser?.email;

            Auth?.LogOut();
            Characters?.ClearSessionCache();

            // 인벤토리·제단 캐시도 함께 비운다. 남겨 두면 다음 사람이 로그인하기 전까지
            // 이전 계정의 조각 수와 기여도가 화면에 남는다.
            PlayerInventory.Clear();
            AltarState.Clear();

            Debug.Log(
                $"[AccountServiceLocator] 로그아웃: {(string.IsNullOrEmpty(email) ? "(로그인 상태 아님)" : email)} " +
                "— 저장된 계정 · 캐릭터 · 외형은 그대로 남습니다.");
        }

        /// <summary>등록을 해제한다. 구현체가 OnDestroy 에서 호출한다.</summary>
        public static void Unregister(IAuthService service)
        {
            if (ReferenceEquals(Auth, service))
            {
                Auth = null;
            }
        }

        /// <summary>등록을 해제한다. 구현체가 OnDestroy 에서 호출한다.</summary>
        public static void Unregister(ICharacterService service)
        {
            if (ReferenceEquals(Characters, service))
            {
                Characters = null;
            }
        }

        /// <summary>등록을 해제한다. 구현체가 OnDestroy 에서 호출한다.</summary>
        public static void Unregister(IInventoryService service)
        {
            if (ReferenceEquals(Inventory, service))
            {
                Inventory = null;
            }
        }

        /// <summary>등록을 해제한다. 구현체가 OnDestroy 에서 호출한다.</summary>
        public static void Unregister(IAltarService service)
        {
            if (ReferenceEquals(Altar, service))
            {
                Altar = null;
            }
        }

        private static void WarnIfReplacing(object current, object incoming)
        {
            if (current == null || ReferenceEquals(current, incoming))
            {
                return;
            }

            Debug.LogWarning(
                $"[AccountServiceLocator] 이미 {current.GetType().Name} 이 등록되어 있는데 " +
                $"{incoming.GetType().Name} 이 덮어씁니다. 서비스가 두 개 만들어졌는지 확인하세요.");
        }
    }
}
