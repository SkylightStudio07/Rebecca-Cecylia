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
    /// 현재 커밋은 두 클라이언트 작업의 공통 계약만 고정하고 이동·탐색·발포 구현은 후속 작업에 맡긴다.
    /// </summary>
    public class AllyUnitInstance : IDamageable
    {
        private static readonly IReadOnlyList<EnemyInstance> EmptyEnemies = Array.Empty<EnemyInstance>();
        private static readonly IReadOnlyList<AllyUnitInstance> EmptyAllies = Array.Empty<AllyUnitInstance>();

        private IReadOnlyList<Vector2> _path;
        private int _pathIndex;
        private bool _isSpawned;
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

        public Vector2? CurrentTargetWaypoint =>
            _path != null && _pathIndex >= 0 && _pathIndex < _path.Count ? _path[_pathIndex] : (Vector2?)null;

        public event Action<float> Damaged;
        public event Action Died;

        /// <summary>
        /// MapManager.Waypoints의 정방향 목록을 받아 끝점에서 시작하고 인덱스를 감소시키도록 준비한다.
        /// 호출자가 역순 복사본을 만들지 않아도 되어 적과 아군이 같은 경로 원본을 공유할 수 있다.
        /// </summary>
        public void Spawn(AllyUnitDefinition definition, IReadOnlyList<Vector2> path)
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
            _isSpawned = true;

            AllyUnitContext ctx = MakeContext(0f, EmptyEnemies, EmptyAllies);
            foreach (IAllyUnitEffect effect in definition.effects)
            {
                effect.OnSpawn(ctx);
            }
        }

        /// <summary>
        /// 후속 전투 구현의 고정 진입점. UnitDeployController가 활성 적·아군 목록과 함께 호출한다.
        /// 현재는 효과 Tick만 전달하며 이동·타깃 선택·공격 상태 전이는 클라이언트 A가 구현한다.
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

            AllyUnitContext ctx = MakeContext(deltaTime, _lastEnemies, _lastAllies);
            foreach (IAllyUnitEffect effect in Definition.effects)
            {
                effect.OnTick(ctx);
            }
        }

        /// <summary>전투 구현이 타깃 획득·상실 시 호출하는 공통 상태 전이.</summary>
        public void SetEngagementTarget(EnemyInstance target)
        {
            if (!_isSpawned || IsDead)
            {
                return;
            }

            CurrentTarget = target;
            State = target != null ? AllyUnitState.Engaging : AllyUnitState.Advancing;
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
