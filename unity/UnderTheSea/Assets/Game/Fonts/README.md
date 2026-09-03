# Korean TextMeshPro font

`NotoSansKR-Bold.otf` is used as the Korean fallback font for TextMeshPro.

- Source: https://github.com/notofonts/noto-cjk
- License: SIL Open Font License 1.1 (`OFL.txt`)
- TMP asset: `NotoSansKR-Bold SDF.asset`
- Atlas mode: Dynamic, 2048 x 2048, multi-atlas enabled

The editor installer at `Assets/Game/Editor/KoreanFontInstaller.cs` creates the TMP asset and registers it in `TMP Settings` automatically. It can also be run manually from:

`Tools > Under The Sea > Install Korean TMP Font`

Existing TMP components may keep their current Latin font. Korean glyphs are resolved through the global fallback. For a text element that should use Noto Sans KR for every character, assign `NotoSansKR-Bold SDF` directly to its Font Asset field.
