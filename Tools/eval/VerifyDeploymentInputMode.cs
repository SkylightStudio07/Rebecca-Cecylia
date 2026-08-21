RCCom.Runtime.DeploymentInputModeController[] modeControllers =
    UnityEngine.Object.FindObjectsByType<RCCom.Runtime.DeploymentInputModeController>(
        UnityEngine.FindObjectsInactive.Include,
        UnityEngine.FindObjectsSortMode.None);
RCCom.Runtime.TowerBuildController towerController =
    UnityEngine.Object.FindFirstObjectByType<RCCom.Runtime.TowerBuildController>(
        UnityEngine.FindObjectsInactive.Include);
RCCom.Runtime.UnitDeployController unitController =
    UnityEngine.Object.FindFirstObjectByType<RCCom.Runtime.UnitDeployController>(
        UnityEngine.FindObjectsInactive.Include);

if (modeControllers.Length != 1 || towerController == null || unitController == null)
{
    throw new System.InvalidOperationException(
        $"입력 모드 런타임 인스턴스가 올바르지 않습니다. " +
        $"Mode={modeControllers.Length}, Tower={(towerController != null)}, Unit={(unitController != null)}");
}

var towerSerialized = new UnityEditor.SerializedObject(towerController);
var unitSerialized = new UnityEditor.SerializedObject(unitController);
UnityEngine.Object towerMode = towerSerialized.FindProperty("inputModeController").objectReferenceValue;
UnityEngine.Object unitMode = unitSerialized.FindProperty("inputModeController").objectReferenceValue;
if (towerMode != modeControllers[0] || unitMode != modeControllers[0])
{
    throw new System.InvalidOperationException("타워와 유닛 Controller가 같은 입력 모드를 공유하지 않습니다.");
}

float originalTimeScale = UnityEngine.Time.timeScale;
try
{
    UnityEngine.Time.timeScale = 1f;
    towerController.SelectTower(0);
    if (modeControllers[0].CurrentMode != RCCom.Runtime.DeploymentInputMode.TowerBuild ||
        towerController.SelectedDefinition == null)
    {
        throw new System.InvalidOperationException("타워 설치 모드 진입에 실패했습니다.");
    }

    if (!unitController.SelectUnit(0) ||
        modeControllers[0].CurrentMode != RCCom.Runtime.DeploymentInputMode.AllyUnitDeploy ||
        towerController.SelectedDefinition != null || unitController.SelectedDefinition == null)
    {
        throw new System.InvalidOperationException("유닛 배치 모드 전환 또는 타워 선택 해제에 실패했습니다.");
    }

    towerController.SelectTower(0);
    if (modeControllers[0].CurrentMode != RCCom.Runtime.DeploymentInputMode.TowerBuild ||
        towerController.SelectedDefinition == null || unitController.SelectedDefinition != null)
    {
        throw new System.InvalidOperationException("타워 설치 모드 전환 또는 유닛 선택 해제에 실패했습니다.");
    }
}
finally
{
    UnityEngine.Time.timeScale = originalTimeScale;
}

string result =
    $"ModeControllers={modeControllers.Length}, Shared=True, Current={modeControllers[0].CurrentMode}";
UnityEngine.Debug.Log($"[DeploymentInputModeVerification] {result}");
return result;
