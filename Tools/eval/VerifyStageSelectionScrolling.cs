if (!UnityEditor.EditorApplication.isPlaying)
{
    throw new System.InvalidOperationException("스테이지 스크롤 검증은 Play Mode에서 실행해야 합니다.");
}

RCCom.UI.StageSelectionUI selectionUI =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.StageSelectionUI>(UnityEngine.FindObjectsInactive.Include);
if (selectionUI == null)
{
    throw new System.InvalidOperationException("StageSelectionUI를 찾지 못했습니다.");
}

selectionUI.Open();
UnityEngine.Canvas.ForceUpdateCanvases();

var serialized = new UnityEditor.SerializedObject(selectionUI);
RCCom.Definitions.Stage.StageCatalog catalog =
    serialized.FindProperty("catalog").objectReferenceValue as RCCom.Definitions.Stage.StageCatalog;
UnityEngine.UI.ScrollRect scrollRect =
    serialized.FindProperty("nodeScrollRect").objectReferenceValue as UnityEngine.UI.ScrollRect;
UnityEngine.UI.Button previous =
    serialized.FindProperty("previousNodeButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.UI.Button next =
    serialized.FindProperty("nextNodeButton").objectReferenceValue as UnityEngine.UI.Button;

if (catalog == null || catalog.entries == null || catalog.entries.Count != 7 ||
    catalog.FindById("ch1-06") == null || catalog.FindById("ch1-07") == null)
{
    throw new System.InvalidOperationException("1-1~1-7 카탈로그 구성이 올바르지 않습니다.");
}

if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null ||
    scrollRect.content.childCount != 7 || scrollRect.content.rect.width <= scrollRect.viewport.rect.width ||
    previous == null || next == null || !previous.gameObject.activeSelf || !next.gameObject.activeSelf)
{
    throw new System.InvalidOperationException("7개 노드의 ScrollRect 또는 좌우 버튼 구성이 올바르지 않습니다.");
}

scrollRect.horizontalNormalizedPosition = 0f;
float initial = scrollRect.horizontalNormalizedPosition;
selectionUI.ScrollNext();
float movedRight = scrollRect.horizontalNormalizedPosition;
selectionUI.ScrollPrevious();
float movedLeft = scrollRect.horizontalNormalizedPosition;
if (movedRight <= initial || movedLeft >= movedRight)
{
    throw new System.InvalidOperationException("좌우 한 칸 스크롤 동작이 실패했습니다.");
}

UnityEngine.Debug.Log(
    $"[StageSelectionScrollCheck] stages={catalog.entries.Count} content={scrollRect.content.rect.width:F1} " +
    $"viewport={scrollRect.viewport.rect.width:F1} right={movedRight:F2} left={movedLeft:F2}");
selectionUI.Close();
