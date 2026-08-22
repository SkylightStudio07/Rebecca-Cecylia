UnityEngine.GameObject root = UnityEngine.GameObject.Find("Canvas/OperatorManagingSystem");
if (root == null)
{
    RCCom.UI.OperatorManagementUI ui = UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorManagementUI>(
        UnityEngine.FindObjectsInactive.Include);
    root = ui != null ? ui.gameObject : null;
}
if (root == null) { throw new System.InvalidOperationException("OperatorManagingSystem을 찾지 못했습니다."); }

string hierarchy = root.name;
for (UnityEngine.Transform p = root.transform.parent; p != null; p = p.parent)
{
    hierarchy = p.name + "/" + hierarchy;
}
UnityEngine.Debug.Log("[OperatorInputInspect] path=" + hierarchy + ", active=" + root.activeInHierarchy);

for (UnityEngine.Transform p = root.transform; p != null; p = p.parent)
{
    UnityEngine.CanvasGroup group = p.GetComponent<UnityEngine.CanvasGroup>();
    if (group != null)
    {
        UnityEngine.Debug.Log("[OperatorInputInspect] CanvasGroup " + p.name +
            " interactable=" + group.interactable + " blocks=" + group.blocksRaycasts +
            " ignoreParent=" + group.ignoreParentGroups);
    }
}

UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(root.GetComponent<RCCom.UI.OperatorManagementUI>());
foreach (string propertyName in new[] { "deployButton", "backButton" })
{
    UnityEngine.UI.Button button = serialized.FindProperty(propertyName).objectReferenceValue as UnityEngine.UI.Button;
    UnityEngine.Debug.Log("[OperatorInputInspect] " + propertyName + "=" +
        (button != null ? button.name + ", active=" + button.gameObject.activeInHierarchy +
        ", interactable=" + button.interactable + ", listeners=" + button.onClick.GetPersistentEventCount() : "null"));
}
