UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    "Assets/Scenes/TitleScene.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Single);

UnityEngine.Canvas canvas = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Canvas>(
    UnityEngine.FindObjectsInactive.Include);
UnityEngine.Transform mainMenu = canvas != null ? canvas.transform.Find("MainMenuBackground") : null;
UnityEngine.Transform operatorImageTransform = mainMenu != null ? mainMenu.Find("OperatorImage") : null;
UnityEngine.UI.Image operatorImage = operatorImageTransform != null
    ? operatorImageTransform.GetComponent<UnityEngine.UI.Image>()
    : null;
RCCom.UI.LobbyOperatorDialogueUI lobbyUi =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.LobbyOperatorDialogueUI>(
        UnityEngine.FindObjectsInactive.Include);

if (scene.path != "Assets/Scenes/TitleScene.unity" || operatorImage == null || lobbyUi == null)
{
    throw new System.InvalidOperationException("TitleScene의 로비 전신 이미지 또는 대사 UI를 찾지 못했습니다.");
}

UnityEditor.SerializedObject lobbySerialized = new UnityEditor.SerializedObject(lobbyUi);
UnityEngine.Object wiredImage = lobbySerialized.FindProperty("lobbyOperatorImage").objectReferenceValue;
if (wiredImage != operatorImage)
{
    throw new System.InvalidOperationException("로비 대사 UI가 메인 OperatorImage에 연결되지 않았습니다.");
}

UnityEngine.Transform[] descendants = mainMenu.GetComponentsInChildren<UnityEngine.Transform>(true);
for (int i = 0; i < descendants.Length; i++)
{
    if (descendants[i].name == "DialoguePortrait")
    {
        throw new System.InvalidOperationException("잘못 생성된 로비 DialoguePortrait가 아직 남아 있습니다.");
    }
}

RCCom.UI.OperatorDialogueSet dialogueSet =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.UI.OperatorDialogueSet>(
        "Assets/Data/Prefabs/UI/New Operator Dialogue Set.asset");
if (dialogueSet == null || dialogueSet.lobbyIdleSprite == null || dialogueSet.idleSprite == null)
{
    throw new System.InvalidOperationException("로비 전신 또는 전투 기본 포트레잇 데이터가 비어 있습니다.");
}

RCCom.UI.OperatorLineSet probe = new RCCom.UI.OperatorLineSet
{
    portraitSprite = dialogueSet.idleSprite,
    defaultLobbySprite = dialogueSet.lobbyIdleSprite,
    entries = new System.Collections.Generic.List<RCCom.UI.OperatorDialogueEntry>
    {
        new RCCom.UI.OperatorDialogueEntry
        {
            text = "검증용 대사",
            lobbySprite = dialogueSet.lobbyIdleSprite,
        },
    },
};

if (!probe.TryGetRandomLobby(out string lobbyText, out UnityEngine.Sprite lobbySprite) ||
    lobbyText != "검증용 대사" || lobbySprite != dialogueSet.lobbyIdleSprite)
{
    throw new System.InvalidOperationException("로비 문장별 전신 스프라이트 선택 계약이 실패했습니다.");
}

if (!probe.TryGetRandomCombat(out string combatText, out UnityEngine.Sprite combatPortrait) ||
    combatText != "검증용 대사" || combatPortrait != dialogueSet.idleSprite)
{
    throw new System.InvalidOperationException("전투 상황 포트레잇 선택 계약이 실패했습니다.");
}

UnityEngine.Debug.Log(
    "[VerifyLobbySpriteSeparation] 로비 OperatorImage와 전투 상황 포트레잇 분리 검증 통과");
