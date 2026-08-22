string portraitPath = "Assets/Art/Character Standing Arts/레베카/레베카/오퍼레이터관리_카시아.png";
UnityEngine.Sprite expected = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(portraitPath);
RCCom.Definitions.Operator.OperatorDefinition definition =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>(
        "Assets/Data/Operators/cassia/OperatorDefinition.asset");
RCCom.Definitions.Operator.OperatorCatalog catalog =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(
        "Assets/Data/Operators/OperatorCatalog.asset");
int catalogIndex = catalog != null ? catalog.FindIndex("cassia") : -1;
RCCom.Definitions.Operator.OperatorCatalogEntry entry =
    catalogIndex >= 0 ? catalog.entries[catalogIndex] : null;

if (expected == null || definition == null || entry == null ||
    definition.managementPortrait != expected || entry.managementPortrait != expected)
{
    throw new System.InvalidOperationException("카시아 관리 카드 포트레잇의 Definition/Catalog 배선이 올바르지 않습니다.");
}

string[] visualPaths =
{
    "Assets/Data/Prefabs/OperatorManagementCard_Normal.prefab",
    "Assets/Data/Prefabs/OperatorManagementCard_Hover.prefab",
    "Assets/Data/Prefabs/OperatorManagementCard_Locked.prefab"
};

foreach (string visualPath in visualPaths)
{
    RCCom.UI.OperatorManagementCardVisual prefab =
        UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.UI.OperatorManagementCardVisual>(visualPath);
    if (prefab == null)
    {
        throw new System.InvalidOperationException($"관리 카드 Visual 프리팹이 없습니다: {visualPath}");
    }

    UnityEngine.GameObject instance = UnityEngine.Object.Instantiate(prefab.gameObject);
    RCCom.UI.OperatorManagementCardVisual visual = instance.GetComponent<RCCom.UI.OperatorManagementCardVisual>();
    visual.Apply(entry, 0, !visualPath.EndsWith("_Locked.prefab"), false, 0);
    UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(visual);
    UnityEngine.UI.Image image = serialized.FindProperty("portraitImage").objectReferenceValue as UnityEngine.UI.Image;
    bool valid = image != null && image.sprite == expected && image.enabled;
    UnityEngine.Object.DestroyImmediate(instance);
    if (!valid)
    {
        throw new System.InvalidOperationException($"관리 카드 Visual이 전용 포트레잇을 표시하지 않습니다: {visualPath}");
    }
}

string[] dependencies = UnityEditor.AssetDatabase.GetDependencies(
    "Assets/Data/Operators/cassia/OperatorDefinition.asset", true);
if (System.Array.IndexOf(dependencies, portraitPath) < 0)
{
    throw new System.InvalidOperationException("관리 카드 포트레잇이 OperatorDefinition Addressables 의존성에 포함되지 않았습니다.");
}

UnityEngine.Debug.Log("[OperatorManagementPortraitCheck] Definition/Catalog/Normal/Hover/Locked/Addressables 의존성 검증 통과");
