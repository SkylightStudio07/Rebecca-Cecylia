UnityEngine.SceneManagement.Scene scene =
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
        "Assets/Scenes/DefenseScene.unity",
        UnityEditor.SceneManagement.OpenSceneMode.Single);
RCCom.UI.GameResultUI controller =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.GameResultUI>(
        UnityEngine.FindObjectsInactive.Include);
if (controller == null)
{
    throw new System.InvalidOperationException("DefenseScene GameResultUI를 찾지 못했습니다.");
}

var serialized = new UnityEditor.SerializedObject(controller);
UnityEngine.CanvasGroup panelGroup =
    serialized.FindProperty("panelGroup").objectReferenceValue as UnityEngine.CanvasGroup;
if (panelGroup == null)
{
    throw new System.InvalidOperationException("GameResultUI panelGroup 연결이 비어 있습니다.");
}

UnityEngine.Transform existing = panelGroup.transform.Find("StageOutcomeTitle");
UnityEngine.GameObject titleObject = existing != null
    ? existing.gameObject
    : new UnityEngine.GameObject("StageOutcomeTitle", typeof(UnityEngine.RectTransform),
        typeof(UnityEngine.CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
if (existing == null)
{
    titleObject.transform.SetParent(panelGroup.transform, false);
}

UnityEngine.RectTransform rect = titleObject.GetComponent<UnityEngine.RectTransform>();
rect.anchorMin = new UnityEngine.Vector2(0.08f, 0.79f);
rect.anchorMax = new UnityEngine.Vector2(0.92f, 0.92f);
rect.offsetMin = UnityEngine.Vector2.zero;
rect.offsetMax = UnityEngine.Vector2.zero;
TMPro.TextMeshProUGUI title = titleObject.GetComponent<TMPro.TextMeshProUGUI>();
TMPro.TextMeshProUGUI referenceText =
    serialized.FindProperty("reachedWaveText").objectReferenceValue as TMPro.TextMeshProUGUI;
title.font = referenceText != null ? referenceText.font : title.font;
title.fontSize = 32f;
title.fontStyle = TMPro.FontStyles.Bold;
title.alignment = TMPro.TextAlignmentOptions.Center;
title.color = UnityEngine.Color.white;
title.text = "MISSION RESULT";
title.raycastTarget = false;

serialized.FindProperty("resultTitleText").objectReferenceValue = title;
serialized.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(titleObject);
UnityEditor.EditorUtility.SetDirty(controller);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log("[BuildStageResultOutcomeTitle] 결과 패널 승패 제목 연결 완료");
