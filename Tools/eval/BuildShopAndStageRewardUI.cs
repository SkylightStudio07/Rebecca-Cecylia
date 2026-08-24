RCCom.EditorTools.LobbyShopPanelSetup.SetupShopLeftNavigation();
RCCom.EditorTools.StageRewardAcquisitionSetup.Setup();

RCCom.Definitions.Operator.OperatorCatalog catalog =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(
        "Assets/Data/Operators/OperatorCatalog.asset");
RCCom.Definitions.Operator.OperatorCatalogEntry sylvia = catalog != null
    ? catalog.entries.Find(entry => entry != null && entry.operatorId == "racing")
    : null;
if (sylvia == null ||
    sylvia.unlockType != RCCom.Definitions.Operator.OperatorUnlockType.StageClearReward ||
    !string.Equals(sylvia.requiredStageId, "ch1-02",
        System.StringComparison.OrdinalIgnoreCase))
{
    throw new System.InvalidOperationException("실비아의 1-2 스테이지 보상 조건이 올바르지 않습니다.");
}

UnityEngine.GameObject titleCanvas = null;
foreach (UnityEngine.GameObject candidate in
    UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
{
    if (candidate.scene.IsValid() && candidate.scene.isLoaded && candidate.name == "Canvas" &&
        candidate.transform.Find("MainMenuBackground") != null)
    {
        titleCanvas = candidate;
        break;
    }
}
if (titleCanvas == null) { throw new System.InvalidOperationException("TitleScene Canvas를 찾지 못했습니다."); }

string[] lobbyButtonNames = { "RecruitButton", "ExchangeButton", "EnhanceButton", "MaterialButton" };
string[] lobbySpritePaths =
{
    "Assets/Art/UI/Recruit.png",
    "Assets/Art/UI/Exchange.png",
    "Assets/Art/UI/Enhance.png",
    "Assets/Art/UI/Material.png",
};
UnityEngine.Transform lobbyUnderPanel = titleCanvas.transform.Find("MainMenuBackground/underPanel");
for (int i = 0; i < lobbyButtonNames.Length; i++)
{
    UnityEngine.UI.Image image = lobbyUnderPanel.Find(lobbyButtonNames[i])?.GetComponent<UnityEngine.UI.Image>();
    if (image == null || UnityEditor.AssetDatabase.GetAssetPath(image.sprite) != lobbySpritePaths[i])
    {
        throw new System.InvalidOperationException(
            $"로비 하단 {lobbyButtonNames[i]}이 Shop 좌측 메뉴 배선에 의해 변경됐습니다.");
    }
}
