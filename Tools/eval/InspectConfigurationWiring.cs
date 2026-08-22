RCCom.UI.TitleMenuTextButton[] buttons = UnityEngine.Object.FindObjectsByType<RCCom.UI.TitleMenuTextButton>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
string report = string.Empty;
for (int i = 0; i < buttons.Length; i++)
{
    UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(buttons[i]);
    UnityEditor.SerializedProperty action = serialized.FindProperty("action");
    if (action.enumValueIndex != (int)RCCom.UI.TitleMenuTextButton.MenuAction.Preference) { continue; }
    UnityEditor.SerializedProperty controller = serialized.FindProperty("configurationController");
    string path = buttons[i].name;
    for (UnityEngine.Transform p = buttons[i].transform.parent; p != null; p = p.parent) { path = p.name + "/" + path; }
    report += path + ", active=" + buttons[i].gameObject.activeInHierarchy +
        ", controller=" + (controller.objectReferenceValue != null ? controller.objectReferenceValue.name : "null") + "; ";
}
throw new System.InvalidOperationException(report.Length > 0 ? report : "Preference action TitleMenuTextButton 없음");
