const string catalogPath = "Assets/Data/Operators/OperatorCatalog.asset";
const string definitionPath = "Assets/Data/Operators/cassia/OperatorDefinition.asset";

var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(catalogPath);
if (catalog == null || catalog.entries.Count != 1)
{
    throw new System.InvalidOperationException("OperatorCatalog가 없거나 Cassia 단일 항목 구성이 아닙니다.");
}

RCCom.Definitions.Operator.OperatorCatalogEntry catalogEntry = catalog.entries[0];
if (catalogEntry.operatorId != "cassia" || catalogEntry.address != "operator/cassia" ||
    catalogEntry.remoteContent || !catalogEntry.IsUnlocked(0))
{
    throw new System.InvalidOperationException("Cassia 카탈로그 메타데이터가 올바르지 않습니다.");
}

UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings =
    UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
string definitionGuid = UnityEditor.AssetDatabase.AssetPathToGUID(definitionPath);
UnityEditor.AddressableAssets.Settings.AddressableAssetEntry addressableEntry = settings?.FindAssetEntry(definitionGuid);
if (addressableEntry == null || addressableEntry.address != catalogEntry.address ||
    addressableEntry.parentGroup.Name != "Operator-cassia-Local")
{
    throw new System.InvalidOperationException("Cassia Addressables 주소 또는 그룹이 올바르지 않습니다.");
}

RCCom.UI.OperatorSelectionUI selectionUI =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorSelectionUI>(UnityEngine.FindObjectsInactive.Include);
if (selectionUI == null)
{
    throw new System.InvalidOperationException("TitleScene에 OperatorSelectionUI가 없습니다.");
}

var selectionSerialized = new UnityEditor.SerializedObject(selectionUI);
string[] requiredFields =
{
    "catalog", "panel", "mainMenuGroup", "portraitImage", "nameText", "descriptionText",
    "unlockText", "statusText", "downloadProgress", "previousButton", "nextButton",
    "confirmButton", "backButton",
};
foreach (string fieldName in requiredFields)
{
    if (selectionSerialized.FindProperty(fieldName).objectReferenceValue == null)
    {
        throw new System.InvalidOperationException($"OperatorSelectionUI 필드가 비어 있습니다: {fieldName}");
    }
}

RCCom.UI.TitleMenuTextButton newGameButton = null;
foreach (RCCom.UI.TitleMenuTextButton candidate in
         UnityEngine.Object.FindObjectsByType<RCCom.UI.TitleMenuTextButton>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var candidateSerialized = new UnityEditor.SerializedObject(candidate);
    if (candidateSerialized.FindProperty("action").enumValueIndex == 0)
    {
        newGameButton = candidate;
        break;
    }
}

if (newGameButton == null ||
    new UnityEditor.SerializedObject(newGameButton).FindProperty("operatorSelectionUI").objectReferenceValue != selectionUI)
{
    throw new System.InvalidOperationException("New Game 버튼이 선택 UI에 연결되지 않았습니다.");
}

UnityEngine.GameObject panel = selectionSerialized.FindProperty("panel").objectReferenceValue as UnityEngine.GameObject;
if (panel == null || panel.activeSelf || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
{
    throw new System.InvalidOperationException("선택 패널의 초기 비활성 상태 또는 TitleScene 저장 상태가 올바르지 않습니다.");
}

if (!RCCom.EditorTools.OperatorAssetValidator.ValidateAll(false))
{
    throw new System.InvalidOperationException("오퍼레이터/카탈로그 검증기가 오류를 반환했습니다.");
}

UnityEngine.Debug.Log(
    $"[OperatorSelectionVerification] 카탈로그·Addressables·TitleScene 배선 검증 통과 " +
    $"({catalogEntry.operatorId}, {catalogEntry.address})");
