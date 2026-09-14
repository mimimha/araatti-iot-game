using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 바다 판 하나를 격자로 만든다.
///
/// 유니티 기본 Plane 을 안 쓰는 이유는 두 가지입니다.
///
///   1. 기본 Plane 은 한 변이 10m 라 400 × 1300m 로 늘리려면 크게 키워야 하고,
///      그러면 무늬가 같이 늘어납니다.
///   2. 파도를 넣을 때는 꼭짓점이 촘촘해야 합니다. (아래)
///
/// ⛔ **지금은 파도를 끕니다.** (`ShipCoopDeckLayout.UseWaves`)
///
///    파도는 셰이더가 꼭짓점을 밀어서 만들고, 그러려면 몇 미터마다 꼭짓점이
///    있어야 합니다. 한동안 3m 간격으로 깔아서 한 장에 꼭짓점 5.8만 개를
///    썼습니다. 물결을 빼기로 하면서 그게 전부 낭비가 됐습니다.
///
///    파도를 되살릴 때 <see cref="Step"/> 을 3 으로 되돌리고 파일을 지우면 됩니다.
///
/// 이미 있으면 다시 만들지 않습니다. 간격을 바꾸면 파일을 지우고 다시 돌리세요.
/// </summary>
public static class ShipCoopSeaMesh
{
    private const string Folder = "Assets/Game/Art/Models/ShipCoop";

    /// <summary>
    /// 꼭짓점 간격 (m).
    ///
    /// 물이 평평하므로 꼭짓점이 필요 없습니다. 25m 면 400 × 1300m 한 장에
    /// 꼭짓점 900개입니다. (3m 였을 때는 5.8만 개였습니다)
    /// 파도를 되살리려면 3 으로 되돌리고 만들어진 파일을 지웁니다.
    /// </summary>
    private const float Step = 25f;

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
