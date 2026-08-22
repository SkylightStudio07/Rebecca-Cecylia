RCCom.UI.OperatorManagementUI ui = UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorManagementUI>(
    UnityEngine.FindObjectsInactive.Include);
if (ui == null) { throw new System.InvalidOperationException("OperatorManagementUI를 찾지 못했습니다."); }

ui.Open();
UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(ui);
UnityEngine.UI.Button deploy = serialized.FindProperty("deployButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.UI.Button back = serialized.FindProperty("backButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.Debug.Log("[OperatorButtonRepro] open=" + ui.gameObject.activeInHierarchy +
    ", component=" + ui.enabled +
    ", deploy=" + (deploy != null ? deploy.interactable + "/" + deploy.onClick.GetPersistentEventCount() : "null") +
    ", back=" + (back != null ? back.interactable + "/" + back.onClick.GetPersistentEventCount() : "null"));

if (!ui.gameObject.activeInHierarchy || deploy == null || !deploy.interactable || back == null || !back.interactable)
{
    throw new System.InvalidOperationException("관리 화면 또는 Deploy/Back 버튼이 활성 입력 상태가 아닙니다.");
}

if (back != null) { back.onClick.Invoke(); }
UnityEngine.Debug.Log("[OperatorButtonRepro] afterBack=" + ui.gameObject.activeInHierarchy);
if (ui.gameObject.activeInHierarchy)
{
    throw new System.InvalidOperationException("Back 버튼 호출 후 관리 화면이 닫히지 않았습니다.");
}
