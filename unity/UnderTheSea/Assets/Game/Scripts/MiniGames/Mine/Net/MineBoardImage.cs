using System.Collections.Generic;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 우리가 판 그림을 PNG 로 만든다. 한 줄 평을 받으려고 LLM 에 보내는 그림이다.
    ///
    /// 판 칸은 검정, 안 판 칸은 흰색이다. 돌 색 · 금 · 캐릭터는 넣지 않는다 —
    /// AI 가 봐야 하는 것은 <b>모양 하나</b>다.
    ///
    /// ⚠ 위아래를 뒤집지 않는다. <c>MineGrid</c> 는 y = size − 1 이 맨 위이고(<c>MineDrawingTarget.ToCells</c>),
    ///    <c>Texture2D</c> 도 y 가 위로 커진다. 그래서 칸 (x, y) 를 픽셀 (x, y) 에 그대로 칠한다.
    /// </summary>
    public static class MineBoardImage
    {
        /// <summary>칸 하나를 몇 픽셀로 그리는가. 20칸이면 320×320 이다.</summary>
        private const int CellPixels = 16;

        public static byte[] EncodePng(IReadOnlyList<bool> cells, int size)
        {
            int side = size * CellPixels;
            var pixels = new Color32[side * side];
            var dug = new Color32(0, 0, 0, 255);
            var rock = new Color32(255, 255, 255, 255);

            for (int py = 0; py < side; py++)
            {
                for (int px = 0; px < side; px++)
                {
                    int index = (py / CellPixels) * size + (px / CellPixels);
                    pixels[py * side + px] = cells[index] ? dug : rock;
                }
            }

            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);

            try
            {
                texture.SetPixels32(pixels);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.Destroy(texture);
            }
        }
    }
}
