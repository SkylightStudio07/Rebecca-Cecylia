// LiveContentLoadingPanel에 다운로드 속도 표시용 보조 텍스트(ProgressText)를 추가하고 연결한다.
UnityEngine.GameObject panelGo = UnityEngine.GameObject.Find("Canvas/LiveContentLoadingPanel");
if (panelGo == null)
{
    UnityEngine.Debug.LogError("[AddLiveContentProgressText] LiveContentLoadingPanel을 찾지 못했습니다.");
}
else
{
    var progressGo = new UnityEngine.GameObject("ProgressText", typeof(UnityEngine.RectTransform));
    progressGo.transform.SetParent(panelGo.transform, false);
    var progressRect = progressGo.GetComponent<UnityEngine.RectTransform>();
    progressRect.anchorMin = new UnityEngine.Vector2(0.5f, 0.5f);
    progressRect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
    progressRect.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    progressRect.sizeDelta = new UnityEngine.Vector2(700f, 60f);
    // 안내 문구(MessageText) 바로 아래에 배치.
    progressRect.anchoredPosition = new UnityEngine.Vector2(0f, -60f);

    var fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
        "Assets/Resource/Font/Pretendard-Bold SDF.asset");

    var progressTmp = progressGo.AddComponent<TMPro.TextMeshProUGUI>();
    progressTmp.text = string.Empty;
    progressTmp.alignment = TMPro.TextAlignmentOptions.Center;
    progressTmp.fontSize = 28f;
    progressTmp.color = new UnityEngine.Color(1f, 1f, 1f, 0.75f);
    if (fontAsset != null)
    {
        progressTmp.font = fontAsset;
    }

    var panelComponent = panelGo.GetComponent<RCCom.UI.LiveContentLoadingPanel>();
    var panelSO = new UnityEditor.SerializedObject(panelComponent);
    panelSO.FindProperty("progressText").objectReferenceValue = progressTmp;
    panelSO.ApplyModifiedPropertiesWithoutUndo();

    UnityEditor.EditorUtility.SetDirty(panelGo);
    UnityEngine.SceneManagement.Scene activeScene =
        UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);

    UnityEngine.Debug.Log("[AddLiveContentProgressText] ProgressText 생성 및 연결 완료");
}
