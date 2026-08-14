using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Card;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Tower;
using RCCom.Definitions.Unit;
using RCCom.Runtime;
using RCCom.UI;
using UnityEditor;
using UnityEngine;

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
            AllyUnitRoster unitRoster = ScriptableObject.CreateInstance<AllyUnitRoster>();
            OperatorDefinition operatorDefinition = ScriptableObject.CreateInstance<OperatorDefinition>();
            TowerRoster towerRoster = ScriptableObject.CreateInstance<TowerRoster>();
            CardRoster cardRoster = ScriptableObject.CreateInstance<CardRoster>();
            OperatorDialogueSet dialogueSet = ScriptableObject.CreateInstance<OperatorDialogueSet>();
            GameObject deployUiObject = null;

            try
            {
                unitDefinition.data = new AllyUnitData
                {
                    unitId = "verification-unit",
                    displayName = "검증 유닛",
                    maxHealth = 10f,
                    moveSpeed = 2f,
                    attackInterval = 1f,
                    attackRange = 2f,
                    detectionRange = 3f,
                };
                unitRoster.units.Add(unitDefinition);

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

                var target = new EnemyInstance();
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

                deployUiObject = new GameObject("AllyUnitDeployAvailabilityVerification");
                deployUiObject.SetActive(false);
                CanvasGroup panelGroup = deployUiObject.AddComponent<CanvasGroup>();
                UnitDeployController deployController = deployUiObject.AddComponent<UnitDeployController>();
                UnitDeployMenuUI deployMenuUI = deployUiObject.AddComponent<UnitDeployMenuUI>();

                var menuSerializedObject = new SerializedObject(deployMenuUI);
                menuSerializedObject.FindProperty("deployController").objectReferenceValue = deployController;
                menuSerializedObject.FindProperty("panelGroup").objectReferenceValue = panelGroup;
                menuSerializedObject.ApplyModifiedPropertiesWithoutUndo();

                deployMenuUI.RefreshAvailability();
                if (deployController.IsDeployInputEnabled || deployController.SelectUnit(0) ||
                    deployMenuUI.IsVisible || panelGroup.interactable || panelGroup.blocksRaycasts ||
                    panelGroup.alpha != 0f)
                {
                    throw new InvalidOperationException("null 로스터의 배치 입력 또는 UI 비활성 계약이 올바르지 않습니다.");
                }

                var controllerSerializedObject = new SerializedObject(deployController);
                controllerSerializedObject.FindProperty("allyUnitRoster").objectReferenceValue = unitRoster;
                controllerSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                deployMenuUI.RefreshAvailability();

                if (!deployController.IsDeployInputEnabled || !deployMenuUI.IsVisible ||
                    !panelGroup.interactable || !panelGroup.blocksRaycasts || panelGroup.alpha != 1f)
                {
                    throw new InvalidOperationException("유효한 로스터의 배치 입력 또는 UI 활성 계약이 올바르지 않습니다.");
                }

                Debug.Log("[AllyUnitFoundationVerifier] 역주행 스폰·상태·피해·Roster·Loadout·배치 가용성 계약 검증 통과");
            }
            finally
            {
                OperatorLoadoutSession.ClearSelection();
                UnityEngine.Object.DestroyImmediate(unitDefinition);
                UnityEngine.Object.DestroyImmediate(unitRoster);
                UnityEngine.Object.DestroyImmediate(operatorDefinition);
                UnityEngine.Object.DestroyImmediate(towerRoster);
                UnityEngine.Object.DestroyImmediate(cardRoster);
                UnityEngine.Object.DestroyImmediate(dialogueSet);

                if (deployUiObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(deployUiObject);
                }
            }
        }
    }
}
