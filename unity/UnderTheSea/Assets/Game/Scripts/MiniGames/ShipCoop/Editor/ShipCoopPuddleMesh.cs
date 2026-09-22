using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 🌊 갑판에 고인 물 웅덩이 한 장을 **가장자리가 울퉁불퉁한 둥근 판**으로 만든다.
///
/// 유니티 Quad 를 안 쓰는 이유 — 네모는 끝이 직선으로 잘려서 물이 아니라 **타일**처럼 보인다.
/// 한동안 두 장을 어긋나게 겹쳐 정사각형을 숨겼는데, 그래도 모서리가 남았다.
/// 물방울처럼 가운데 꼭짓점 하나에서 팬(fan) 삼각형을 뿌리고, 바깥 꼭짓점의 반지름에
/// 노이즈를 줘서 둥글되 고르지 않게 만든다.
///
/// <code>
///   꼭짓점   가운데 1 + 바깥 Points 개
///   반지름   Diameter / 2 × (1 ± NoiseAmount)     시드 고정 — 다시 만들어도 같은 모양
///   UV       평면 투영 (x, z → u, v). SeaWater 셰이더가 UV 로 무늬를 흘리므로 반드시 있어야 한다
///   노멀     전부 +y (위를 본다)
/// </code>
///
/// 만든 메시는 <see cref="ShipCoopSeaMesh"/> 와 같은 폴더에 파일로 남긴다. 파일이 있으면 **덮어쓴다** —
/// 모양 상수를 바꾸면 다음 배치에서 그대로 반영되고, 중복 파일이 생기지 않는다.
///
/// 재질은 여기서 정하지 않는다. 배치 도구(<c>ShipCoopDeckLayout.DressPuddle</c>)가 SeaWater.mat 을 입힌다.
/// </summary>
public static class ShipCoopPuddleMesh
{
    private const string Folder = "Assets/Game/Art/Models/ShipCoop";
    private const string AssetPath = Folder + "/Puddle_01.asset";

    /// <summary>지름(m). 예전 Quad 웅덩이(PuddleSize 1.6)와 같은 크기.</summary>
    public const float Diameter = 1.6f;

    /// <summary>바깥 꼭짓점 수. 24~32 사이. 적으면 각이 보이고, 많으면 노이즈가 잔떨림이 된다.</summary>
    private const int Points = 28;

    /// <summary>반지름을 이만큼(±) 흔든다. 0.2 면 ±20%.</summary>
    private const float NoiseAmount = 0.2f;

    /// <summary>
    /// 노이즈 시드. **상수로 고정** — 툴을 다시 돌려도 같은 모양이어야 배치가 흔들리지 않는다.
    /// 다른 모양이 보고 싶으면 이 값을 바꾼다.
    /// </summary>
    private const int Seed = 20260915;

    /// <summary>웅덩이 메시를 만들어 돌려준다. 파일이 있으면 내용을 덮어쓴다.</summary>
    public static Mesh CreateOrOverwrite()
    {
        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        Mesh made = Build();
        made.name = System.IO.Path.GetFileNameWithoutExtension(AssetPath);

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);

        if (existing != null)
        {
            // 같은 파일(같은 GUID)을 유지해야 씬 · 프리팹의 참조가 끊어지지 않는다.
            existing.Clear();
            EditorUtility.CopySerialized(made, existing);
            existing.name = made.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(made);
        }
        else
        {
            AssetDatabase.CreateAsset(made, AssetPath);
        }

        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
    }

    private static Mesh Build()
    {
        // System.Random 은 시드가 같으면 플랫폼 · 실행마다 같은 수열을 낸다. UnityEngine.Random 은 전역 상태를 건드린다.
        System.Random noise = new System.Random(Seed);

        float radius = Diameter * 0.5f;

        Vector3[] points = new Vector3[Points + 1];
        Vector2[] uvs = new Vector2[points.Length];
        Vector3[] up = new Vector3[points.Length];

        points[0] = Vector3.zero;

        for (int i = 0; i < Points; i++)
        {
            float angle = (i / (float)Points) * Mathf.PI * 2f;

            // -1 ~ +1 을 NoiseAmount 만큼 섞는다. 이웃 꼭짓점과 독립이라 물방울처럼 들쭉날쭉하다.
            float wobble = 1f + ((float)noise.NextDouble() * 2f - 1f) * NoiseAmount;
            float r = radius * wobble;

            points[i + 1] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }

        for (int i = 0; i < points.Length; i++)
        {
            // 평면 투영. 가운데가 (0.5, 0.5), 지름이 0~1 에 걸린다. 노이즈로 튀어나온 부분은 살짝 밖으로 나가도 된다.
            uvs[i] = new Vector2(points[i].x / Diameter + 0.5f, points[i].z / Diameter + 0.5f);
            up[i] = Vector3.up;
        }

        // 가운데(0)에서 이웃한 바깥 두 점으로 삼각형. 위(+y)에서 봤을 때 시계 방향이어야 앞면이 위를 본다.
        int[] tris = new int[Points * 3];

        for (int i = 0; i < Points; i++)
        {
            int a = i + 1;
            int b = (i + 1) % Points + 1;

            tris[i * 3] = 0;
            tris[i * 3 + 1] = b;
            tris[i * 3 + 2] = a;
        }

        Mesh mesh = new Mesh
        {
            vertices = points,
            uv = uvs,
            normals = up,
            triangles = tris
        };

        // 물 셰이더가 잔결(노멀맵)을 얹으려면 접선이 있어야 한다. (ShipCoopSeaMesh 와 같은 이유)
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }
}
