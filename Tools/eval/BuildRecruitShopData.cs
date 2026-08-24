RCCom.EditorTools.OperatorBuildReport report = RCCom.EditorTools.OperatorAssetBuilder.BuildSingle(
    "Assets/Editor/OperatorRecipes/Calliste.json");
if (!report.validationPassed)
{
    throw new System.InvalidOperationException("칼리스테 오퍼레이터 에셋 검증에 실패했습니다.");
}

RCCom.EditorTools.LobbyShopPanelSetup.Setup();
RCCom.EditorTools.LobbyShopPanelSetup.Validate();

RCCom.Definitions.Operator.OperatorDefinition definition =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>(
        "Assets/Data/Operators/calliste/OperatorDefinition.asset");
if (definition == null || definition.shopPortrait == null ||
    definition.shopUpperBodyPortrait == null || definition.alternateName != "Bartender" ||
    string.IsNullOrWhiteSpace(definition.shopDialogue) || definition.purchasePrice != 100)
{
    throw new System.InvalidOperationException("칼리스테 상점 데이터가 레시피와 일치하지 않습니다.");
}

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log("[RecruitShopBuild] 칼리스테 상점 데이터 생성과 TitleScene 배선 검증 완료");
