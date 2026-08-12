RCCom.UI.OperatorSelectionUI selectionUI =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorSelectionUI>(UnityEngine.FindObjectsInactive.Include);
if (selectionUI == null)
{
    throw new System.InvalidOperationException("Play Mode에서 OperatorSelectionUI를 찾지 못했습니다.");
}

selectionUI.Open();
