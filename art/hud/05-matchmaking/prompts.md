# Image generation prompts

Mode: built-in image_gen. Reference: user attachment codex-clipboard-59ad00f1-bcb3-4711-80e3-47ccfafced91.png. Reference is visual guidance only.

## Harbour background
Use case: ui-mockup supporting background asset. Generate ONLY a beautiful 16:9 landscape empty tropical pirate harbour environment background for a four-player co-op matchmaking lobby, closely matching the attached reference's chunky low-poly stylized game art, vibrant turquoise water, warm wooden docks, saturated cobalt blue sky, palm islands, distant lighthouse, friendly seafaring adventure mood. High-quality softly shaded game art with tactile wood. Composition: deck and dock corners frame the bottom edges, modest palm foliage at side edges, small lighthouse in upper-left distance, turquoise harbour center, sea and distant tropical cliffs. The center 75% will be covered by a large UI panel, so keep central backdrop low-detail. It must be an ENVIRONMENT-ONLY plate, no interface of any kind: NO panel, NO cards, NO text, NO letters, NO numbers, NO logo, NO watermark, NO buttons, NO character, NO character silhouettes, NO question marks. No people anywhere. Match reference artistic style while making an original empty harbour background. 16:9 aspect ratio.

## Empty decorative panel
Create ONE standalone blank decorative UI MAIN PANEL sprite for a tropical pirate co-op matchmaking lobby. Use attached reference only for panel material/style. FRONT FACING FLAT ORTHOGRAPHIC interface. Wide landscape panel, aspect approximately 1.9:1, fills canvas with small even transparent margin. A large rounded rectangle with a thin warm cream/brass beveled border, deep dark navy blue interior with subtle hand-painted nautical parchment grain. Dark navy interior is filled, NOT transparent. Outside the rounded panel must be genuinely transparent alpha. Small tactile golden rope knots, an orange starfish at top-right and bottom-left, a small seashell at bottom-right, inspired by reference corners but modest and confined close to border. High-quality stylized pirate game UI, restrained wood/brass accents, subtle material texture. Entire interior is COMPLETELY EMPTY uninterrupted dark navy, uniform calm texture with plenty of room for FOUR runtime player slots. NO text, NO letters, NO title, NO logo, NO numbers, NO spinner, NO icons inside, NO player slots, NO cards, NO buttons, NO characters, NO silhouettes, NO question marks, NO harbour backdrop. Only a single blank decorative panel on real transparent background. Interface sharp with straight aligned horizontal/vertical edges, zero perspective.

## Waiting silhouette (built-in image_gen)
Create a single isolated flat silhouette sprite matching the dark anonymous pirate in the reference: front-facing chunky pirate, broad tricorn hat, round head, short torso, arms at hips; dark navy #102A3B, transparent background. No question mark, text, card, frame or rectangle.

## v3 waist-up silhouette — built-in image_gen
Recreate the reference silhouette contours and proportions: very large broad tricorn hat, round head and ears, shoulders, outward elbows, hands on hips. Waist-up only, flat cut at nameplate, no legs or feet. Uniform dark navy #132D3D. Remove question mark, text, card, background, border. Transparent outside.

## v4 taller panel — built-in image_gen

Recreate the existing blank panel at approximately 1.58:1 width:height, using the user reference for vertical scale. Preserve navy texture, cream border, rope/starfish/shell corners, transparent exterior, and empty interior. No text, slots, characters, spinner or buttons.

## v6 final panel construction

The v4 imagegen output was discarded because its generated transparency guide required destructive edge cleanup. The final v6 panel uses the clean v3 imagegen panel as its source; its top and bottom decorative regions are preserved pixel-for-pixel and only the undecorated center band is extended vertically.
