RCCom.Definitions.Operator.OperatorCatalog catalog =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(
        "Assets/Data/Operators/OperatorCatalog.asset");
RCCom.Definitions.Operator.OperatorDefinition definition =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>(
        "Assets/Data/Operators/calliste/OperatorDefinition.asset");

if (catalog == null || catalog.entries == null || catalog.entries.Count < 2 ||
    catalog.entries[0] == null || catalog.entries[0].operatorId != "cassia" ||
    catalog.entries[1] == null || catalog.entries[1].operatorId != "calliste" ||
    catalog.entries[1].displayName != "칼리스테" ||
    !catalog.entries[1].IsUnlocked(0))
{
    throw new System.InvalidOperationException("카탈로그 1번 카시아/2번 칼리스테 또는 즉시 해금 배치가 올바르지 않습니다.");
}

if (definition == null || definition.towerRoster == null || definition.cardRoster == null ||
    definition.allyUnitRoster == null || definition.dialogueSet == null || definition.requiredBestWave != 0)
{
    throw new System.InvalidOperationException("칼리스테 Definition의 임시 로스터·대사·해금 참조가 누락되었습니다.");
}

UnityEngine.Debug.Log("[CallisteBuild] 카시아 1번, 칼리스테 2번 즉시 해금 및 임시 로스터 배선 검증 통과");
