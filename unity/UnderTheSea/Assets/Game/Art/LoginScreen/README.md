# 로그인 화면 에셋

기준 해상도는 `1300 x 733`입니다. 배치 좌표는 `login-screen-layout.json`을 참고하세요.

## 최종 에셋

- `background-clean.png`: 로그인 UI와 캐릭터가 제거된 항구 배경
- `crew.png`: 네 명의 캐릭터와 지도 테이블 장식 스프라이트
- `login-panel-frame.png`: 로그인 패널의 금색 프레임과 남색 판넬
- `tab-login-active.png`: 로그인 탭 원본 상태
- `tab-register-inactive.png`: 회원가입 탭 원본 상태
- `input-field-base.png`: 텍스트와 아이콘을 얹는 재사용 입력창 바탕
- `button-login-base.png`: 텍스트를 얹는 로그인 버튼 바탕
- `button-login-reference.png`: 원본의 로그인 글자가 포함된 비교용 분리본
- `button-back-reference.png`: 뒤로가기 버튼 원본 분리본
- `icon-email.png`: 이메일 아이콘
- `icon-lock.png`: 잠금 아이콘

`input-email-reference.png`와 `input-password-reference.png`는 원본 비교용이며 아이콘과 문구가 포함되어 있습니다. 실제 구현에서는 `input-field-base.png` 위에 `icon-email.png`/`icon-lock.png`와 TextMeshPro 텍스트를 별도로 배치하세요.

## 권장 Unity Hierarchy

```text
LoginScreen
├─ Background
├─ Crew
└─ LoginPanel
   ├─ Frame
   ├─ Tabs
   ├─ EmailField
   │  ├─ Background
   │  ├─ EmailIcon
   │  └─ Placeholder (TMP_InputField)
   ├─ PasswordField
   │  ├─ Background
   │  ├─ LockIcon
   │  └─ Placeholder (TMP_InputField)
   ├─ LoginButton
   │  ├─ Background
   │  └─ Label (TextMeshProUGUI)
   └─ RegisterHint
```

메뉴 텍스트에는 프로젝트의 `Assets/Game/Fonts/NotoSansKR-Bold SDF.asset`를 지정하세요. 패널과 입력창은 `Image`의 Preserve Aspect를 켜고, 입력창 바탕은 가로로 늘릴 경우 9-slice를 사용하면 좋습니다.
