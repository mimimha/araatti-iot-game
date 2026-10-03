using UnityEditor;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🖼 <b>무쌍 판 그림을 9-슬라이스로 들여온다.</b>
    ///
    /// <c>Panel_Gold.png</c> 는 배의 <c>voyage-panel</c> 에서 <b>금테만 남기고 안쪽을 비운</b>
    /// 그림이다(돛단배·홈 제거). 9-슬라이스로 들여와야 <b>어떤 크기로 늘려도 테두리 두께가
    /// 그대로</b>다.
    ///
    /// <b>왜 이게 필요했나.</b> 지금까지는 통짜 그림을 <c>preserveAspect</c> 로 그렸다.
    /// 그러면 판을 줄일 때 <b>테두리도 같이 줄어든다.</b> 상단 바(1024→660, 0.65배)는
    /// 두껍게 남고 카드(1024→250, 0.24배)는 실처럼 얇아져서, 같은 세트로 보이지 않았다.
    ///
    /// 경계값은 픽셀을 재서 정했다 — 금색이 좌우 x=42, 상하 y=26 에서 끝난다.
    /// </summary>
    public static class WarriorsPanelImport
    {
        /// <summary>
        /// ⚠ <c>Panel_Gold.png</c>(배 voyage-panel 파생) 대신 <b>코드로 생성한 캡슐 판</b>을 쓴다.
        ///
        /// 원본은 금테 바깥은 캡슐인데 <b>안쪽 가장자리는 반경이 작은 둥근 사각형</b>이라, 남색 면이
        /// "금테 안에 박힌 각진 네모" 로 보였다. 9-슬라이스 경계를 어떻게 잡아도 그림에 그려진
        /// 안쪽 모양은 바뀌지 않는다. 새 그림은 바깥 테·금띠·그림자·남색 면이 전부 같은 중심의
        /// 캡슐(반원 끝)이라 어느 높이로 그려도 한 몸으로 보인다. 원본 파일은 그대로 남겨 둔다.
        /// </summary>
        // ⚠ 코드로 만든 캡슐(Panel_GoldCapsule)은 "너무 민둥하다" 는 지적. 팀이 이미 쓰는
        //    GAME OVER 창의 장식 금테(Common/result-panel-frame.png, 1305×1205)를 절반 크기로
        //    복사해 무쌍 HUD 의 판으로 쓴다. 바깥도 안쪽도 같은 둥근 사각형이라 한 몸으로 보이고,
        //    결과 창과 같은 그림이라 화면 톤이 맞는다. 공용 원본 파일은 건드리지 않는다.
        private const string PanelPath =
            "Assets/Game/Art/MiniGames/Warriors/UI/Panel_GoldOrnate.png";

        /// <summary>
        /// 9-슬라이스 경계. <b>금색이 끝나는 곳이 아니라, 안쪽 곡선과 그림자 띠가 끝나는 곳</b>이다.
        ///
        /// ⚠ 예전 값 (44, 28) 이 "금테 안 각진 남색 네모" 의 원인이었다. 픽셀을 다시 재 보니
        ///    (System.Drawing 으로 실측):
        ///    <code>
        ///      금색 끝            x=42 · y=26
        ///      안쪽 그림자 띠      y=26~31 (검정 → 남색 그라데이션), 아래도 대칭
        ///      순 남색 시작        x≥44 · y≥31
        ///      모서리 안쪽 곡선    x=40~42 열에서 y=47~50 까지 이어짐
        ///    </code>
        ///    세로 경계 28 은 그림자 띠 한가운데와 모서리 곡선을 <b>가로질러</b> 잘랐다. 그래서
        ///    1) 가운데 타일이 y=28 의 검정 줄부터 시작해 <b>어두운 가로 띠</b>로 늘어났고
        ///    2) 모서리 타일이 y=28 에서 끊겨 그 아래 곡선(y 28~50)이 가장자리 타일의
        ///       <b>직선 프로필</b>로 대체됐다 — 둥근 금테 안에 90도 남색 모서리가 생긴 이유다.
        ///
        ///    세로 50 이면 곡선(≤50)과 띠(≤31)가 모두 모서리 타일 안에 들어간다.
        ///    가운데(y 50~90)는 B 채널 73~80 으로 균일해서 늘려도 티가 안 난다.
        ///    가로 44 는 그대로 — 곡선이 x 방향으로는 44 안에서 끝난다.
        /// </summary>
        // 캡슐 판: 양 끝 반원(반지름 70 = 높이의 절반)이 모서리 타일, 가운데는 균일한 띠.
        // 세로는 배율을 판 높이에 맞춰 그리므로(WarriorsHudLayout.PanelPpu) 늘어나지 않지만,
        // 경계는 그림 안에 있어야 하므로 60 으로 두고 가운데 20줄(순 남색)만 남긴다.
        // 256×256 합성(결과 창 금테의 네 모서리 128px 씩 + 평평한 남색 안쪽). 장식 띠는 약 36px,
        // 안쪽 둥근 모서리는 약 40px 까지 이어진다. 40 이면 모서리 타일 안에 다 들어간다.
        // 실측: 금띠 8~28 · 어두운 구분선 28~44 · 순 남색 44~. 구분선이 경계에 걸리면 가운데 타일로
        // 늘어나 "금테 아래 검은 띠" 가 되므로 경계는 48 (가운데 48~208 은 B 76 균일).
        private static readonly Vector4 Border = new Vector4(48f, 48f, 48f, 48f);

        [MenuItem("Tools/아라아띠/Warriors 판 그림 9-슬라이스로 들이기")]
        public static void Wire()
        {
            Import(PanelPath, Border);
        }

        private static void Import(string PanelPath, Vector4 Border)
        {
            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(PanelPath) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError($"[판 그림] 임포터를 찾지 못했습니다 — {PanelPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;

            // ⚠ Single 이어야 파일 경로로 곧장 Sprite 를 읽을 수 있다.
            //    Multiple 이면 하위 에셋이 되어 LoadAssetAtPath<Sprite> 가 null 을 준다.
            //    크라켄 아이콘에서 실제로 그 일을 겪었다.
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = Border;
            settings.spriteMeshType = SpriteMeshType.FullRect;   // 9-슬라이스는 FullRect 여야 한다
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();

            // ⚠ 저장을 믿지 않고 다시 읽는다.
            AssetDatabase.ImportAsset(PanelPath, ImportAssetOptions.ForceUpdate);

            Sprite made = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);

            if (made == null)
            {
                Debug.LogError($"[판 그림] Sprite 로 읽히지 않습니다 — {PanelPath}");
                return;
            }

            if (made.border != Border)
            {
                Debug.LogError($"[판 그림] 테두리가 저장되지 않았습니다 — 넣은 값 {Border}, 실제 {made.border}");
                return;
            }

            Debug.Log(
                $"[판 그림] ✅ {made.rect.width:F0}×{made.rect.height:F0} · " +
                $"9-슬라이스 테두리 좌{made.border.x:F0} 하{made.border.y:F0} " +
                $"우{made.border.z:F0} 상{made.border.w:F0}");
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }
    }
}
