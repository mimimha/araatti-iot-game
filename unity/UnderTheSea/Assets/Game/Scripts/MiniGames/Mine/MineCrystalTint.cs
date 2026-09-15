using UnityEngine;

/// <summary>
/// 광물 알갱이 하나의 색. 머티리얼을 건드리지 않고 이 오브젝트만 물들인다.
///
/// **왜 컴포넌트인가** — 색 편차를 <see cref="MaterialPropertyBlock"/> 으로만 넣으면
/// **씬에 저장되지 않는다.** 그것은 런타임 상태라 씬을 다시 열거나 재생을 시작하면
/// 사라지고, 머티리얼 원본 색으로 돌아간다. 편차를 넣은 줄 알았는데 한 번도
/// 안 보이는 일이 실제로 있었다.
///
/// 색은 여기 **직렬화된 값**으로 두고, 그릴 때마다 블록으로 덮는다.
/// 444개에 머티리얼을 하나씩 만들 수는 없으므로 이 방법을 쓴다.
///
/// 블록은 하나를 돌려 쓴다. 컴포넌트마다 들고 있으면 메모리만 먹는다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class MineCrystalTint : MonoBehaviour
{
    [Tooltip("머티리얼의 밑색을 덮는다.")]
    [SerializeField] private Color baseColor = Color.white;

    [Tooltip("머티리얼의 발광을 덮는다. 검정이면 안 빛난다.")]
    [SerializeField] private Color emissionColor = Color.black;

    private static MaterialPropertyBlock _block;

    /// <summary>
    /// 밝을 때 동굴을 어둡히는 배율. <see cref="MineVision"/> 이 정해준다.
    ///
    /// **덮어쓰지 않고 곱한다.** 덮어쓰면 광물의 제 색이 지워져 흰 돌이 된다.
    /// </summary>
    private float _dim = 1f;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        // 인스펙터에서 색을 만지면 바로 보이게 한다.
        Apply();
    }

    /// <summary>색을 정한다. 배치 스크립트가 불러준다.</summary>
    public void Set(Color newBase, Color newEmission)
    {
        baseColor = newBase;
        emissionColor = newEmission;
        Apply();
    }

    /// <summary>밝을 때 얼마나 어둡힐 것인가. 1 이면 그대로.</summary>
    public void SetDim(float dim)
    {
        _dim = Mathf.Max(0f, dim);
        Apply();
    }

    /// <summary>발광만 배로 키운다. 색조는 그대로 둔다.</summary>
    public void ScaleEmission(float gain)
    {
        emissionColor = new Color(emissionColor.r * gain,
                                  emissionColor.g * gain,
                                  emissionColor.b * gain,
                                  emissionColor.a);
        Apply();
    }

    private void Apply()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null) return;

        _block ??= new MaterialPropertyBlock();

        renderer.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", baseColor * _dim);
        _block.SetColor("_EmissionColor", emissionColor * _dim);
        renderer.SetPropertyBlock(_block);
    }
}
