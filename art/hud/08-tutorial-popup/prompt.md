# 출항 안내 팝업 목업

Built-in image generation. Visual mockup only; Unity code/prefab unchanged. Copy is condensed from ShipCoopTutorialView.cs, preserving shared HP, time limit, five tasks, cargo restrictions, flexible roles and automatic/Enter dismissal.

Use case: ui-mockup / precise-object-edit.
Create a beautiful Korean cooperative pirate-game tutorial popup composited over the supplied gameplay screenshot. Image 1 is the edit target: replace its existing oversized dark tutorial rectangle and ALL overflowing tutorial text. Image 2 is the approved interaction icon design reference, authoritative for wooden wheel, cream canvas sail, dark iron cannon, repair hammer, water bucket. Image 3 is the login visual language reference.

Preserve screenshot 1's actual ship deck camera, sea, low-poly character and background HUD placement. Apply a uniform dark translucent modal scrim so the scene is visible but subdued. The popup is the hero, crisply rendered. Wide landscape image matching source aspect ratio around 16:9, high quality.

Popup: centered generous approximately 1150x740 panel on a 1632x920 canvas. Fits wholly inside screen with at least 75px vertical margins. Rounded deep-navy painted wood body, restrained aged brass rim, tiny corner rivets, a compact warm wood title tab that sits integrated into its top edge. Clean high-quality casual pirate visual style, softly bevelled physical materials, readable modern rounded Korean sans-serif. Cream typography, warm gold emphasis, muted turquoise accent, coral only for failure conditions. Keep ornament restrained, hierarchy and negative space excellent. Avoid chunky gold ornaments and cartoon skulls.

EXACT layout and copy, all text contained inside the panel, no text overlaps:
1. Header title on small wood tab: "함께 항해하기"
2. Large centered mission headline near top: "네 명이 힘을 합쳐, 3분 안에 목적지까지!"
3. Immediately below a slim subdued callout with a small cream HEART icon:
"체력은 모두가 공유해요. 침몰하거나 시간이 끝나면 실패!"
4. Middle row: FIVE evenly spaced compact cards, each with a lovely tangible medium-detail icon from reference 2, short bold heading, and one smaller action line. All five cards equal height and calm navy backplates with subtle blue edges, no bulky circles.
Card1: canvas sail icon, heading "돛 조절", description "돛을 펴서 전진"
Card2: wooden ship wheel icon, heading "조타", description "암초를 피해 방향 조절"
Card3: dark iron cannon icon, heading "대포", description "다가오는 적선 막기"
Card4: hammer and repair plank icon, heading "수리", description "판자로 구멍 막기"
Card5: wooden water bucket icon, heading "배수", description "차오른 물 퍼내기"
5. Below cards a compact left-aligned two-line logistics note, tiny crate icon:
"포탄·수리 자재·양동이는 상자에서 가져오세요."
"물건을 운반하는 동안에는 다른 일을 할 수 없어요."
6. Centered warm cream/gold cooperative rule below that:
"정해진 역할은 없어요. 비어 있는 자리를 서로 채워주세요!"
7. Footer separated with thin brass hairline, small text at left "잠시 후 자동으로 닫힙니다" and at right an ivory keyboard keycap "Enter" followed by label "닫기". This is a keyboard dismiss affordance, NOT a start game or ready check button; the game runs behind it. No fake timer number. Footer text must stay within panel with comfortable bottom padding.

Do not preserve any of the old overlapping tutorial text outside the new panel. No extra writing, no pagination, no new controls, no role assignment claims, no watermark. Every required line of Korean must be correct and legible. This is a finished game UI mockup, not an annotated design presentation.
