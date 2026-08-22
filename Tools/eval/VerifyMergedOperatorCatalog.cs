RCCom.Definitions.Operator.OperatorCatalog catalog =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(
        "Assets/Data/Operators/OperatorCatalog.asset");
if (catalog == null || catalog.entries == null || catalog.entries.Count != 3 ||
    catalog.entries[0] == null || catalog.entries[0].operatorId != "cassia" ||
    catalog.entries[1] == null || catalog.entries[1].operatorId != "calliste" ||
    catalog.entries[2] == null || catalog.entries[2].operatorId != "racing")
{
    throw new System.InvalidOperationException("통합 카탈로그 순서가 카시아/칼리스테/실비아가 아닙니다.");
}

UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings =
    UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
foreach (string groupName in new[] { "Operator-cassia-Local", "Operator-calliste-Local", "Operator-racing-Local" })
{
    if (settings == null || settings.FindGroup(groupName) == null)
    {
        throw new System.InvalidOperationException("Addressables 그룹이 누락되었습니다: " + groupName);
    }
}

UnityEngine.Debug.Log("[MergedOperators] 카시아/칼리스테/실비아 카탈로그 및 Addressables 검증 통과");
