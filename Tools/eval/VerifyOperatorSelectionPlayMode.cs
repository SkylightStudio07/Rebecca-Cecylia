if (!UnityEditor.EditorApplication.isPlaying)
{
    throw new System.InvalidOperationException("선택 UI Play Mode 검증은 재생 중에만 실행할 수 있습니다.");
}

RCCom.UI.OperatorSelectionUI selectionUI =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorSelectionUI>(UnityEngine.FindObjectsInactive.Include);
var serialized = new UnityEditor.SerializedObject(selectionUI);
UnityEngine.GameObject panel = serialized.FindProperty("panel").objectReferenceValue as UnityEngine.GameObject;
TMPro.TextMeshProUGUI nameText = serialized.FindProperty("nameText").objectReferenceValue as TMPro.TextMeshProUGUI;
TMPro.TextMeshProUGUI descriptionText = serialized.FindProperty("descriptionText").objectReferenceValue as TMPro.TextMeshProUGUI;
UnityEngine.UI.Button confirm = serialized.FindProperty("confirmButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.UI.Button previous = serialized.FindProperty("previousButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.UI.Button next = serialized.FindProperty("nextButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.CanvasGroup mainMenuGroup = serialized.FindProperty("mainMenuGroup").objectReferenceValue as UnityEngine.CanvasGroup;

if (panel == null || !panel.activeSelf || nameText == null || nameText.text != "카시아" ||
    descriptionText == null || string.IsNullOrWhiteSpace(descriptionText.text) ||
    confirm == null || !confirm.interactable || previous == null || previous.interactable ||
    next == null || next.interactable || mainMenuGroup == null || mainMenuGroup.blocksRaycasts)
{
    throw new System.InvalidOperationException("Play Mode 선택 패널의 표시값 또는 상호작용 상태가 올바르지 않습니다.");
}

selectionUI.Close();
if (panel.activeSelf || !mainMenuGroup.blocksRaycasts)
{
    throw new System.InvalidOperationException("선택 패널 닫기 또는 메인 메뉴 입력 복원이 실패했습니다.");
}

UnityEngine.Debug.Log("[OperatorSelectionPlayMode] 카시아 표시·버튼 상태·뒤로가기 검증 통과");
