// TitleScene에 LiveContent 로딩 블로커 패널을 만들고, 기존 "LiveContent" 버튼에
// RCCom.UI.LiveContentButton 컨트롤러를 부착해 서로 연결한다. 일회성 씬 배선 작업.

UnityEngine.GameObject canvasGo = UnityEngine.GameObject.Find("Canvas");
if (canvasGo == null)
{
    UnityEngine.Debug.LogError("[BuildLiveContentPanel] Canvas를 찾지 못했습니다.");
}
else
{
    // 1. 패널 루트 — 화면 전체를 덮는 배경(Backdrop) + CanvasGroup.
    var panelGo = new UnityEngine.GameObject("LiveContentLoadingPanel", typeof(UnityEngine.RectTransform));
    panelGo.transform.SetParent(canvasGo.transform, false);
    panelGo.transform.SetAsLastSibling();

    var panelRect = panelGo.GetComponent<UnityEngine.RectTransform>();
    panelRect.anchorMin = UnityEngine.Vector2.zero;
    panelRect.anchorMax = UnityEngine.Vector2.one;
    panelRect.offsetMin = UnityEngine.Vector2.zero;
    panelRect.offsetMax = UnityEngine.Vector2.zero;

    var backdrop = panelGo.AddComponent<UnityEngine.UI.Image>();
    backdrop.color = new UnityEngine.Color(0f, 0f, 0f, 0.82f);
    backdrop.raycastTarget = true;

    var canvasGroup = panelGo.AddComponent<UnityEngine.CanvasGroup>();
    canvasGroup.alpha = 0f;
    canvasGroup.interactable = false;
    canvasGroup.blocksRaycasts = false;

    // 2. 안내 문구.
    var textGo = new UnityEngine.GameObject("MessageText", typeof(UnityEngine.RectTransform));
    textGo.transform.SetParent(panelGo.transform, false);
    var textRect = textGo.GetComponent<UnityEngine.RectTransform>();
    textRect.anchorMin = new UnityEngine.Vector2(0.5f, 0.5f);
    textRect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
    textRect.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    textRect.sizeDelta = new UnityEngine.Vector2(900f, 160f);
    textRect.anchoredPosition = UnityEngine.Vector2.zero;

    var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
    tmp.text = "온라인 서비스에서 신규 콘텐츠를 받아오고 있습니다.";
    tmp.alignment = TMPro.TextAlignmentOptions.Center;
    tmp.fontSize = 42f;
    tmp.color = UnityEngine.Color.white;
    tmp.enableWordWrapping = true;

    // 3. LiveContentLoadingPanel 부착 + private 직렬화 필드 연결.
    var panelComponent = panelGo.AddComponent<RCCom.UI.LiveContentLoadingPanel>();
    var panelSO = new UnityEditor.SerializedObject(panelComponent);
    panelSO.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
    panelSO.FindProperty("messageText").objectReferenceValue = tmp;
    panelSO.ApplyModifiedPropertiesWithoutUndo();

    // 4. 기존 "LiveContent" 버튼에 컨트롤러 부착.
    UnityEngine.GameObject liveContentButtonGo =
        UnityEngine.GameObject.Find("Canvas/MainMenuBackground/CommandMenuPanels/LiveContent");
    if (liveContentButtonGo == null)
    {
        UnityEngine.Debug.LogError("[BuildLiveContentPanel] LiveContent 버튼을 찾지 못했습니다.");
    }
    else
    {
        var buttonComponent = liveContentButtonGo.GetComponent<UnityEngine.UI.Button>();
        var liveContentButton = liveContentButtonGo.AddComponent<RCCom.UI.LiveContentButton>();
        var buttonSO = new UnityEditor.SerializedObject(liveContentButton);
        buttonSO.FindProperty("button").objectReferenceValue = buttonComponent;
        buttonSO.FindProperty("loadingPanel").objectReferenceValue = panelComponent;
        buttonSO.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.EditorUtility.SetDirty(liveContentButtonGo);
        UnityEditor.EditorUtility.SetDirty(canvasGo);

        UnityEngine.SceneManagement.Scene activeScene =
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);

        UnityEngine.Debug.Log("[BuildLiveContentPanel] LiveContent 로딩 패널 생성 및 버튼 연결 완료");
    }
}
