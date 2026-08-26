var operators = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(
    "Assets/Data/Operators/OperatorCatalog.asset");
var stages = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Stage.StageCatalog>(
    "Assets/Data/Stages/StageCatalog.asset");
var resolvedOperators = RCCom.Runtime.LiveCatalogService.Resolve(operators);
var resolvedStages = RCCom.Runtime.LiveCatalogService.Resolve(stages);

if (RCCom.Runtime.LiveCatalogService.IsRunning || RCCom.Runtime.LiveCatalogService.IsResolved ||
    RCCom.Runtime.LiveCatalogService.IsActivated)
{
    throw new System.InvalidOperationException("버튼 전 LiveCatalogService가 이미 시작되었습니다.");
}

if (resolvedOperators == null || resolvedOperators.entries.Count != 4)
{
    throw new System.InvalidOperationException(
        "버튼 전 로컬 오퍼레이터 수가 4명이 아닙니다: " +
        (resolvedOperators != null ? resolvedOperators.entries.Count : -1));
}

if (resolvedStages == null || resolvedStages.entries.Count != 5)
{
    throw new System.InvalidOperationException(
        "버튼 전 로컬 스테이지 수가 5개가 아닙니다: " +
        (resolvedStages != null ? resolvedStages.entries.Count : -1));
}

UnityEngine.Debug.Log("[LiveContentVerifier] 초기 로컬 전용 상태 PASS: operator=4, stage=5, remote request=0");
