using System;
using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Unit;
using RCCom.Effects.Enemy.Concrete;
using RCCom.Effects.Unit.Concrete;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 아군·적 교전 코어를 메모리 임시 SO만으로 검증한다. 씬·프리팹·프로젝트 에셋을
    /// 읽거나 수정하지 않아, 데이터 배선 전에도 이동·전투 불변식을 반복 확인할 수 있다.
    /// </summary>
    public static class AllyUnitCombatVerifier
    {
        [MenuItem("RCCom/Ally Units/Verify Combat Core")]
        public static void Verify()
        {
            var temporaryObjects = new List<UnityEngine.Object>();

            try
            {
                VerifySettingsAndEndpointSpawn(temporaryObjects);
                VerifyContinuousReverseProgress(temporaryObjects);
                VerifyProgressUsesCurrentWaypointSegment(temporaryObjects);
                VerifyFinalWaitPointAcrossShortFirstSegment(temporaryObjects);
                VerifyAttackWhileAdvancing(temporaryObjects);
                VerifyContactRangeStopsBothSides(temporaryObjects);
                VerifyContactBoundaryOnLargeStep(temporaryObjects);
                VerifySetEngagementTargetUsesContactRange(temporaryObjects);
                VerifyAlliesFormSpawnOrderedLine(temporaryObjects);
                VerifyBaseEndpointOverlapAndOrderedResume(temporaryObjects);
                VerifyImmediateAttackAndCooldown(temporaryObjects);
                VerifyAllyAdvancesAfterEnemyDeath(temporaryObjects);
                VerifyEnemyAdvancesAfterAllyDeath(temporaryObjects);
                VerifyAlliesFocusEnemyFrontline(temporaryObjects);
                VerifyEnemiesFocusAllyFrontline(temporaryObjects);
                VerifyDeadTargetIsNotAttackedAgain(temporaryObjects);
                VerifyTargetReleaseOnRangeExit(temporaryObjects);
                VerifyEnemyMovesWithoutEngagement(temporaryObjects);
                VerifyGoalDamageAndReachedGoalOnce(temporaryObjects);
                VerifyBasicAttackDamage(temporaryObjects);
                VerifyEnemyContactDamageEffectPath(temporaryObjects);

                Debug.Log("[AllyUnitCombatVerifier] 아군 유닛 전투 코어 21개 시나리오 검증 통과");
            }
            finally
            {
                for (int i = temporaryObjects.Count - 1; i >= 0; i--)
                {
                    if (temporaryObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
                    }
                }
            }
        }

        private static void VerifySettingsAndEndpointSpawn(List<UnityEngine.Object> temporaryObjects)
        {
            UnitCombatSettings settings = CreateSettings(temporaryObjects, 0f, 0f);
            AllyUnitDefinition definition = CreateAlly(temporaryObjects, 10f, 1f, 2f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(5f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();

            unit.Spawn(definition, path, settings);

            AssertNear(unit.ContactRange, 0.75f, "설정 없는 잘못된 contactRange 안전 기본값");
            AssertNear(unit.SeparationMargin, 0.05f, "설정 없는 잘못된 separationMargin 안전 기본값");
            Assert(unit.Position == path[2], "아군이 경로 끝점에서 시작하지 않습니다.");
            Assert(unit.CurrentTargetWaypoint == path[1], "끝점 스폰 후 역방향 웨이포인트가 아닙니다.");
            AssertNear(unit.PathProgress, 1f, "끝점 진행도가 1이 아닙니다.");
        }

        private static void VerifyContinuousReverseProgress(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition definition = CreateAlly(temporaryObjects, 10f, 1f, 2f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(5f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(definition, path);
            unit.Tick(1f, Array.Empty<EnemyInstance>(), new[] { unit });

            AssertNear(unit.Position.x, 9f, "아군 역방향 이동량이 moveSpeed와 다릅니다.");
            AssertNear(unit.PathProgress, 0.9f, "선분 내부 이동량이 연속 진행도에 반영되지 않았습니다.");
        }

        private static void VerifyProgressUsesCurrentWaypointSegment(List<UnityEngine.Object> temporaryObjects)
        {
            var path = new List<Vector2>
            {
                new(-2f, 0f),
                new(2f, 0f),
                new(0f, 0f),
                new(2f, 0f),
            };
            Vector2 sharedPosition = new(0f, 0f);

            float firstForwardProgress = AllyUnitTargeting.CalculatePathProgress(
                path,
                1,
                sharedPosition,
                true);
            float secondForwardProgress = AllyUnitTargeting.CalculatePathProgress(
                path,
                2,
                sharedPosition,
                true);
            float firstReverseProgress = AllyUnitTargeting.CalculatePathProgress(
                path,
                0,
                sharedPosition,
                false);
            float secondReverseProgress = AllyUnitTargeting.CalculatePathProgress(
                path,
                1,
                sharedPosition,
                false);

            AssertNear(firstForwardProgress, 0.25f,
                "교차·되감기 경로의 첫 번째 실제 구간 진행도가 잘못되었습니다.");
            AssertNear(secondForwardProgress, 0.75f,
                "전진 적 진행도가 실제 nextWaypointIndex 구간을 따르지 않습니다.");
            AssertNear(firstReverseProgress, 0.25f,
                "교차·되감기 경로의 아군 첫 번째 실제 구간 진행도가 잘못되었습니다.");
            AssertNear(secondReverseProgress, 0.75f,
                "역주행 아군 진행도가 실제 nextWaypointIndex 구간을 따르지 않습니다.");
            Assert(secondForwardProgress > firstForwardProgress && secondReverseProgress > firstReverseProgress,
                "같은 좌표의 서로 다른 실제 구간이 같은 진행도로 계산되었습니다.");
        }

        private static void VerifyFinalWaitPointAcrossShortFirstSegment(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition definition = CreateAlly(temporaryObjects, 10f, 10f, 2f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(0.2f, 0f), new(5f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(definition, path);
            unit.Tick(1f, Array.Empty<EnemyInstance>(), new[] { unit });

            AssertNear(unit.Position.x, 0.8f, "첫 선분이 짧을 때 최종 대기점 계산이 끊겼습니다.");
            Assert(unit.Position != path[0], "아군이 경로 시작점까지 직접 진입했습니다.");
            Assert(unit.CurrentTargetWaypoint == path[0], "최종 대기점에서 방향을 계산할 목표가 없습니다.");
        }

        private static void VerifyAttackWhileAdvancing(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 1f, 1f, 3f, true);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));

            unit.Tick(0.1f, new[] { enemy }, new[] { unit });

            AssertNear(enemy.currentHealth, 99f, "공격 범위 안에서 이동 중 첫 공격이 발생하지 않았습니다.");
            Assert(unit.State == AllyUnitState.Advancing, "접촉 거리 밖 원거리 공격 중 이동이 멈췄습니다.");
            Assert(unit.Position.x < 10f, "공격 범위 안의 아군이 진격하지 않았습니다.");
        }

        private static void VerifyContactRangeStopsBothSides(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 1f, 1f, 3f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 1f, 0f, 1f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(9.5f, 0f));
            float unitStart = unit.Position.x;
            float enemyStart = enemy.position.x;

            unit.Tick(1f, new[] { enemy }, new[] { unit });
            enemy.Tick(1f);

            Assert(unit.State == AllyUnitState.Engaging, "contactRange 진입 후 아군이 Engaging이 아닙니다.");
            AssertNear(unit.Position.x, unitStart, "접촉 거리 안에서 아군이 이동했습니다.");
            AssertNear(enemy.position.x, enemyStart, "접촉 거리 안에서 적이 이동했습니다.");
        }

        private static void VerifyContactBoundaryOnLargeStep(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 100f, 10f, 0f, 3f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 3f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var ally = new AllyUnitInstance();
            ally.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));

            ally.Tick(1f, new[] { enemy }, new[] { ally });

            AssertNear(ally.Position.x, 8.75f, "아군이 큰 프레임에도 접촉 경계를 넘어가지 않았습니다.");
            AssertNear(Vector2.Distance(ally.Position, enemy.position), ally.ContactRange,
                "아군이 접촉 경계에서 정확한 거리를 유지하지 않았습니다.");
            Assert(ally.State == AllyUnitState.Engaging, "아군이 접촉 범위 진입 프레임에 Engaging으로 전환되지 않았습니다.");

            AllyUnitDefinition movingAllyDefinition = CreateAlly(temporaryObjects, 100f, 2f, 0f, 3f, false);
            EnemyDefinition advancingEnemyDefinition = CreateEnemy(temporaryObjects, 100f, 10f, 0f, 3f, 1f, false);
            var enemyPath = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var movingAllyPath = new List<Vector2> { new(0f, 0f), new(11f, 0f) };
            var movingAlly = new AllyUnitInstance();
            movingAlly.Spawn(movingAllyDefinition, movingAllyPath);
            EnemyInstance advancingEnemy = SpawnEnemy(advancingEnemyDefinition, enemyPath, enemyPath[0]);

            // 적의 현재 구간을 확정한 뒤 아군이 먼저 이동한다. 후보를 이동 전에 제시하면
            // 아군의 옛 위치가 sweep 밖이라 적이 같은 프레임에 전열을 관통할 수 있다.
            advancingEnemy.Tick(0f);
            Assert(Vector2.Distance(advancingEnemy.position, movingAlly.Position) >
                   advancingEnemy.Data.attackRange,
                "최초 조우 회귀 조건의 초기 거리가 attackRange보다 크지 않습니다.");
            movingAlly.Tick(1f, new[] { advancingEnemy }, new[] { movingAlly });
            AssertNear(movingAlly.Position.x, 9f,
                "고속 조우 검증에서 아군의 선행 이동 위치가 잘못되었습니다.");
            Assert(advancingEnemy.CurrentTarget == null,
                "attackRange 밖의 아군이 공격 타깃으로 잘못 등록되었습니다.");
            advancingEnemy.Tick(1f);

            AssertNear(advancingEnemy.position.x, 8.25f,
                "attackRange 밖 최초 조우에서 적이 contactRange 경계를 관통했습니다.");
            AssertNear(Vector2.Distance(advancingEnemy.position, movingAlly.Position), movingAlly.ContactRange,
                "attackRange 밖 최초 조우에서 적이 contactRange 경계에 정지하지 않았습니다.");
        }

        private static void VerifySetEngagementTargetUsesContactRange(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 100f, 0f, 0f, 5f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 5f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var ally = new AllyUnitInstance();
            ally.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));

            ally.SetEngagementTarget(enemy);
            Assert(ally.CurrentTarget == enemy && ally.State == AllyUnitState.Advancing,
                "공격 대상이 접촉 거리 밖인데 SetEngagementTarget이 이동을 멈췄습니다.");

            enemy.position = new Vector2(9.5f, 0f);
            ally.SetEngagementTarget(enemy);
            Assert(ally.State == AllyUnitState.Engaging,
                "접촉 거리 안의 SetEngagementTarget이 Engaging으로 전환되지 않았습니다.");

            ally.SetEngagementTarget(null);
            Assert(ally.CurrentTarget == null && ally.State == AllyUnitState.Advancing,
                "SetEngagementTarget(null)이 Advancing으로 돌아가지 않았습니다.");
        }

        private static void VerifyAlliesFormSpawnOrderedLine(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 100f, 10f, 0f, 1f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 1f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(20f, 0f) };
            var first = new AllyUnitInstance();
            var second = new AllyUnitInstance();
            var third = new AllyUnitInstance();
            first.Spawn(allyDefinition, path);
            second.Spawn(allyDefinition, path);
            third.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(14.25f, 0f));
            var allies = new[] { first, second, third };

            first.Tick(1f, new[] { enemy }, allies);
            second.Tick(1f, new[] { enemy }, allies);
            third.Tick(1f, new[] { enemy }, allies);

            AssertNear(first.Position.x, 15f,
                "선두 아군이 적 contactRange 경계에 정지하지 않았습니다.");
            AssertNear(second.Position.x - first.Position.x, first.ContactRange,
                "두 번째 아군이 선두와 contactRange 간격을 만들지 않았습니다.");
            AssertNear(third.Position.x - second.Position.x, second.ContactRange,
                "세 번째 아군이 생성 순서 대열을 만들지 않았습니다.");
        }

        private static void VerifyBaseEndpointOverlapAndOrderedResume(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 100f, 1f, 0f, 1f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 1f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(20f, 0f) };
            var first = new AllyUnitInstance();
            var second = new AllyUnitInstance();
            var third = new AllyUnitInstance();
            first.Spawn(allyDefinition, path);
            second.Spawn(allyDefinition, path);
            third.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(19.25f, 0f));
            var enemies = new[] { enemy };
            var allies = new[] { first, second, third };

            first.Tick(1f, enemies, allies);
            second.Tick(1f, enemies, allies);
            third.Tick(1f, enemies, allies);

            Assert(first.Position == path[1] && second.Position == path[1] && third.Position == path[1],
                "기지 말단에서 공간이 없는데 아군 중첩이 강제로 해소되었습니다.");

            enemy.TakeDamage(enemy.currentHealth);
            first.Tick(1f, enemies, allies);
            second.Tick(1f, enemies, allies);
            third.Tick(1f, enemies, allies);

            AssertNear(first.Position.x, 19f,
                "적 사망 후 먼저 스폰된 아군이 진격을 재개하지 않았습니다.");
            AssertNear(second.Position.x, 19.75f,
                "두 번째 아군이 열린 공간을 생성 순서대로 채우지 않았습니다.");
            AssertNear(third.Position.x, 20f,
                "공간이 부족한 후속 아군이 기지 말단 중첩 예외를 벗어났습니다.");

            first.Tick(1f, enemies, allies);
            second.Tick(1f, enemies, allies);
            third.Tick(1f, enemies, allies);

            AssertNear(second.Position.x - first.Position.x, first.ContactRange,
                "진격 재개 뒤 두 번째 아군의 대열 간격이 잘못되었습니다.");
            AssertNear(third.Position.x - second.Position.x, second.ContactRange,
                "기지에 겹쳤던 세 번째 아군이 영구 정지했습니다.");
        }

        private static void VerifyImmediateAttackAndCooldown(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 0f, 3f, 3f, true);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));

            unit.Tick(0.1f, new[] { enemy }, new[] { unit });
            AssertNear(enemy.currentHealth, 97f, "첫 공격이 쿨다운 0에서 즉시 발생하지 않았습니다.");
            unit.Tick(0.5f, new[] { enemy }, new[] { unit });
            AssertNear(enemy.currentHealth, 97f, "공격 주기 전에 추가 공격이 발생했습니다.");
            unit.Tick(0.5f, new[] { enemy }, new[] { unit });
            AssertNear(enemy.currentHealth, 94f, "공격 주기 후 공격이 발생하지 않았습니다.");
        }

        private static void VerifyAllyAdvancesAfterEnemyDeath(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 1f, 2f, 3f, true);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 1f, 0f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));

            unit.Tick(0f, new[] { enemy }, new[] { unit });

            Assert(enemy.IsDead, "공격 대상 적이 사망하지 않았습니다.");
            Assert(unit.CurrentTarget == null, "적 사망 후 아군 타깃이 즉시 해제되지 않았습니다.");
            Assert(unit.State == AllyUnitState.Advancing, "적 사망 후 아군이 진격 상태로 돌아가지 않았습니다.");
        }

        private static void VerifyEnemyAdvancesAfterAllyDeath(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 1f, 0f, 0f, 2f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 1f, 0f, 2f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(1f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, path[0]);

            unit.Tick(0f, new[] { enemy }, new[] { unit });
            Assert(enemy.CurrentTarget == unit, "적이 제시된 아군 후보를 기억하지 않았습니다.");
            unit.TakeDamage(1f);
            enemy.Tick(0f);
            enemy.Tick(1f);

            Assert(unit.IsDead, "아군 사망 상태가 되지 않았습니다.");
            Assert(enemy.CurrentTarget == null, "아군 사망 후 적 타깃이 해제되지 않았습니다.");
            Assert(enemy.position.x > 0f, "아군 사망 후 적이 경로 진격을 재개하지 않았습니다.");
        }

        private static void VerifyAlliesFocusEnemyFrontline(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 0f, 1f, 5f, true);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var first = new AllyUnitInstance();
            var second = new AllyUnitInstance();
            first.Spawn(allyDefinition, path);
            second.Spawn(allyDefinition, path);
            EnemyInstance advanced = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));
            EnemyInstance rear = SpawnEnemy(enemyDefinition, path, new Vector2(7f, 0f));

            first.Tick(0f, new[] { advanced, rear }, new[] { first, second });
            second.Tick(0f, new[] { advanced, rear }, new[] { first, second });

            AssertNear(advanced.currentHealth, 98f, "여러 아군이 적 전열 하나에 집중포화하지 않았습니다.");
            AssertNear(rear.currentHealth, 100f, "아군이 적 전열보다 뒤 대상에게 분산 공격했습니다.");
        }

        private static void VerifyEnemiesFocusAllyFrontline(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 10f, 0f, 20f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 20f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var frontline = new AllyUnitInstance();
            var rear = new AllyUnitInstance();
            frontline.Spawn(allyDefinition, path);
            rear.Spawn(allyDefinition, path);
            frontline.Tick(0.7f, Array.Empty<EnemyInstance>(), new[] { frontline, rear });
            EnemyInstance first = SpawnEnemy(enemyDefinition, path, new Vector2(0f, 0f));
            EnemyInstance second = SpawnEnemy(enemyDefinition, path, new Vector2(0.1f, 0f));

            frontline.Tick(0f, new[] { first, second }, new[] { frontline, rear });
            rear.Tick(0f, new[] { first, second }, new[] { frontline, rear });

            Assert(frontline.PathProgress < rear.PathProgress,
                "아군 전열 앞뒤에 설정한 진행도 차이가 없습니다.");
            Assert(first.CurrentTarget == frontline && second.CurrentTarget == frontline,
                "여러 적이 가장 앞선 아군 전열을 집중 공격할 후보를 얻지 못했습니다.");
        }

        private static void VerifyDeadTargetIsNotAttackedAgain(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 0f, 1f, 3f, true);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 1f, 0f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));

            unit.Tick(0f, new[] { enemy }, new[] { unit });
            float healthAfterDeath = enemy.currentHealth;
            unit.Tick(1f, new[] { enemy }, new[] { unit });

            Assert(enemy.IsDead, "죽은 대상이 다시 살아났습니다.");
            AssertNear(enemy.currentHealth, healthAfterDeath, "죽은 대상에게 재공격이 적용되었습니다.");
        }

        private static void VerifyTargetReleaseOnRangeExit(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 10f, 0f, 1f, 3f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, new Vector2(8f, 0f));

            unit.Tick(0f, new[] { enemy }, new[] { unit });
            Assert(unit.CurrentTarget == enemy, "공격 범위 진입 대상이 선택되지 않았습니다.");
            enemy.position = path[0];
            unit.Tick(0f, new[] { enemy }, new[] { unit });

            Assert(unit.CurrentTarget == null, "공격 범위 이탈 후 아군 타깃이 남아 있습니다.");
        }

        private static void VerifyEnemyMovesWithoutEngagement(List<UnityEngine.Object> temporaryObjects)
        {
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 1f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, path[0]);

            enemy.Tick(1f);
            enemy.Tick(1f);

            AssertNear(enemy.position.x, 1f, "교전 없는 적의 기존 웨이포인트 이동이 깨졌습니다.");
        }

        private static void VerifyGoalDamageAndReachedGoalOnce(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition goalDefinition = CreateAlly(temporaryObjects, 100f, 0f, 0f, 1f, false);
            AllyUnitDefinition dummyDefinition = CreateAlly(temporaryObjects, 100f, 0f, 0f, 1f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 2f, 7f, 0f, 1f, true);
            var path = new List<Vector2> { new(0f, 0f), new(1f, 0f) };
            var goal = new AllyUnitInstance();
            goal.Spawn(goalDefinition, path);
            var enemy = new EnemyInstance { definition = enemyDefinition, position = path[0] };
            enemy.Spawn(path, goal);
            int reachedGoalCount = 0;
            enemy.ReachedGoal += () => reachedGoalCount++;

            enemy.Tick(1f);
            enemy.Tick(1f);
            enemy.Tick(1f);

            AssertNear(goal.CurrentHealth, 93f, "거점 도달 시 기존 contactDamage 경로가 적용되지 않았습니다.");
            Assert(reachedGoalCount == 1, "ReachedGoal이 정확히 한 번 발생하지 않았습니다.");
            Assert(enemy.HasReachedGoal && !enemy.IsAlive, "거점 도달 적이 생존 상태로 남아 있습니다.");
            _ = dummyDefinition;
        }

        private static void VerifyBasicAttackDamage(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 100f, 0f, 7f, 1f, true);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 0f, 0f, 1f, false);
            var path = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, path);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, path, path[0]);

            unit.TriggerAttack(enemy);

            AssertNear(enemy.currentHealth, 93f, "BasicAttackEffect가 attackDamage만큼 피해를 적용하지 않았습니다.");
        }

        private static void VerifyEnemyContactDamageEffectPath(List<UnityEngine.Object> temporaryObjects)
        {
            AllyUnitDefinition allyDefinition = CreateAlly(temporaryObjects, 100f, 0f, 0f, 1f, false);
            EnemyDefinition enemyDefinition = CreateEnemy(temporaryObjects, 100f, 0f, 7f, 1f, 1f, true);
            var enemyPath = new List<Vector2> { new(0f, 0f), new(10f, 0f) };
            var allyPath = new List<Vector2> { new(0f, 0f), new(1f, 0f) };
            var unit = new AllyUnitInstance();
            unit.Spawn(allyDefinition, allyPath);
            EnemyInstance enemy = SpawnEnemy(enemyDefinition, enemyPath, enemyPath[0]);

            unit.Tick(0f, new[] { enemy }, new[] { unit });
            enemy.Tick(0f);

            AssertNear(unit.CurrentHealth, 93f, "적 공격이 기존 ContactDamageEffect 경로를 사용하지 않았습니다.");
        }

        private static UnitCombatSettings CreateSettings(
            List<UnityEngine.Object> temporaryObjects,
            float contactRange,
            float separationMargin)
        {
            var settings = ScriptableObject.CreateInstance<UnitCombatSettings>();
            temporaryObjects.Add(settings);
            SetPrivateFloat(settings, "contactRange", contactRange);
            SetPrivateFloat(settings, "separationMargin", separationMargin);
            return settings;
        }

        private static AllyUnitDefinition CreateAlly(
            List<UnityEngine.Object> temporaryObjects,
            float maxHealth,
            float moveSpeed,
            float attackDamage,
            float attackRange,
            bool basicAttack)
        {
            var definition = ScriptableObject.CreateInstance<AllyUnitDefinition>();
            temporaryObjects.Add(definition);
            definition.data = new AllyUnitData
            {
                unitId = "combat-verification-ally",
                displayName = "검증 아군",
                maxHealth = maxHealth,
                moveSpeed = moveSpeed,
                attackDamage = attackDamage,
                attackInterval = 1f,
                attackRange = attackRange,
                detectionRange = 99f,
            };

            if (basicAttack)
            {
                var effect = ScriptableObject.CreateInstance<BasicAttackEffect>();
                temporaryObjects.Add(effect);
                definition.effects.Add(effect);
            }

            return definition;
        }

        private static EnemyDefinition CreateEnemy(
            List<UnityEngine.Object> temporaryObjects,
            float maxHealth,
            float moveSpeed,
            float contactDamage,
            float attackRange,
            float attackInterval,
            bool contactEffect)
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            temporaryObjects.Add(definition);
            definition.data = new EnemyData
            {
                enemyId = "combat-verification-enemy",
                displayName = "검증 적",
                maxHealth = maxHealth,
                moveSpeed = moveSpeed,
                contactDamage = contactDamage,
                attackRange = attackRange,
                attackInterval = attackInterval,
            };

            if (contactEffect)
            {
                var effect = ScriptableObject.CreateInstance<ContactDamageEffect>();
                temporaryObjects.Add(effect);
                definition.effects.Add(effect);
            }

            return definition;
        }

        private static EnemyInstance SpawnEnemy(
            EnemyDefinition definition,
            IReadOnlyList<Vector2> path,
            Vector2 position)
        {
            var enemy = new EnemyInstance
            {
                definition = definition,
                position = position,
            };
            enemy.Spawn(path, null);
            enemy.position = position;
            return enemy;
        }

        private static void SetPrivateFloat(UnityEngine.Object target, string fieldName, float value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(fieldName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertNear(float actual, float expected, string message)
        {
            if (Mathf.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException($"{message} (실제 {actual}, 기대 {expected})");
            }
        }
    }
}
