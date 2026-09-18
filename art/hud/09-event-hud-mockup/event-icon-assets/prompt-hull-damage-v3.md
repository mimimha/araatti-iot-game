# 선체 파손·침수 아이콘 v3

제작: built-in image_gen. v2의 파손 구멍과 물 유입을 유지하면서 돛대·삼각돛·뱃머리·용골을 추가해 배 실루엣을 강화.

Use case: precise-object-edit / background-extraction.
Asset type: production game HUD event icon sprite.
Image 1 establishes the approved five-icon visual system. Image 2 is the current hull-damage-and-flooding icon to improve.

Redesign ONLY this icon so it unmistakably reads as a small sailing ship suffering a hull breach, while keeping the breach and flooding dominant at 48–64 px.

Required silhouette:
- a compact side-view warm-ivory boat hull with a clearly raised pointed bow on the right and a gently curved keel;
- one short mast rising from the hull near the left-center;
- one small simple triangular warm-ivory sail attached to the mast, occupying no more than the upper third of the icon;
- exactly one large irregular dark-navy breach hole in the lower middle of the hull;
- exactly three short bold coral-red cracks radiating from the breach;
- one thick bright-cyan water stream physically connected to the breach and flowing inward/downward;
- optionally exactly two small dark-navy circular portholes, only if they remain legible and do not compete with the breach.

The mast, sail, raised bow and curved keel must create an obvious boat silhouette before any interior detail is noticed. Keep the hull broad enough that the breach remains large and clear. The sail should identify the object as a ship, not turn it into the same silhouette as the enemy-ship event icon; use only one low triangular sail and no flag.

Match the approved icon system exactly: thick dark-navy edge, chunky rounded geometry, restrained polished 2.5D bevel, warm-ivory/coral/cyan palette, same lighting and visual weight. Genuinely transparent RGBA square canvas with even padding.

Output exactly one centered icon. No circular medallion, container, badge, text, label, loose planks, separate droplets, tools, full ocean scene, skull, flag, cannons, ropes, multiple sails, detached shadow, watermark or canvas border.
