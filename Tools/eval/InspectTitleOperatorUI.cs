UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var report = new System.Text.StringBuilder();
report.AppendLine($"scene={scene.name}, dirty={scene.isDirty}");

UnityEngine.Canvas canvas = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Canvas>();
if (canvas != null)
{
    UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
    report.AppendLine($"canvas={GetPath(canvas.transform)}, resolution={scaler?.referenceResolution}");
}

foreach (RCCom.UI.TitleMenuTextButton button in
         UnityEngine.Object.FindObjectsByType<RCCom.UI.TitleMenuTextButton>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var serialized = new UnityEditor.SerializedObject(button);
    int action = serialized.FindProperty("action").enumValueIndex;
    report.AppendLine($"menuButton={GetPath(button.transform)}, action={action}, active={button.gameObject.activeInHierarchy}");
}

TMPro.TextMeshProUGUI sampleText = UnityEngine.Object.FindFirstObjectByType<TMPro.TextMeshProUGUI>(UnityEngine.FindObjectsInactive.Include);
if (sampleText != null)
{
    report.AppendLine($"sampleText={GetPath(sampleText.transform)}, font={UnityEditor.AssetDatabase.GetAssetPath(sampleText.font)}");
}

UnityEngine.Debug.Log($"[TitleInspection]\n{report}");

static string GetPath(UnityEngine.Transform target)
{
    var names = new System.Collections.Generic.List<string>();
    for (UnityEngine.Transform current = target; current != null; current = current.parent)
    {
        names.Add(current.name);
    }

    names.Reverse();
    return string.Join("/", names);
}
