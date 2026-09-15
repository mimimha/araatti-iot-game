using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 🕳 선창 바닥을 **선체 모양 그대로** 한 장으로 만든다. (SHIPCOOP.md 4장)
///
/// ⚠ **네모 판으로는 물이 다 안 가려집니다.**
///
///    예전 선창 바닥은 폭 10.3m · 길이 35m 짜리 상자였습니다. 선체는 z −19.7 ~ 27 이라
///    **뱃머리 쪽 9.5m 와 배 뒤 2.2m 가 비었고**, 옆으로도 0.45m 씩 남았습니다.
///    그 틈으로 바다 판이 비쳐서 침수가 0% 인데도 **배 앞부분 안에 물이 찬 것처럼** 보였습니다.
///    상자를 선체보다 크게 하면 뱃머리처럼 좁아지는 곳에서 배 밖으로 튀어나옵니다.
///
///    그래서 선체를 z 마다 **레이로 재서** 그 폭에 딱 맞는 판을 만듭니다.
///
/// <code>
///   높이   HoldFloorY (−6.2). 흘수선(−8.0)보다 위, 갑판(−3.49)보다 아래
///   폭     그 높이에서 좌 · 우로 선체 콜라이더를 맞힌 자리, 안쪽으로 Inset 만큼 들여서
///   z      선체 앞뒤 끝까지 Step 간격. 어느 쪽이든 못 맞힌 구간은 비운다 (선체가 없는 곳)
///   면     위(+y)만 본다. 위에서 내려보는 카메라가 바다 대신 이걸 본다
/// </code>
///
/// 선체 콜라이더는 배치 도구가 켜 둔다(<c>PlaceShip</c>). 밖에서 안쪽으로 쏘므로 앞면을 맞힌다.
/// 만든 메시는 <c>Art/Models/ShipCoop/HoldFloor_01.asset</c> 에 남기고, 있으면 **덮어쓴다.**
/// 배 모델을 바꾸면 배치 도구를 한 번 돌리는 것으로 따라간다.
/// </summary>
public static class ShipCoopHoldFloorMesh
{
    private const string Folder = "Assets/Game/Art/Models/ShipCoop";
    private const string AssetPath = Folder + "/HoldFloor_01.asset";

    /// <summary>z 방향 재는 간격(m). 뱃머리가 좁아지는 곡선을 따라가려면 1m 는 거칠다.</summary>
    private const float Step = 0.5f;

    /// <summary>선체 안쪽으로 이만큼 들인다(m). 딱 맞추면 선체와 z-fighting 으로 번쩍인다.</summary>
    private const float Inset = 0.12f;

    /// <summary>레이를 쏘기 시작하는 거리(m). 선체 반폭(5.6)보다 넉넉히.</summary>
    private const float ProbeFrom = 30f;

    /// <summary>이보다 좁은 구간은 안 만든다(m). 뱃머리 끝은 판이 없어도 선체가 가린다.</summary>
    private const float MinWidth = 0.3f;

    /// <summary>재고 만든 결과. 로그에 적을 숫자들.</summary>
    public struct Report
    {
        public int Slices;

        /// <summary>레이가 빗나가 앞뒤에서 이어 그린 칸 수. 많으면 선체 콜라이더에 틈이 있다는 뜻이다.</summary>
        public int Filled;

        public float MinZ;
        public float MaxZ;
        public float Widest;
    }

    /// <summary>
    /// 선체를 재서 바닥 메시를 만든다. 선체를 못 재면 null.
    /// </summary>
    /// <param name="ship">배 루트. 이 아래 콜라이더만 선체로 본다.</param>
    /// <param name="floorY">바닥 높이(월드 y).</param>
    /// <param name="centreX">선체 좌우 가운데(월드 x). 왼쪽 레이는 이보다 왼쪽을, 오른쪽 레이는 오른쪽을 맞혀야 한다.</param>
    public static Mesh CreateOrOverwrite(GameObject ship, float floorY, float centreX, out Report report)
    {
        report = default;

        if (ship == null)
        {
            return null;
        }

        if (!HullBounds(ship, out Bounds hull))
        {
            return null;
        }

        // 콜라이더 자리가 트랜스폼을 따라오게. 에디터에서는 물리가 안 돌아서 이걸 안 하면 옛 자리를 맞힌다.
        Physics.SyncTransforms();

        // ⚠ **빠진 칸을 그냥 건너뛰면 바닥에 구멍이 남는다.**
        //
        //    처음에는 레이가 빗나간 칸에서 띠를 끊었다. 그랬더니 83칸 중 77칸만 만들어졌고,
        //    나머지 6칸이 **뚫린 채로** 남았다. 하필 그 구멍 하나가 중간갑판 격자창 밑이라,
        //    격자창으로 내려다보면 선창 바닥이 아니라 **바다**가 보였다 — 배 밑에 물이 찬 것처럼.
        //
        //    선체는 앞뒤로 매끈하게 변하니, 빗나간 칸은 **양옆 성공한 칸에서 이어 그리면** 된다.
        //    어차피 선체 안쪽이라 조금 넓거나 좁아도 밖에서는 안 보인다. 구멍이 남는 쪽이 훨씬 나쁘다.
        int count = Mathf.Max(2, Mathf.FloorToInt((hull.max.z - hull.min.z) / Step) + 1);

        float[] leftX = new float[count];
        float[] rightX = new float[count];
        bool[] found = new bool[count];
        int hits = 0;

        for (int i = 0; i < count; i++)
        {
            float z = hull.min.z + i * Step;

            bool okL = Probe(ship.transform, new Vector3(centreX - ProbeFrom, floorY, z), Vector3.right, out float xl);
            bool okR = Probe(ship.transform, new Vector3(centreX + ProbeFrom, floorY, z), Vector3.left, out float xr);

            // 양쪽 다 맞히고, 왼쪽은 가운데보다 왼쪽 · 오른쪽은 오른쪽이어야 한다. 반대면 먼 쪽 안벽을 맞힌 것이다.
            if (!okL || !okR || xl >= centreX || xr <= centreX || (xr - xl) - 2f * Inset < MinWidth)
            {
                continue;
            }

            found[i] = true;
            leftX[i] = xl;
            rightX[i] = xr;
            hits++;
            report.Widest = Mathf.Max(report.Widest, xr - xl);
        }

        if (hits < 2)
        {
            return null;
        }

        FillGaps(leftX, rightX, found);
        report.Filled = count - hits;

        List<Vector3> left = new List<Vector3>(count);
        List<Vector3> right = new List<Vector3>(count);

        for (int i = 0; i < count; i++)
        {
            float z = hull.min.z + i * Step;
            left.Add(new Vector3(leftX[i] + Inset, floorY, z));
            right.Add(new Vector3(rightX[i] - Inset, floorY, z));
        }

        report.Slices = left.Count;
        report.MinZ = left[0].z;
        report.MaxZ = left[left.Count - 1].z;

        Mesh made = Build(left, right, hull);
        made.name = Path.GetFileNameWithoutExtension(AssetPath);

        return Save(made);
    }

