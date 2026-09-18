# 이벤트 HUD 전체 목업 v4

제작: built-in image_gen. 테두리 없는 컨테이너에 최종 픽토그램을 직접 배치해 전체 HUD 조화를 확인.

Use case: compositing / precise-object-edit.
Image 1: EDIT TARGET, approved full-screen borderless event HUD mockup.
Images 2–4: exact finalized transparent sprites for BIG WAVE, SQUALL and HULL DAMAGE + FLOODING. Use their icon designs faithfully; do not redraw them as detailed scenes.

Keep the entire gameplay screenshot and all non-event HUD unchanged: ocean, ship, character, top-left departure badge, top-center HP/timer/voyage/warning, bottom portraits and interaction bar.

Revise ONLY the three stacked upper-left event cards:
- Keep their borderless semi-transparent midnight-navy rounded containers, same size, position, spacing, soft shadow and overall information hierarchy.
- REMOVE the circular icon medallions and gold icon rings completely.
- Place each supplied icon directly on the left with generous padding, about 62 px visual size, consistent scale and vertical centering.
- Shift text slightly right so it never overlaps the standalone icon.
- Retain compact rounded state badges and thin countdown bars.

Card 1 uses Image 2 big-wave icon. Exact Korean title "거대한 파도". Amber badge "예고". Exact subtitle "조타를 붙잡고 정면 돌파". Right countdown "5초". Amber countdown line about 70 percent.
Card 2 uses Image 3 squall icon. Exact Korean title "돌풍". Coral badge "발생". Exact subtitle "돛을 30% 이상 유지". Right countdown "12초". Coral countdown line about 45 percent.
Card 3 uses Image 4 final sailing-ship hull-damage icon. Exact Korean title "선체 파손·침수". Coral badge "발생". Exact subtitle "구멍을 막고 물을 퍼내세요". Small location "하단갑판". Because this event is untimed, absolutely NO countdown number and NO countdown bar on the third card.

Typography: friendly bold Korean sans-serif, warm ivory title, muted light-blue subtitle, high readability at gameplay size. The new pictograms must visually relate to the existing cream heart, sailboat and hand icons across the whole HUD.

Do not add new UI, labels, annotations, extra cards, decorative borders, gold container frames, circular medallions, watermark or comparison layout. Preserve 16:9 framing.
