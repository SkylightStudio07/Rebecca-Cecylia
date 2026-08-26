UnityEngine.Renderer[] renderers = UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
var report = new System.Text.StringBuilder();
report.AppendLine("[RuntimeMaterialInspector] rendererCount=" + renderers.Length);
int suspiciousCount = 0;
for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
{
    UnityEngine.Material[] materials = renderers[rendererIndex].sharedMaterials;
    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
    {
        UnityEngine.Material material = materials[materialIndex];
        UnityEngine.Shader shader = material != null ? material.shader : null;
        string shaderName = shader != null ? shader.name : "null";
        bool suspicious = material == null || shader == null || !shader.isSupported ||
                          shaderName.Contains("Error") || shaderName.Contains("InternalError");
        if (!suspicious)
        {
            continue;
        }

        suspiciousCount++;
        report.AppendLine(renderers[rendererIndex].GetType().Name + "/" +
                          renderers[rendererIndex].gameObject.name + " [active=" +
                          renderers[rendererIndex].gameObject.activeInHierarchy + "]: material=" +
                          (material != null ? material.name : "null") + ", shader=" + shaderName +
                          ", supported=" + (shader != null && shader.isSupported));
    }
}

report.AppendLine("suspiciousCount=" + suspiciousCount);
UnityEngine.Debug.Log(report.ToString());