    /// <summary>
    /// 빗나간 칸을 앞뒤 성공한 칸에서 이어 그린다. 양 끝은 가장 가까운 값을 그대로 늘린다.
    /// </summary>
    private static void FillGaps(float[] leftX, float[] rightX, bool[] found)
    {
        int n = found.Length;

        for (int i = 0; i < n; i++)
        {
            if (found[i])
            {
                continue;
            }

            int before = -1;
            for (int j = i - 1; j >= 0; j--)
            {
                if (found[j]) { before = j; break; }
            }

            int after = -1;
            for (int j = i + 1; j < n; j++)
            {
                if (found[j]) { after = j; break; }
            }

            if (before < 0 && after < 0)
            {
                continue;
            }

            if (before < 0) { leftX[i] = leftX[after]; rightX[i] = rightX[after]; continue; }
            if (after < 0) { leftX[i] = leftX[before]; rightX[i] = rightX[before]; continue; }

            float t = (i - before) / (float)(after - before);
            leftX[i] = Mathf.Lerp(leftX[before], leftX[after], t);
            rightX[i] = Mathf.Lerp(rightX[before], rightX[after], t);
        }
    }

    /// <summary>선체(Hull*) · 갑판(Deck*) 렌더러의 경계. 돛대 · 삭구는 뺀다.</summary>
    private static bool HullBounds(GameObject ship, out Bounds box)
    {
        box = default;
        bool any = false;

        foreach (Renderer draw in ship.GetComponentsInChildren<Renderer>())
        {
            if (!draw.name.StartsWith("Hull") && !draw.name.StartsWith("Deck"))
            {
                continue;
            }

            if (!any) { box = draw.bounds; any = true; }
            else { box.Encapsulate(draw.bounds); }
        }

        return any;
    }

    /// <summary>배 밖에서 안쪽으로 쏴서, 배에 속한 콜라이더 중 가장 먼저 맞은 것의 x.</summary>
    private static bool Probe(Transform ship, Vector3 from, Vector3 dir, out float x)
    {
        x = 0f;

        RaycastHit[] hits = Physics.RaycastAll(from, dir, ProbeFrom * 2f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            // 선창 바닥 자신 · 걷는 큐브 · 사람은 배의 자식이 아니다. 배 부품만 본다.
            if (!hits[i].collider.transform.IsChildOf(ship))
            {
                continue;
            }

            if (hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                x = hits[i].point.x;
                found = true;
            }
        }

        return found;
    }

    private static Mesh Build(List<Vector3> left, List<Vector3> right, Bounds hull)
    {
        int n = left.Count;
        Vector3[] points = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        Vector3[] up = new Vector3[n * 2];

        for (int i = 0; i < n; i++)
        {
            points[i * 2] = left[i];
            points[i * 2 + 1] = right[i];

            float v = Mathf.InverseLerp(hull.min.z, hull.max.z, left[i].z);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);

            up[i * 2] = Vector3.up;
            up[i * 2 + 1] = Vector3.up;
        }

        // 띠는 **끊지 않는다.** 빠진 칸은 앞뒤에서 이어 그렸다(FillGaps). 끊으면 그 자리가 구멍이 된다.
        List<int> tris = new List<int>((n - 1) * 6);

        for (int i = 0; i < n - 1; i++)
        {
            int l0 = i * 2, r0 = i * 2 + 1;
            int l1 = (i + 1) * 2, r1 = (i + 1) * 2 + 1;

            // 위(+y)에서 봤을 때 시계 방향이어야 앞면이 위를 본다. (+x 오른쪽, +z 앞)
            tris.Add(l0); tris.Add(l1); tris.Add(r0);
            tris.Add(r0); tris.Add(l1); tris.Add(r1);
        }

        Mesh mesh = new Mesh
        {
            vertices = points,
            uv = uvs,
            normals = up,
            triangles = tris.ToArray()
        };

        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>같은 파일(같은 GUID)에 덮어쓴다. 씬의 참조가 끊어지지 않고 파일이 늘지 않는다.</summary>
    private static Mesh Save(Mesh made)
    {
        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);

        if (existing != null)
        {
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
}
