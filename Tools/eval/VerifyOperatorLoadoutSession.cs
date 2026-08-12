const string definitionPath = "Assets/Data/Operators/cassia/OperatorDefinition.asset";

var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>(
    definitionPath);
if (definition == null)
{
    throw new System.InvalidOperationException($"오퍼레이터 Definition을 불러오지 못했습니다: {definitionPath}");
}

RCCom.Runtime.OperatorLoadoutSession.Select(definition);

if (RCCom.Runtime.OperatorLoadoutSession.SelectedDefinition != definition ||
    RCCom.Runtime.OperatorLoadoutSession.ResolveTowerRoster(null) != definition.towerRoster ||
    RCCom.Runtime.OperatorLoadoutSession.ResolveCardRoster(null) != definition.cardRoster ||
    RCCom.Runtime.OperatorLoadoutSession.ResolveDialogueSet(null) != definition.dialogueSet)
{
    throw new System.InvalidOperationException("선택된 오퍼레이터의 참조가 동일한 로드아웃으로 해석되지 않았습니다.");
}

RCCom.Data.PlayerData runtimeData = RCCom.Runtime.OperatorLoadoutSession.CreatePlayerData(null);
if (object.ReferenceEquals(runtimeData, definition.playerData) ||
    runtimeData.maxHealth != definition.playerData.maxHealth ||
    runtimeData.attackDamage != definition.playerData.attackDamage ||
    runtimeData.attackRange != definition.playerData.attackRange)
{
    throw new System.InvalidOperationException("플레이어 데이터의 세션 복제가 올바르지 않습니다.");
}

float originalAttackDamage = definition.playerData.attackDamage;
runtimeData.attackDamage += 123f;
if (definition.playerData.attackDamage != originalAttackDamage)
{
    throw new System.InvalidOperationException("런타임 플레이어 데이터 수정이 OperatorDefinition 원본을 오염시켰습니다.");
}

RCCom.Runtime.OperatorLoadoutSession.ClearSelection();
if (RCCom.Runtime.OperatorLoadoutSession.SelectedDefinition != null)
{
    throw new System.InvalidOperationException("오퍼레이터 선택 초기화가 실패했습니다.");
}

UnityEngine.Debug.Log(
    $"[OperatorLoadoutVerification] 카시아 런타임 로드아웃 검증 통과 " +
    $"(체력 {runtimeData.maxHealth}, 공격 범위 {runtimeData.attackRange})");
