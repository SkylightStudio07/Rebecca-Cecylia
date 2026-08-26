UnityEngine.Shader shader = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(
    "Assets/Shaders/Tower/LaserBeam.shader");
UnityEngine.Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(
    "Assets/Data/Prefabs/VFX/LaserBeam.mat");
UnityEngine.GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
    "Assets/Data/Prefabs/VFX/LaserBeamView.prefab");

if (shader == null || material == null || prefab == null)
{
    throw new System.InvalidOperationException("레이저 셰이더/머티리얼/프리팹 중 하나를 찾지 못했습니다.");
}

UnityEditor.ShaderMessage[] messages = UnityEditor.ShaderUtil.GetShaderMessages(shader);
UnityEngine.LineRenderer[] lines = prefab.GetComponentsInChildren<UnityEngine.LineRenderer>(true);
var report = new System.Text.StringBuilder();
report.AppendLine("[LaserShaderInspector]");
UnityEditor.AddressableAssets.Settings.AddressableAssetSettings addressableSettings =
    UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
report.AppendLine("activeBuildTarget=" + UnityEditor.EditorUserBuildSettings.activeBuildTarget);
report.AppendLine("playModeBuilderIndex=" + addressableSettings.ActivePlayModeDataBuilderIndex +
                  ", playModeBuilder=" + addressableSettings.ActivePlayModeDataBuilder.Name);
report.AppendLine("shader=" + shader.name + ", supported=" + shader.isSupported +
                  ", passCount=" + shader.passCount + ", errors=" +
                  UnityEditor.ShaderUtil.ShaderHasError(shader));
report.AppendLine("materialShader=" + (material.shader != null ? material.shader.name : "null"));
UnityEngine.Material resolvedMaterial = RCCom.Runtime.Visuals.RuntimeShaderMaterialResolver.Resolve(
    material, "RCCom/Tower Visuals/Laser Beam");
report.AppendLine("resolvedMaterialShader=" +
                  (resolvedMaterial != null && resolvedMaterial.shader != null
                      ? resolvedMaterial.shader.name
                      : "null") + ", supported=" +
                  (resolvedMaterial != null && resolvedMaterial.shader != null &&
                   resolvedMaterial.shader.isSupported));
report.AppendLine("prefabLines=" + lines.Length);
for (int i = 0; i < lines.Length; i++)
{
    UnityEngine.Material shared = lines[i].sharedMaterial;
    report.AppendLine(lines[i].name + ": material=" +
                      (shared != null ? shared.name : "null") + ", shader=" +
                      (shared != null && shared.shader != null ? shared.shader.name : "null") +
                      ", supported=" +
                      (shared != null && shared.shader != null && shared.shader.isSupported));
}

for (int i = 0; i < messages.Length; i++)
{
    report.AppendLine(messages[i].severity + ": " + messages[i].message +
                      " (" + messages[i].platform + ", line " + messages[i].line + ")");
}

RCCom.Runtime.LaserBeamView[] runtimeViews =
    UnityEngine.Object.FindObjectsByType<RCCom.Runtime.LaserBeamView>(
        UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
report.AppendLine("runtimeViews=" + runtimeViews.Length);
for (int viewIndex = 0; viewIndex < runtimeViews.Length; viewIndex++)
{
    UnityEngine.LineRenderer[] runtimeLines =
        runtimeViews[viewIndex].GetComponentsInChildren<UnityEngine.LineRenderer>(true);
    for (int lineIndex = 0; lineIndex < runtimeLines.Length; lineIndex++)
    {
        UnityEngine.Material shared = runtimeLines[lineIndex].sharedMaterial;
        report.AppendLine("runtime/" + runtimeViews[viewIndex].name + "/" + runtimeLines[lineIndex].name +
                          ": material=" + (shared != null ? shared.name : "null") + ", shader=" +
                          (shared != null && shared.shader != null ? shared.shader.name : "null") +
                          ", supported=" +
                          (shared != null && shared.shader != null && shared.shader.isSupported));
    }
}

UnityEngine.Debug.Log(report.ToString());
