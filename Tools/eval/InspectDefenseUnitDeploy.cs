UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var report = new System.Text.StringBuilder();
report.AppendLine($"scene={scene.path}, dirty={scene.isDirty}, playing={UnityEditor.EditorApplication.isPlaying}");

RCCom.Runtime.UnitDeployController[] controllers =
    UnityEngine.Object.FindObjectsByType<RCCom.Runtime.UnitDeployController>(
        UnityEngine.FindObjectsInactive.Include,
        UnityEngine.FindObjectsSortMode.None);
report.AppendLine($"controllers={controllers.Length}");
foreach (RCCom.Runtime.UnitDeployController controller in controllers)
{
    var serialized = new UnityEditor.SerializedObject(controller);
    UnityEngine.Object roster = serialized.FindProperty("allyUnitRoster").objectReferenceValue;
    UnityEngine.Object viewPrefab = serialized.FindProperty("viewPrefab").objectReferenceValue;
    report.AppendLine(
        $"controller={GetPath(controller.transform)}, active={controller.gameObject.activeInHierarchy}, " +
        $"enabled={controller.enabled}, roster={UnityEditor.AssetDatabase.GetAssetPath(roster)}, " +
        $"view={UnityEditor.AssetDatabase.GetAssetPath(viewPrefab)}, available={controller.IsAvailable}");
}

RCCom.UI.UnitDeployMenuUI[] menus =
    UnityEngine.Object.FindObjectsByType<RCCom.UI.UnitDeployMenuUI>(
        UnityEngine.FindObjectsInactive.Include,
        UnityEngine.FindObjectsSortMode.None);
report.AppendLine($"menus={menus.Length}");
foreach (RCCom.UI.UnitDeployMenuUI menu in menus)
{
    var serialized = new UnityEditor.SerializedObject(menu);
    UnityEngine.Object controller = serialized.FindProperty("deployController").objectReferenceValue;
    UnityEngine.Object panel = serialized.FindProperty("panelGroup").objectReferenceValue;
    UnityEngine.Object content = serialized.FindProperty("contentParent").objectReferenceValue;
    UnityEngine.Object prefab = serialized.FindProperty("buttonPrefab").objectReferenceValue;
    report.AppendLine(
        $"menu={GetPath(menu.transform)}, activeSelf={menu.gameObject.activeSelf}, " +
        $"activeHierarchy={menu.gameObject.activeInHierarchy}, enabled={menu.enabled}, " +
        $"controller={GetPath((controller as UnityEngine.Component)?.transform)}, " +
        $"panel={(panel != null)}, content={GetPath((content as UnityEngine.Component)?.transform)}, " +
        $"prefabNull={(prefab == null)}, prefab={GetAssetPath(prefab)}, runtimeVisible={menu.IsVisible}, " +
        $"runtimeButtons={menu.ButtonCount}");
}

UnityEngine.Debug.Log($"[DefenseUnitDeployInspection]\n{report}");
return report.ToString();

static string GetPath(UnityEngine.Transform target)
{
    if (target == null)
    {
        return "<null>";
    }

    var names = new System.Collections.Generic.List<string>();
    for (UnityEngine.Transform current = target; current != null; current = current.parent)
    {
        names.Add(current.name);
    }

    names.Reverse();
    return string.Join("/", names);
}

static string GetAssetPath(UnityEngine.Object target)
{
    if (target is UnityEngine.Component component)
    {
        return UnityEditor.AssetDatabase.GetAssetPath(component.gameObject);
    }

    return target != null ? UnityEditor.AssetDatabase.GetAssetPath(target) : "<null>";
}
