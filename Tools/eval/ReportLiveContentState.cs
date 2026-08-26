var operators = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(
    "Assets/Data/Operators/OperatorCatalog.asset");
var stages = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Stage.StageCatalog>(
    "Assets/Data/Stages/StageCatalog.asset");
var resolvedOperators = RCCom.Runtime.LiveCatalogService.Resolve(operators);
var resolvedStages = RCCom.Runtime.LiveCatalogService.Resolve(stages);
UnityEngine.Debug.Log(
    "[LiveContentVerifier] resolved=" + RCCom.Runtime.LiveCatalogService.IsResolved +
    ", activated=" + RCCom.Runtime.LiveCatalogService.IsActivated +
    ", running=" + RCCom.Runtime.LiveCatalogService.IsRunning +
    ", operator=" + (resolvedOperators != null ? resolvedOperators.entries.Count : -1) +
    ", stage=" + (resolvedStages != null ? resolvedStages.entries.Count : -1) +
    ", failure=" + RCCom.Runtime.LiveCatalogService.FailureMessage);
