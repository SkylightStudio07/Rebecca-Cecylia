using System;
using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 소환된 아군 유닛 1체의 순수 C# 런타임 상태. UnitDeployController가 목록을 소유하고
    /// Tick을 호출하며, 이 클래스에는 MonoBehaviour.Update를 두지 않는다.
    /// 이동·타기팅·발포를 View와 분리해 처리하며, 인스턴스별 타깃·쿨다운은 이 클래스만 소유한다.
    /// </summary>
    public class AllyUnitInstance : IDamageable
    {
        private static readonly IReadOnlyList<EnemyInstance> EmptyEnemies = Array.Empty<EnemyInstance>();
        private static readonly IReadOnlyList<AllyUnitInstance> EmptyAllies = Array.Empty<AllyUnitInstance>();
        private const float DefaultContactRange = 0.75f;
        private const float DefaultSeparationMargin = 0.05f;

        private IReadOnlyList<Vector2> _path;
        private int _pathIndex;
        private bool _isSpawned;
        private float _contactRange = DefaultContactRange;
        private float _separationMargin = DefaultSeparationMargin;
        private Vector2 _finalWaitPoint;
        private float _finalWaitProgress;
        private bool _hasReachedFinalWaitPoint;
        private IReadOnlyList<EnemyInstance> _lastEnemies = EmptyEnemies;
        private IReadOnlyList<AllyUnitInstance> _lastAllies = EmptyAllies;

        public AllyUnitDefinition Definition { get; private set; }
        public AllyUnitData Data => Definition != null ? Definition.data : null;
        public Vector2 Position { get; private set; }
        public float CurrentHealth { get; private set; }
        public AllyUnitState State { get; private set; } = AllyUnitState.Advancing;
        public EnemyInstance CurrentTarget { get; private set; }
        public float AttackCooldownRemaining { get; set; }
        public bool IsSpawned => _isSpawned;
        public bool IsDead => State == AllyUnitState.Dead;
        public bool IsAlive => _isSpawned && !IsDead;
        public float ContactRange => _contactRange;
        public float SeparationMargin => _separationMargin;
        public float EffectiveAttackRange => Mathf.Max(Data != null ? Data.attackRange : 0f, _contactRange);

        /// <summary>
        /// 경로 시작점 0, 끝점 1인 연속 진행도. 아군은 끝점에서 시작해 값이 자연스럽게
        /// 감소하므로 적의 진행도와 같은 좌표계에서 전열을 비교할 수 있다.
        /// </summary>
        public float PathProgress => AllyUnitTargeting.CalculatePathProgress(
            _path,
            _pathIndex,
            Position,
            false);

        public Vector2? CurrentTargetWaypoint
        {
            get
            {
                if (_path == null || _path.Count == 0)
                {
                    return null;
                }

                if (_hasReachedFinalWaitPoint)
                {
                    // 최종 대기점에서는 이동을 하지 않지만, 타깃이 없을 때 적 생성점
                    // 방향을 바라보게 해 View가 마지막 회전 상태에 고정되지 않게 한다.
                    return _path[0];
                }

                return _pathIndex >= 0 && _pathIndex < _path.Count ? _path[_pathIndex] : (Vector2?)null;
            }
        }

        public event Action<float> Damaged;
        public event Action Died;

        /// <summary>
        /// MapManager.Waypoints의 정방향 목록을 받아 끝점에서 시작하고 인덱스를 감소시키도록 준비한다.
        /// 호출자가 역순 복사본을 만들지 않아도 되어 적과 아군이 같은 경로 원본을 공유할 수 있다.
        /// </summary>
        public void Spawn(AllyUnitDefinition definition, IReadOnlyList<Vector2> path)
        {
            Spawn(definition, path, null);
        }

        /// <summary>
        /// 전투 거리 설정을 주입해 스폰한다. 설정 SO가 없어도 기존 호출부가 같은 기본
        /// 거리를 사용하도록 오버로드를 유지하며, 인스턴스에는 해석된 값만 보관한다.
        /// </summary>
        public void Spawn(
            AllyUnitDefinition definition,
            IReadOnlyList<Vector2> path,
            UnitCombatSettings settings)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.data == null || path == null || path.Count == 0)
            {
                throw new InvalidOperationException("아군 유닛 Definition 또는 이동 경로가 비어 있습니다.");
            }

            Definition = definition;
            _path = path;
            _pathIndex = path.Count - 2;
            Position = path[path.Count - 1];
            CurrentHealth = definition.data.maxHealth;
            State = AllyUnitState.Advancing;
            CurrentTarget = null;
            AttackCooldownRemaining = 0f;
            _contactRange = settings != null ? settings.ContactRange : DefaultContactRange;
            _separationMargin = settings != null ? settings.SeparationMargin : DefaultSeparationMargin;
            float totalPathLength = AllyUnitTargeting.CalculatePathLength(path);
            float requestedWaitDistance = _contactRange + _separationMargin;
            float safeWaitDistance = totalPathLength > 0.0001f
                ? Mathf.Clamp(
                    requestedWaitDistance,
                    0.0001f,
                    Mathf.Max(0.0001f, totalPathLength - 0.0001f))
                : 0f;
            _finalWaitPoint = AllyUnitTargeting.GetPointAtDistance(path, safeWaitDistance);
            _finalWaitProgress = totalPathLength > 0.0001f
                ? safeWaitDistance / totalPathLength
                : 1f;
            _hasReachedFinalWaitPoint = path.Count <= 1 ||
                                        PathProgress <= _finalWaitProgress + 0.0001f;
            _isSpawned = true;

            AllyUnitContext ctx = MakeContext(0f, EmptyEnemies, EmptyAllies);
            foreach (IAllyUnitEffect effect in definition.effects)
            {
                effect.OnSpawn(ctx);
            }
        }

        /// <summary>
        /// UnitDeployController가 활성 적·아군 목록과 함께 호출하는 고정 진입점. 이동도
        /// 이 순수 C# 인스턴스가 처리해 View와 전투 흐름이 서로 결합되지 않게 한다.
        /// </summary>
        public void Tick(
            float deltaTime,
            IReadOnlyList<EnemyInstance> activeEnemies,
            IReadOnlyList<AllyUnitInstance> activeAllies)
        {
            if (!_isSpawned || IsDead)
            {
                return;
            }

            _lastEnemies = activeEnemies ?? EmptyEnemies;
            _lastAllies = activeAllies ?? EmptyAllies;

            RefreshTargetAndState();
            TickAttack(Mathf.Max(0f, deltaTime));
            if (IsDead)
            {
                return;
            }

            if (State != AllyUnitState.Engaging)
            {
                MoveAlongPath(Mathf.Max(0f, deltaTime));
            }

            AllyUnitContext ctx = MakeContext(deltaTime, _lastEnemies, _lastAllies);
            foreach (IAllyUnitEffect effect in Definition.effects)
            {
                effect.OnTick(ctx);
            }

            // 적이 이동 차단 sweep을 최신 아군 위치로 계산해야 하므로 이동과 효과 처리가
            // 끝난 뒤 후보를 제시한다. UnitDeployController는 모든 아군 Tick을 WaveManager보다
            // 먼저 끝내기만 하면 별도의 후보 선행 순회를 할 필요가 없다.
            OfferAttackCandidates(_lastEnemies);
        }

        /// <summary>
        /// 현재 위치를 기준으로 공격·이동 차단 후보를 적에게 제시한다. Tick은 이동을 마친 뒤
        /// 이 메서드를 한 번 호출하므로 통합 컨트롤러가 같은 후보 목록을 다시 순회하지 않는다.
        /// </summary>
        public void OfferAttackCandidates(IReadOnlyList<EnemyInstance> activeEnemies)
        {
            if (!_isSpawned || IsDead)
            {
                return;
            }

            _lastEnemies = activeEnemies ?? EmptyEnemies;
            foreach (EnemyInstance enemy in _lastEnemies)
            {
                if (enemy != null)
                {
                    enemy.TryOfferAttackTarget(this);
                    // 실제 적 Tick에서 deltaTime과 둔화 만료를 다시 검증한다. 여기서는 현재
                    // 웨이포인트 선분 전체를 후보로 잡아 프레임 중 속도 변화도 놓치지 않는다.
                    enemy.TryOfferMovementTarget(this, float.PositiveInfinity);
                }
            }
        }

        /// <summary>전투 구현이 타깃 획득·상실 시 호출하는 공통 상태 전이.</summary>
        public void SetEngagementTarget(EnemyInstance target)
        {
            if (!_isSpawned || IsDead)
            {
                return;
            }

            if (target == null)
            {
                CurrentTarget = null;
                State = AllyUnitState.Advancing;
                return;
            }

            if (!target.IsAlive)
            {
                CurrentTarget = null;
                State = AllyUnitState.Advancing;
                return;
            }

            CurrentTarget = target;
            State = IsTargetInContactRange(target)
                ? AllyUnitState.Engaging
                : AllyUnitState.Advancing;
        }

        /// <summary>공격 대상이 아니라 실제 접촉선 안에 들어왔는지 확인한다.</summary>
        public bool IsTargetInContactRange(EnemyInstance target)
        {
            return target != null && target.IsAlive &&
                   AllyUnitTargeting.IsWithinRange(Position, target.position, ContactRange);
        }

        /// <summary>현재 위치에서 공격 범위 안에 있는지 확인한다.</summary>
        public bool IsTargetInAttackRange(EnemyInstance target)
        {
            return target != null && target.IsAlive &&
                   AllyUnitTargeting.IsWithinRange(Position, target.position, EffectiveAttackRange);
        }

        /// <summary>공격 타이밍을 결정한 런타임 로직이 효과 SO의 OnAttack 훅을 구동한다.</summary>
        public void TriggerAttack(EnemyInstance target)
        {
            if (!_isSpawned || IsDead || target == null)
            {
                return;
            }

            AllyUnitContext ctx = MakeContext(0f, _lastEnemies, _lastAllies);
            foreach (IAllyUnitEffect effect in Definition.effects)
            {
                effect.OnAttack(ctx, target);
            }
        }

        public void TakeDamage(float amount)
        {
            if (!_isSpawned || IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth -= amount;
            Damaged?.Invoke(amount);
            if (CurrentHealth > 0f)
            {
                return;
            }

            CurrentHealth = 0f;
            State = AllyUnitState.Dead;
            CurrentTarget = null;

            AllyUnitContext ctx = MakeContext(0f, _lastEnemies, _lastAllies);
            foreach (IAllyUnitEffect effect in Definition.effects)
            {
                effect.OnDeath(ctx);
            }

            Died?.Invoke();
        }

        private void RefreshTargetAndState()
        {
            EnemyInstance attackTarget = AllyUnitTargeting.FindBestEnemy(this, _lastEnemies);
            CurrentTarget = attackTarget;

            EnemyInstance contactTarget = AllyUnitTargeting.FindBestContactEnemy(this, _lastEnemies);
            State = contactTarget != null ? AllyUnitState.Engaging : AllyUnitState.Advancing;
        }

        private void TickAttack(float deltaTime)
        {
            if (!IsTargetInAttackRange(CurrentTarget))
            {
                CurrentTarget = null;
                if (State != AllyUnitState.Dead)
                {
                    State = AllyUnitState.Advancing;
                }

                return;
            }

            AttackCooldownRemaining -= deltaTime;
            if (AttackCooldownRemaining > 0f)
            {
                return;
            }

            EnemyInstance target = CurrentTarget;
            TriggerAttack(target);
            AttackCooldownRemaining = Data.attackInterval > 0f ? Data.attackInterval : 1f;

            if (target == null || !target.IsAlive || !IsTargetInAttackRange(target))
            {
                CurrentTarget = null;
                if (State != AllyUnitState.Dead)
                {
                    State = AllyUnitState.Advancing;
                }
            }
        }

        private void MoveAlongPath(float deltaTime)
        {
            if (_hasReachedFinalWaitPoint || _path == null || _path.Count <= 1 ||
                Data == null || Data.moveSpeed <= 0f)
            {
                return;
            }

            float totalPathLength = AllyUnitTargeting.CalculatePathLength(_path);
            float distanceToWaitPoint = Mathf.Max(0f, (PathProgress - _finalWaitProgress) * totalPathLength);
            if (distanceToWaitPoint <= 0.0001f)
            {
                Position = _finalWaitPoint;
                _hasReachedFinalWaitPoint = true;
                return;
            }

            float remainingDistance = Mathf.Min(Data.moveSpeed * deltaTime, distanceToWaitPoint);
            while (remainingDistance > 0.0001f && !_hasReachedFinalWaitPoint)
            {
                if (_pathIndex < 0 || _pathIndex >= _path.Count)
                {
                    Position = _finalWaitPoint;
                    _hasReachedFinalWaitPoint = true;
                    break;
                }

                Vector2 target = _path[_pathIndex];
                Vector2 toTarget = target - Position;
                float distance = toTarget.magnitude;
                if (distance <= 0.0001f)
                {
                    Position = target;
                    _pathIndex--;
                    continue;
                }

                float movementDistance = Mathf.Min(remainingDistance, distance);
                float contactDistance = DistanceBeforeContact(target);
                if (contactDistance <= movementDistance + 0.0001f)
                {
                    float safeContactDistance = Mathf.Min(contactDistance, movementDistance);
                    if (safeContactDistance > 0.0001f)
                    {
                        Position += toTarget / distance * safeContactDistance;
                    }

                    remainingDistance = 0f;
                    UpdateContactState();
                    break;
                }

                if (remainingDistance >= distance)
                {
                    Position = target;
                    remainingDistance -= distance;
                    _pathIndex--;
                }
                else
                {
                    Position += toTarget / distance * remainingDistance;
                    remainingDistance = 0f;
                }
            }

            if (PathProgress <= _finalWaitProgress + 0.0001f)
            {
                Position = _finalWaitPoint;
                _hasReachedFinalWaitPoint = true;
            }

            UpdateContactState();
        }

        private float DistanceBeforeContact(Vector2 end)
        {
            float minimumDistance = float.PositiveInfinity;
            foreach (EnemyInstance enemy in _lastEnemies)
            {
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                minimumDistance = Mathf.Min(
                    minimumDistance,
                    AllyUnitTargeting.DistanceBeforeContact(
                        Position,
                        end,
                        enemy.position,
                        ContactRange));
            }

            AllyUnitInstance precedingAlly = FindPrecedingAlly();
            if (precedingAlly != null)
            {
                minimumDistance = Mathf.Min(
                    minimumDistance,
                    AllyUnitTargeting.DistanceBeforeContact(
                        Position,
                        end,
                        precedingAlly.Position,
                        ContactRange));
            }

            return minimumDistance;
        }

        /// <summary>
        /// 활성 목록의 등록 순서를 스폰 순서로 사용해 바로 앞의 살아 있는 아군만 찾는다.
        /// 모든 앞 유닛을 장애물로 취급하면 굽거나 교차하는 경로에서 다른 선분의 유닛이
        /// 후속 대열을 막을 수 있으므로, 추월만 막으면 되는 직전 선행 유닛으로 한정한다.
        /// </summary>
        private AllyUnitInstance FindPrecedingAlly()
        {
            AllyUnitInstance precedingAlly = null;
            foreach (AllyUnitInstance ally in _lastAllies)
            {
                if (ReferenceEquals(ally, this))
                {
                    return precedingAlly;
                }

                if (ally != null && ally.IsAlive)
                {
                    precedingAlly = ally;
                }
            }

            return null;
        }

        private void UpdateContactState()
        {
            EnemyInstance contactTarget = AllyUnitTargeting.FindBestContactEnemy(this, _lastEnemies);
            if (contactTarget != null)
            {
                CurrentTarget = contactTarget;
                State = AllyUnitState.Engaging;
            }
        }

        private AllyUnitContext MakeContext(
            float deltaTime,
            IReadOnlyList<EnemyInstance> activeEnemies,
            IReadOnlyList<AllyUnitInstance> activeAllies)
        {
            return new AllyUnitContext
            {
                self = this,
                deltaTime = deltaTime,
                activeEnemies = activeEnemies,
                activeAllies = activeAllies,
            };
        }
    }
}
