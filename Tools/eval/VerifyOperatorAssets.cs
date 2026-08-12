const string operatorFolder = "Assets/Data/Operators/cassia";
const string generatedLabel = "RCCom.GeneratedOperator";

var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>(
    $"{operatorFolder}/OperatorDefinition.asset");
var towerRoster = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Tower.TowerRoster>(
    $"{operatorFolder}/TowerRoster.asset");
var cardRoster = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Card.CardRoster>(
    $"{operatorFolder}/CardRoster.asset");

if (definition == null || towerRoster == null || cardRoster == null)
{
    throw new System.InvalidOperationException("카시아 자동 생성 에셋 3개를 모두 불러오지 못했습니다.");
}

foreach (UnityEngine.Object asset in new UnityEngine.Object[] { definition, towerRoster, cardRoster })
{
    string[] labels = UnityEditor.AssetDatabase.GetLabels(asset);
    if (System.Array.IndexOf(labels, generatedLabel) < 0)
    {
        throw new System.InvalidOperationException($"자동 생성 라벨이 없습니다: {UnityEditor.AssetDatabase.GetAssetPath(asset)}");
    }
}

var sourceTowerRoster = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Tower.TowerRoster>(
    "Assets/Data/Prefabs/TowerRoaster.asset");
var sourceCardRoster = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Card.CardRoster>(
    "Assets/Data/Cards/Unlock/플레이어 카드 로스터.asset");

if (definition.operatorId != "cassia" || definition.towerRoster != towerRoster ||
    definition.cardRoster != cardRoster || definition.dialogueSet == null ||
    definition.selectionPortrait == null || definition.playerData == null)
{
    throw new System.InvalidOperationException("카시아 OperatorDefinition의 필수 참조가 올바르지 않습니다.");
}

if (towerRoster.towers.Count != sourceTowerRoster.towers.Count ||
    cardRoster.cards.Count != sourceCardRoster.cards.Count)
{
    throw new System.InvalidOperationException("카시아 전용 Roster의 항목 수가 원본과 일치하지 않습니다.");
}

UnityEngine.Debug.Log(
    $"[OperatorAssetVerification] 카시아 생성 에셋 검증 통과 " +
    $"(타워 {towerRoster.towers.Count}, 카드 {cardRoster.cards.Count})");
