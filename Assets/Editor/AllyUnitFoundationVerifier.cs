using System;
using System.Collections.Generic;
using System.Reflection;
using RCCom.Data;
using RCCom.Definitions.Card;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Tower;
using RCCom.Definitions.Unit;
using RCCom.Runtime;
using RCCom.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// asmdef 테스트 구조를 도입하지 않고도 아군 유닛 공통 계약의 핵심 불변식을 검증한다.
    /// 임시 SO는 메모리에만 만들고 즉시 파괴하므로 프로젝트 에셋을 변경하지 않는다.
    /// </summary>
    public static class AllyUnitFoundationVerifier
    {
        [MenuItem("RCCom/Ally Units/Verify Foundation Contract")]
        public static void Verify()
        {
            AllyUnitDefinition unitDefinition = ScriptableObject.CreateInstance<AllyUnitDefinition>();
            AttackTowerDefinition towerDefinition = ScriptableObject.CreateInstance<AttackTowerDefinition>();
            EnemyDefinition enemyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
            AllyUnitRoster unitRoster = ScriptableObject.CreateInstance<AllyUnitRoster>();
            OperatorDefinition operatorDefinition = ScriptableObject.CreateInstance<OperatorDefinition>();
            TowerRoster towerRoster = ScriptableObject.CreateInstance<TowerRoster>();
            CardRoster cardRoster = ScriptableObject.CreateInstance<CardRoster>();
            OperatorDialogueSet dialogueSet = ScriptableObject.CreateInstance<OperatorDialogueSet>();
            GameObject deployUiObject = null;
            GameObject inputModeObject = null;
            GameObject towerControllerObject = null;
            GameObject waveObject = null;
            float originalTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = 1f;
                unitDefinition.data = new AllyUnitData
                {
                    unitId = "verification-unit",
                    displayName = "검증 유닛",
                    deployCost = 3,
                    maxHealth = 10f,
                    moveSpeed = 2f,
                    attackInterval = 1f,
                    attackRange = 2f,
                    detectionRange = 3f,
                };
                enemyDefinition.data = new EnemyData
                {
                    enemyId = "verification-enemy",
                    displayName = "검증 적",
                    maxHealth = 10f,
                    moveSpeed = 0f,
                    attackRange = 1f,
                    attackInterval = 1f,
                };
                unitRoster.units.Add(unitDefinition);
                towerDefinition.data.displayName = "검증 타워";
                towerRoster.towers.Add(towerDefinition);

                var path = new List<Vector2>
                {
                    new(0f, 0f),
                    new(5f, 0f),
                    new(10f, 0f),
                };
                var instance = new AllyUnitInstance();
                instance.Spawn(unitDefinition, path);

                if (instance.Position != path[2] || instance.CurrentTargetWaypoint != path[1] ||
                    instance.State != AllyUnitState.Advancing || instance.CurrentHealth != 10f)
                {
                    throw new InvalidOperationException("역방향 스폰 또는 초기 상태 계약이 올바르지 않습니다.");
                }

                var target = new EnemyInstance
                {
                    definition = enemyDefinition,
                    position = path[2],
                };
                target.Spawn(path, null);
                target.position = path[2];
                instance.SetEngagementTarget(target);
                if (instance.State != AllyUnitState.Engaging || instance.CurrentTarget != target)
                {
                    throw new InvalidOperationException("교전 상태 전이 계약이 올바르지 않습니다.");
                }

                instance.SetEngagementTarget(null);
                if (instance.State != AllyUnitState.Advancing || instance.CurrentTarget != null)
                {
                    throw new InvalidOperationException("진격 재개 상태 전이 계약이 올바르지 않습니다.");
                }

                bool died = false;
                instance.Died += () => died = true;
                instance.TakeDamage(10f);
                if (!died || !instance.IsDead || instance.CurrentHealth != 0f)
                {
                    throw new InvalidOperationException("피해·사망 계약이 올바르지 않습니다.");
                }

                if (unitRoster.FindById("verification-unit") != unitDefinition)
                {
                    throw new InvalidOperationException("AllyUnitRoster ID 조회 계약이 올바르지 않습니다.");
                }

                operatorDefinition.operatorId = "verification-operator";
                operatorDefinition.towerRoster = towerRoster;
                operatorDefinition.cardRoster = cardRoster;
                operatorDefinition.allyUnitRoster = unitRoster;
                operatorDefinition.dialogueSet = dialogueSet;
                OperatorLoadoutSession.Select(operatorDefinition);
                if (OperatorLoadoutSession.ResolveAllyUnitRoster() != unitRoster)
                {
                    throw new InvalidOperationException("오퍼레이터 유닛 로스터 해석 계약이 올바르지 않습니다.");
                }

                OperatorLoadoutSession.ClearSelection();

                inputModeObject = new GameObject("DeploymentInputModeVerification");
                DeploymentInputModeController inputModeController =
                    inputModeObject.AddComponent<DeploymentInputModeController>();

                towerControllerObject = new GameObject("TowerBuildInputModeVerification");
                towerControllerObject.SetActive(false);
                TowerBuildController towerBuildController = towerControllerObject.AddComponent<TowerBuildController>();
                var towerControllerSerializedObject = new SerializedObject(towerBuildController);
                towerControllerSerializedObject.FindProperty("towerRoster").objectReferenceValue = towerRoster;
                towerControllerSerializedObject.FindProperty("inputModeController").objectReferenceValue = inputModeController;
                towerControllerSerializedObject.ApplyModifiedPropertiesWithoutUndo();

                deployUiObject = new GameObject("AllyUnitDeployAvailabilityVerification");
                deployUiObject.SetActive(false);
                CanvasGroup panelGroup = deployUiObject.AddComponent<CanvasGroup>();
                UnitDeployController deployController = deployUiObject.AddComponent<UnitDeployController>();
                UnitDeployMenuUI deployMenuUI = deployUiObject.AddComponent<UnitDeployMenuUI>();
                var contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.transform.SetParent(deployUiObject.transform);

                var buttonTemplateObject = new GameObject("UnitDeployButtonTemplate", typeof(RectTransform));
                buttonTemplateObject.transform.SetParent(deployUiObject.transform);
                Image buttonIcon = buttonTemplateObject.AddComponent<Image>();
                Button buttonComponent = buttonTemplateObject.AddComponent<Button>();
                UnitDeployButton buttonTemplate = buttonTemplateObject.AddComponent<UnitDeployButton>();

                var nameObject = new GameObject("Name", typeof(RectTransform));
                nameObject.transform.SetParent(buttonTemplateObject.transform);
                var nameText = nameObject.AddComponent<TMPro.TextMeshProUGUI>();

                var costObject = new GameObject("Cost", typeof(RectTransform));
                costObject.transform.SetParent(buttonTemplateObject.transform);
                var costText = costObject.AddComponent<TMPro.TextMeshProUGUI>();

                var selectionIndicator = new GameObject("SelectionIndicator", typeof(RectTransform));
                selectionIndicator.transform.SetParent(buttonTemplateObject.transform);
                selectionIndicator.SetActive(false);

                var buttonSerializedObject = new SerializedObject(buttonTemplate);
                buttonSerializedObject.FindProperty("icon").objectReferenceValue = buttonIcon;
                buttonSerializedObject.FindProperty("nameText").objectReferenceValue = nameText;
                buttonSerializedObject.FindProperty("costText").objectReferenceValue = costText;
                buttonSerializedObject.FindProperty("button").objectReferenceValue = buttonComponent;
                buttonSerializedObject.FindProperty("selectionIndicator").objectReferenceValue = selectionIndicator;
                buttonSerializedObject.ApplyModifiedPropertiesWithoutUndo();

                var menuSerializedObject = new SerializedObject(deployMenuUI);
                menuSerializedObject.FindProperty("deployController").objectReferenceValue = deployController;
                menuSerializedObject.FindProperty("panelGroup").objectReferenceValue = panelGroup;
                menuSerializedObject.FindProperty("contentParent").objectReferenceValue = contentObject.transform;
                menuSerializedObject.FindProperty("buttonPrefab").objectReferenceValue = buttonTemplate;
                menuSerializedObject.ApplyModifiedPropertiesWithoutUndo();

                var controllerSerializedObject = new SerializedObject(deployController);
                controllerSerializedObject.FindProperty("inputModeController").objectReferenceValue = inputModeController;
                controllerSerializedObject.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo registerInstance = typeof(UnitDeployController).GetMethod(
                    "RegisterInstance",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (registerInstance == null)
                {
                    throw new InvalidOperationException("아군 유닛 활성 목록 등록 진입점을 찾지 못했습니다.");
                }

                var trackedInstance = new AllyUnitInstance();
                trackedInstance.Spawn(unitDefinition, path);
                int removedEventCount = 0;
                AllyUnitInstance removedInstance = null;
                deployController.UnitRemoved += removed =>
                {
                    removedEventCount++;
                    removedInstance = removed;
                };

                registerInstance.Invoke(deployController, new object[] { trackedInstance });
                if (deployController.ActiveUnits.Count != 1 || deployController.ActiveUnits[0] != trackedInstance)
                {
                    throw new InvalidOperationException("소환 유닛의 활성 목록 등록 계약이 올바르지 않습니다.");
                }

                // Tick 도중 사망해도 다음 프레임까지 죽은 참조가 남지 않아야 타깃 후보와
                // 아군 목록을 받는 다른 Instance가 이미 죽은 유닛을 다시 선택하지 않는다.
                trackedInstance.TakeDamage(trackedInstance.CurrentHealth);
                if (deployController.ActiveUnits.Count != 0 || removedEventCount != 1 ||
                    removedInstance != trackedInstance)
                {
                    throw new InvalidOperationException("사망 유닛의 활성 목록 제거 계약이 올바르지 않습니다.");
                }

                // Edit Mode의 비활성 임시 오브젝트는 생명주기 콜백이 자동 실행되지 않으므로,
                // 실제 UI와 같은 OnEnable 구독 경로를 명시적으로 통과시킨다.
                MethodInfo menuOnEnable = typeof(UnitDeployMenuUI).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (menuOnEnable == null)
                {
                    throw new InvalidOperationException("유닛 배치 UI의 이벤트 구독 진입점을 찾지 못했습니다.");
                }

                menuOnEnable.Invoke(deployMenuUI, null);
                InvokeLifecycleMethod(deployController, "OnEnable");
                InvokeLifecycleMethod(towerBuildController, "OnEnable");
                deployMenuUI.RefreshAvailability();
                if (deployController.IsDeployInputEnabled || deployController.SelectUnit(0) ||
                    deployMenuUI.IsVisible || panelGroup.interactable || panelGroup.blocksRaycasts ||
                    panelGroup.alpha != 0f)
                {
                    throw new InvalidOperationException("null 로스터의 배치 입력 또는 UI 비활성 계약이 올바르지 않습니다.");
                }

                controllerSerializedObject.FindProperty("allyUnitRoster").objectReferenceValue = unitRoster;
                controllerSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                deployMenuUI.RefreshAvailability();

                if (!deployController.IsDeployInputEnabled || !deployMenuUI.IsVisible ||
                    !panelGroup.interactable || !panelGroup.blocksRaycasts || panelGroup.alpha != 1f)
                {
                    throw new InvalidOperationException("유효한 로스터의 배치 입력 또는 UI 활성 계약이 올바르지 않습니다.");
                }

                AllyUnitDefinition selectedDefinition = null;
                deployController.SelectionChanged += definition => selectedDefinition = definition;
                deployMenuUI.Rebuild();
                UnitDeployButton generatedButton = contentObject.GetComponentInChildren<UnitDeployButton>(true);
                if (deployMenuUI.ButtonCount != 1 || generatedButton == null ||
                    generatedButton.Definition != unitDefinition)
                {
                    throw new InvalidOperationException("AllyUnitRoster 기반 동적 버튼 생성 계약이 올바르지 않습니다.");
                }

                string generatedName = generatedButton.transform.Find("Name").GetComponent<TMPro.TextMeshProUGUI>().text;
                string generatedCost = generatedButton.transform.Find("Cost").GetComponent<TMPro.TextMeshProUGUI>().text;
                if (generatedName != unitDefinition.data.displayName || generatedCost != "3 CP")
                {
                    throw new InvalidOperationException("유닛 Definition의 이름 또는 배치 비용 표시 계약이 올바르지 않습니다.");
                }

                Button generatedButtonComponent = generatedButton.GetComponent<Button>();
                if (generatedButtonComponent.interactable)
                {
                    throw new InvalidOperationException("지휘 포인트 부족 시 유닛 버튼 비활성 계약이 올바르지 않습니다.");
                }

                int changedCommandPoints = -1;
                deployController.CommandPointsChanged += points => changedCommandPoints = points;
                deployController.AddCommandPoints(5);
                if (deployController.CommandPoints != 5 || changedCommandPoints != 5 ||
                    !deployController.CanAfford(unitDefinition) || !generatedButtonComponent.interactable)
                {
                    throw new InvalidOperationException(
                        $"지휘 포인트 충족 시 유닛 버튼 활성 계약이 올바르지 않습니다. " +
                        $"CP={deployController.CommandPoints}, Event={changedCommandPoints}, " +
                        $"CanAfford={deployController.CanAfford(unitDefinition)}, " +
                        $"Interactable={generatedButtonComponent.interactable}");
                }

                generatedButtonComponent.onClick.Invoke();
                if (deployController.SelectedDefinition != unitDefinition || selectedDefinition != unitDefinition ||
                    inputModeController.CurrentMode != DeploymentInputMode.AllyUnitDeploy)
                {
                    throw new InvalidOperationException("동적 유닛 버튼의 Definition 선택 계약이 올바르지 않습니다.");
                }

                towerBuildController.SelectTower(0);
                if (inputModeController.CurrentMode != DeploymentInputMode.TowerBuild ||
                    towerBuildController.SelectedDefinition != towerDefinition ||
                    deployController.SelectedDefinition != null || selectedDefinition != null)
                {
                    throw new InvalidOperationException("타워 설치 모드 전환 시 유닛 선택 해제 계약이 올바르지 않습니다.");
                }

                generatedButtonComponent.onClick.Invoke();
                if (inputModeController.CurrentMode != DeploymentInputMode.AllyUnitDeploy ||
                    deployController.SelectedDefinition != unitDefinition ||
                    towerBuildController.SelectedDefinition != null)
                {
                    throw new InvalidOperationException("유닛 배치 모드 전환 시 타워 선택 해제 계약이 올바르지 않습니다.");
                }

                generatedButton.SetSelected(true);
                if (!generatedButton.transform.Find("SelectionIndicator").gameObject.activeSelf)
                {
                    throw new InvalidOperationException("유닛 버튼의 선택 표시 계약이 올바르지 않습니다.");
                }

                deployController.ClearSelection();
                if (deployController.SelectedDefinition != null || selectedDefinition != null ||
                    inputModeController.CurrentMode != DeploymentInputMode.None)
                {
                    throw new InvalidOperationException("유닛 Definition 선택 해제 계약이 올바르지 않습니다.");
                }

                if (!deployController.TrySpendCommandPoints(unitDefinition.data.deployCost) ||
                    deployController.CommandPoints != 2 || changedCommandPoints != 2 ||
                    generatedButtonComponent.interactable)
                {
                    throw new InvalidOperationException("지휘 포인트 소비 또는 부족 전환 계약이 올바르지 않습니다.");
                }

                if (deployController.TrySpendCommandPoints(unitDefinition.data.deployCost) ||
                    deployController.CommandPoints != 2 || changedCommandPoints != 2)
                {
                    throw new InvalidOperationException("지휘 포인트 부족 시 소비 거부 계약이 올바르지 않습니다.");
                }

                waveObject = new GameObject("AllyUnitCommandPointRecoveryVerification");
                waveObject.SetActive(false);
                var waveManager = waveObject.AddComponent<RCCom.Managers.WaveManager>();
                controllerSerializedObject.FindProperty("waveManager").objectReferenceValue = waveManager;
                controllerSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                deployController.AddCommandPoints(93);

                MethodInfo tickBeforeEnemies = typeof(UnitDeployController).GetMethod(
                    "HandleBeforeEnemiesTick",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (tickBeforeEnemies == null)
                {
                    throw new InvalidOperationException("지휘 포인트 회복 Tick 진입점을 찾지 못했습니다.");
                }

                tickBeforeEnemies.Invoke(deployController, new object[] { 0.5f });
                tickBeforeEnemies.Invoke(deployController, new object[] { 0.25f });
                if (deployController.CommandPoints != 98)
                {
                    throw new InvalidOperationException("전투 중 지휘 포인트 회복 계약이 올바르지 않습니다.");
                }

                Time.timeScale = 0f;
                tickBeforeEnemies.Invoke(deployController, new object[] { 1f });
                if (deployController.IsDeployInputEnabled || deployController.SelectUnit(0) ||
                    deployController.CommandPoints != 98)
                {
                    throw new InvalidOperationException("일시정지 중 배치 입력 또는 지휘 포인트 회복 차단 계약이 올바르지 않습니다.");
                }

                Time.timeScale = 1f;

                var waveSerializedObject = new SerializedObject(waveManager);
                waveSerializedObject.FindProperty("isWaitingForNextWave").boolValue = true;
                waveSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                tickBeforeEnemies.Invoke(deployController, new object[] { 1f });
                if (deployController.CommandPoints != 98)
                {
                    throw new InvalidOperationException("빌드 페이즈 지휘 포인트 회복 차단 계약이 올바르지 않습니다.");
                }

                deployController.AddCommandPoints(99);
                if (deployController.CommandPoints != deployController.MaxCommandPoints ||
                    deployController.MaxCommandPoints != 100)
                {
                    throw new InvalidOperationException("지휘 포인트 상한 계약이 올바르지 않습니다.");
                }

                Debug.Log("[AllyUnitFoundationVerifier] 역주행 스폰·상태·피해·사망 활성 목록 정리·Roster·Loadout·상호 배타 배치 입력 모드·Definition 선택·비용별 버튼 상태·지휘 포인트 소비·회복·일시정지·상한 계약 검증 통과");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                OperatorLoadoutSession.ClearSelection();
                UnityEngine.Object.DestroyImmediate(unitDefinition);
                UnityEngine.Object.DestroyImmediate(towerDefinition);
                UnityEngine.Object.DestroyImmediate(enemyDefinition);
                UnityEngine.Object.DestroyImmediate(unitRoster);
                UnityEngine.Object.DestroyImmediate(operatorDefinition);
                UnityEngine.Object.DestroyImmediate(towerRoster);
                UnityEngine.Object.DestroyImmediate(cardRoster);
                UnityEngine.Object.DestroyImmediate(dialogueSet);

                if (deployUiObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(deployUiObject);
                }

                if (towerControllerObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(towerControllerObject);
                }

                if (inputModeObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(inputModeObject);
                }

                if (waveObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(waveObject);
                }
            }
        }

        private static void InvokeLifecycleMethod(MonoBehaviour target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name}.{methodName} 진입점을 찾지 못했습니다.");
            }

            method.Invoke(target, null);
        }
    }
}
