using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// Addressables 번들 안의 머티리얼 값을 유지하면서 셰이더만 현재 플레이어가 보유한 인스턴스로
    /// 다시 연결한다. WebGL용 번들을 Windows Editor의 Use Existing Build 모드에서 읽으면 번들
    /// 내부 셰이더가 지원되지 않아 보라색이 되고, 원격 번들도 빌드 순서에 따라 같은 문제가 날 수
    /// 있으므로 Always Included Shaders와 Shader.Find를 함께 사용한다.
    /// </summary>
    public static class RuntimeShaderMaterialResolver
    {
        private static readonly Dictionary<Material, Material> ResolvedMaterials = new();

        public static Material Resolve(Material source, string expectedShaderName)
        {
            if (source == null || string.IsNullOrWhiteSpace(expectedShaderName))
            {
                return source;
            }

            if (ResolvedMaterials.TryGetValue(source, out Material cached) && cached != null)
            {
                return cached;
            }

            Shader localShader = Shader.Find(expectedShaderName);
            if (localShader == null || !localShader.isSupported)
            {
                Debug.LogError($"[RuntimeShaderMaterialResolver] 로컬 셰이더를 찾지 못했거나 지원하지 않습니다: {expectedShaderName}");
                return source;
            }

            // 원격 머티리얼의 데이터 기반 튜닝값은 보존하고 플랫폼 종속 셰이더 인스턴스만 교체한다.
            var resolved = new Material(source)
            {
                name = $"{source.name} (Runtime)",
                shader = localShader,
                hideFlags = HideFlags.DontSave,
            };
            ResolvedMaterials[source] = resolved;
            return resolved;
        }

        public static void Clear()
        {
            foreach (Material material in ResolvedMaterials.Values)
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }

            ResolvedMaterials.Clear();
        }
    }
}
