using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 파도가 칠 수 있는 **촘촘한 바다 판**을 만든다.
///
/// ⚠ 왜 필요한가 — 유니티 기본 Plane 으로는 파도가 안 칩니다.
///
///    물 셰이더는 꼭짓점을 위아래로 밀어서 파도를 만듭니다. (`_Displacement_Amount`)
///    그런데 기본 Plane 은 한 변에 꼭짓점이 11개뿐입니다.
///    400m 짜리로 늘리면 **40m 마다 한 번 꺾이는** 판이 됩니다.
///    밀 곳이 없으니 아무리 값을 올려도 물이 평평합니다.
///    (WaterWorks 가 주는 `Water_Plane` 프리팹도 같은 기본 Plane 입니다)
///
/// 그래서 몇 미터마다 꼭짓점이 있는 격자를 만들어 씁니다.
///
/// 이미 있으면 다시 만들지 않습니다. 촘촘함을 바꾸고 싶으면 파일을 지우고
/// 다시 돌리면 됩니다.
/// </summary>
public static class ShipCoopSeaMesh
{
    private const string Folder = "Assets/Game/Art/Models/ShipCoop";

    /// <summary>
    /// 꼭짓점 간격 (m). 촘촘할수록 파도가 곱지만 무거워집니다.
    ///
    /// 물결 하나가 25m 안팎이라 3m 면 한 물결에 여덟 점입니다. 충분합니다.
    /// 400 × 1300m 한 장에 꼭짓점 약 5.8만 개, 삼각형 11.6만 개입니다.
    /// </summary>
    private const float Step = 3f;

    /// <summary>
    /// 바다 판 하나를 만들어 돌려준다. 없으면 그려서 파일로 남긴다.
    /// </summary>
    public static Mesh GetOrCreate(float width, float length)
    {
        string path = $"{Folder}/SeaGrid_{width:F0}x{length:F0}.asset";

        Mesh found = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        if (found != null)
        {
            return found;
        }

        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        int columns = Mathf.Max(1, Mathf.RoundToInt(width / Step));
        int rows = Mathf.Max(1, Mathf.RoundToInt(length / Step));

        Mesh made = Build(width, length, columns, rows);
        made.name = Path.GetFileNameWithoutExtension(path);

        AssetDatabase.CreateAsset(made, path);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static Mesh Build(float width, float length, int columns, int rows)
    {
        int acrossVerts = columns + 1;
        int alongVerts = rows + 1;

        Vector3[] points = new Vector3[acrossVerts * alongVerts];
        Vector2[] uvs = new Vector2[points.Length];
        Vector3[] up = new Vector3[points.Length];

        for (int z = 0; z < alongVerts; z++)
        {
            for (int x = 0; x < acrossVerts; x++)
            {
                int i = z * acrossVerts + x;

                float fx = x / (float)columns;
                float fz = z / (float)rows;

                // 가운데가 원점. 배치할 때 다루기 쉽다.
                points[i] = new Vector3((fx - 0.5f) * width, 0f, (fz - 0.5f) * length);
                uvs[i] = new Vector2(fx, fz);
                up[i] = Vector3.up;
            }
        }

        int[] tris = new int[columns * rows * 6];
        int t = 0;

        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < columns; x++)
            {
                int corner = z * acrossVerts + x;

                tris[t++] = corner;
                tris[t++] = corner + acrossVerts;
                tris[t++] = corner + 1;

                tris[t++] = corner + 1;
                tris[t++] = corner + acrossVerts;
                tris[t++] = corner + acrossVerts + 1;
            }
        }

        Mesh mesh = new Mesh();

        // 꼭짓점이 6만 개를 넘을 수 있다. 16비트로는 모자란다.
        mesh.indexFormat = points.Length > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.vertices = points;
        mesh.uv = uvs;
        mesh.normals = up;
        mesh.triangles = tris;

        // 물 셰이더가 잔결(노멀맵)을 얹으려면 접선이 있어야 한다.
        // 없으면 물이 매끈한 거울이 된다. 기본 Plane 에는 들어 있다.
        mesh.RecalculateTangents();

        // 파도로 꼭짓점이 오르내려도 화면 밖으로 잘리지 않게 넉넉히 잡는다.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(width, 20f, length));

        return mesh;
    }
}
