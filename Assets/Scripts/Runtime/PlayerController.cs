using System;
using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Effects.PlayerPart;
using RCCom.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RCCom.Runtime
{
    /// <summary>
    /// 플레이어 컨트롤러. 그리드와 무관한 자유 이동, 가장 가까운 적 자동 원거리 공격,
    /// 쿨다운 기반 스킬, 피격/무적 처리를 담당한다. 개체가 하나뿐이라 Tower처럼 그냥
    /// MonoBehaviour로 둔다 (매니저 아키텍처 원칙상 PlayerManager도 만들지 않음).
    ///
    /// 인스펙터 연결 필요:
    /// - data: PlayerData
    /// - moveAction: InputSystem_Actions의 Player/Move
    /// - attackRangeTrigger: 자식 오브젝트(원거리 사거리용 큰 Collider2D)에 붙은 AttackRangeTrigger
    ///
    /// 스킬은 원래 "Attack" 액션(마우스 좌클릭 포함)을 재사용했는데, 타워 건설 클릭과 입력이
    /// 겹쳐버려서({{user}} 발견) 스페이스바 직접 폴링(Keyboard.current.spaceKey)으로 변경—
    /// TowerBuildController의 우클릭/숫자키와 같은 스타일로 통일.
    ///
    /// 이 오브젝트 자신의 Collider2D는 몸통 히트박스(적 접촉 피해 판정용, EnemyView 쪽에서 감지)이고,
    /// attackRangeTrigger는 별도 자식 오브젝트의 더 큰 콜라이더다 — 한 오브젝트에 반경이 다른
    /// 콜라이더 2개를 두면 OnTriggerEnter2D에서 어느 쪽인지 구분이 안 돼 분리했다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        public PlayerData data;
        public InputActionReference moveAction;
        public AttackRangeTrigger attackRangeTrigger;

        [Tooltip("플레이어가 벗어나면 안 되는 영역 (배경 크기와 맞춘 BoxCollider2D — CameraFollow의 Bounds와 같은 오브젝트를 드래그하면 됨)")]
        [SerializeField] private Collider2D levelBounds;

        [SerializeField] private GameObject fakeProjectilePrefab;

        [Tooltip("스프라이트만 담은 자식 오브젝트가 있으면 여기 연결 — 회전이 몸통 Collider2D에 영향을 안 주게 됨. 비워두면 이 오브젝트(루트) 자체를 회전시킴.")]
        [SerializeField] private Transform spriteTransform;

        [Tooltip("스프라이트 아트가 기본적으로 바라보는 방향 보정각 (오른쪽 기준 0, 위쪽 기준 90, 아래쪽 -90, 왼쪽 180)")]
        [SerializeField] private float spriteForwardOffsetDegrees = 90f;

        [Header("피격/스킬 시각 피드백 (EnemyView 피격 틴트와 같은 방식)")]
        [Tooltip("스프라이트가 루트에 바로 있으면 이 오브젝트의 SpriteRenderer, spriteTransform 자식에 있으면 그쪽의 SpriteRenderer를 연결")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.15f;
        [Tooltip("스킬(오버드라이브 모드) 발동 중 유지되는 틴트 — 피격 틴트보다 우선순위가 낮음")]
        [SerializeField] private Color skillTintColor = new(0.5f, 0.8f, 1f, 1f);

        public float CurrentHealth { get; private set; }
        public float MaxHealth => data != null ? data.maxHealth : 0f;
        public bool IsDead => _isDead;
        public Vector2 LastMoveInput { get; private set; }

        /// <summary>피격이 실제로 적용됐을 때(무적 중이 아닐 때) 알림 — 오퍼레이터 대사 등 UI가 구독.</summary>
        public event Action<float> Damaged;

        /// <summary>체력 0 도달 시 1회만 발생.</summary>
        public event Action Died;

        /// <summary>스킬(오버드라이브 모드) 발동 시작 시 1회 발생 — 오퍼레이터 대사가 구독.</summary>
        public event Action SkillUsed;

        private bool _isDead;

        /// <summary>HUD가 "오버드라이브 모드" 텍스트 표시 여부 판단에 사용.</summary>
        public bool IsSkillActive { get; private set; }

        /// <summary>HUD가 "준비" 텍스트 표시 여부 판단에 사용 — 쿨다운도 다 됐고 지금 사용 중도 아닐 때.</summary>
        public bool IsSkillReady => !IsSkillActive && _skillChargesAvailable > 0;

        /// <summary>HUD 스킬 게이지바가 참조 (0 = 방금 사용, SkillCooldownDuration = 완전 충전).</summary>
        public float SkillCooldownRemaining => Mathf.Max(0f, _skillCooldownRemaining);
        public float SkillCooldownDuration => data.skillCooldown;

        private float _invulnerabilityRemaining;
        private float _attackCooldownRemaining;
        private float _skillCooldownRemaining;
        private int _skillBurstRemaining;
        private float _skillBurstTimer;
        private float _hitFlashRemaining;
        private Color _baseColor = Color.white;
        private float _forcedDashRemaining;
        private Vector2 _forcedDashDirection = Vector2.right;
        private int _skillChargesAvailable;

        private readonly List<PlayerPartEffectBase> _partEffects = new();
        private readonly Dictionary<PlayerPartEffectBase, PlayerPartRuntimeState> _partStates = new();
        private readonly List<PlayerAttackShot> _queuedAttackShots = new();
        private IPlayerPrimaryAttackEffect _primaryAttackEffect;

        private readonly List<EnemyInstance> _enemiesInRange = new();

        private void Awake()
        {
            PlayerLoadoutResult loadout = OperatorLoadoutSession.ComposePlayerLoadout(data);
            data = loadout.data;
            _partEffects.AddRange(loadout.effects);
            attackRangeTrigger.SetWorldRadius(data.attackRange);
            CurrentHealth = data.maxHealth;
            _skillChargesAvailable = Mathf.Max(1, data.skillChargeCapacity);

            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                if (effect is IPlayerPrimaryAttackEffect primaryAttack)
                {
                    _primaryAttackEffect = primaryAttack;
                    break;
                }
            }

            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
            }

            PlayerPartContext spawnContext = MakePartContext(0f);
            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                effect.OnSpawn(spawnContext);
            }
        }

        private void OnEnable()
        {
            moveAction.action.Enable();
            attackRangeTrigger.EnteredRange += HandleEnemyEnteredRange;
            attackRangeTrigger.ExitedRange += HandleEnemyExitedRange;
        }

        private void OnDisable()
        {
            moveAction.action.Disable();
            attackRangeTrigger.EnteredRange -= HandleEnemyEnteredRange;
            attackRangeTrigger.ExitedRange -= HandleEnemyExitedRange;
        }

        private void Update()
        {
            // 카드 선택/게임오버/튜토리얼 등 Time.timeScale=0인 동안엔 입력 처리 자체를 완전히
            // 멈춘다 — deltaTime이 0이라 이동(위치)은 어차피 안 움직이지만, UpdateFacing()은
            // deltaTime과 무관하게 입력 방향만 보고 즉시 회전시키고 스킬 발동도 키 입력만
            // 확인하는 폴링이라, 이 게이트가 없으면 "멈춘 것처럼 보여도" 방향 전환/스킬 발동이
            // 그대로 새어나간다 ({{user}} 발견).
            if (Time.timeScale <= 0f)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            TickQueuedAttackShots(deltaTime);
            TickPartEffects(deltaTime);
            Move(deltaTime);
            TickTimers(deltaTime);
            TryAutoAttack();
            TrySkill();
        }

        private void Move(float deltaTime)
        {
            Vector2 input = moveAction.action.ReadValue<Vector2>();
            LastMoveInput = input;
            Vector2 moveDirection = input.normalized;
            if (_forcedDashRemaining > 0f)
            {
                _forcedDashRemaining -= deltaTime;
                moveDirection = _forcedDashDirection;
            }

            float moveSpeed = data.moveSpeed *
                (IsSkillActive ? data.skillOverdriveMoveSpeedMultiplier : 1f);
            PlayerPartContext context = MakePartContext(deltaTime);
            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                moveSpeed = effect.ModifyMoveSpeed(context, moveSpeed);
            }

            Vector3 nextPosition = transform.position + (Vector3)(moveDirection * moveSpeed * deltaTime);

            if (levelBounds != null)
            {
                Bounds bounds = levelBounds.bounds;
                nextPosition.x = Mathf.Clamp(nextPosition.x, bounds.min.x, bounds.max.x);
                nextPosition.y = Mathf.Clamp(nextPosition.y, bounds.min.y, bounds.max.y);
            }

            transform.position = nextPosition;
            UpdateFacing(moveDirection);
        }

        /// <summary>
        /// 입력이 있을 때만 이동 방향으로 스프라이트를 회전 — 입력이 0이 되는 순간(정지) 각도가
        /// 0으로 스냅되는 걸 막기 위해, 멈춰 있을 땐 마지막 방향을 그대로 유지한다.
        /// </summary>
        private void UpdateFacing(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg + spriteForwardOffsetDegrees;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            if (spriteTransform != null)
            {
                spriteTransform.rotation = rotation;
            }
            else
            {
                transform.rotation = rotation;
            }
        }

        private void TickTimers(float deltaTime)
        {
            _invulnerabilityRemaining -= deltaTime;
            _attackCooldownRemaining -= deltaTime;
            _skillCooldownRemaining -= deltaTime;

            if (_skillChargesAvailable < Mathf.Max(1, data.skillChargeCapacity) &&
                _skillCooldownRemaining <= 0f && !IsSkillActive)
            {
                _skillChargesAvailable++;
                _skillCooldownRemaining = _skillChargesAvailable < data.skillChargeCapacity
                    ? data.skillCooldown
                    : 0f;
            }

            if (_hitFlashRemaining > 0f)
            {
                _hitFlashRemaining -= deltaTime;
            }

            UpdateSpriteTint();
        }

        /// <summary>
        /// EnemyView의 피격 틴트와 같은 방식 — 로직 없이 SpriteRenderer.color만 상황에 맞춰
        /// 바꾼다. 우선순위: 피격 직후(빨강) > 스킬(오버드라이브 모드) 사용 중(청색) > 평상시(원래 색).
        /// </summary>
        private void UpdateSpriteTint()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (_hitFlashRemaining > 0f)
            {
                spriteRenderer.color = hitFlashColor;
            }
            else if (IsSkillActive)
            {
                spriteRenderer.color = skillTintColor;
            }
            else
            {
                spriteRenderer.color = _baseColor;
            }
        }

        private void TryAutoAttack()
        {
            if (_attackCooldownRemaining > 0f)
            {
                return;
            }

            EnemyInstance target = EnemyTargeting.FindNearestInRange(_enemiesInRange, transform.position, data.attackRange);
            if (target == null)
            {
                if (_enemiesInRange.Count > 0)
                {
                    // TODO: 확인 끝나면 삭제 — 사거리 진입 로그(HandleEnemyEnteredRange)는 안 뜨는데
                    // 이 로그만 뜨면 데이터(attackRange)와 실제 트리거 콜라이더 반경이 어긋난 것.
                    Debug.Log($"[PlayerDebug] 사거리 내 목록엔 {_enemiesInRange.Count}마리 있지만 attackRange({data.attackRange}) 조건 통과 못 함");
                }

                return;
            }

            var context = new PlayerAttackContext
            {
                self = this,
                data = data,
                activeEnemies = new List<EnemyInstance>(_enemiesInRange),
                origin = transform.position,
            };

            if (_primaryAttackEffect != null)
            {
                _primaryAttackEffect.OnAttack(context, target);
            }
            else
            {
                // 카탈로그/에셋이 아직 없는 씬 단독 테스트에서도 기존 단발 공격은 유지한다.
                context.Fire(target);
            }

            _attackCooldownRemaining = data.attackInterval;
        }

        /// <summary>
        /// "오버드라이브 모드": 1회성 즉발 대신, skillBurstInterval 간격으로 skillBurstCount번
        /// 연속 범위 공격을 가한다. 이펙트 없이도 매 타격마다 EnemyView의 기존 피격 빨간 틴트가
        /// 자연스럽게 터져서 시각 피드백이 되므로 별도 VFX가 필요 없다.
        /// </summary>
        private void TrySkill()
        {
            if (IsSkillActive)
            {
                TickSkillBurst();
                return;
            }

            bool skillPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (_skillChargesAvailable <= 0 || !skillPressed)
            {
                return;
            }

            IsSkillActive = true;
            _skillChargesAvailable--;
            _skillBurstRemaining = data.skillBurstCount;
            _skillBurstTimer = 0f; // 첫 타는 즉시 발동
            SkillUsed?.Invoke();

            PlayerPartContext context = MakePartContext(0f);
            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                effect.OnSkillUsed(context);
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySkill();
            }
        }

        private void TickSkillBurst()
        {
            _skillBurstTimer -= Time.deltaTime;
            if (_skillBurstTimer > 0f)
            {
                return;
            }

            FireSkillPulse();
            _skillBurstRemaining--;
            _skillBurstTimer = data.skillBurstInterval;

            if (_skillBurstRemaining <= 0)
            {
                IsSkillActive = false;
                if (_skillChargesAvailable < Mathf.Max(1, data.skillChargeCapacity) &&
                    _skillCooldownRemaining <= 0f)
                {
                    _skillCooldownRemaining = data.skillCooldown;
                }
            }
        }

        private void FireSkillPulse()
        {
            float sqrSkillRange = data.skillRange * data.skillRange;
            // PierceDamageEffect/SplashDamageEffect와 같은 이유(TakeDamage → 즉사 →
            // EnemyView.HandleDied()의 콜라이더 비활성화 → OnTriggerExit2D 동기 발생)로
            // _enemiesInRange 원본을 직접 순회하면 열거 도중 리스트가 바뀌어 예외가 난다.
            var targets = new List<EnemyInstance>(_enemiesInRange);
            foreach (EnemyInstance enemy in targets)
            {
                if ((enemy.position - (Vector2)transform.position).sqrMagnitude <= sqrSkillRange)
                {
                    enemy.TakeDamage(data.skillDamage, transform.position);
                }
            }
        }

        private void HandleEnemyEnteredRange(Collider2D other)
        {
            if (other.TryGetComponent(out EnemyView enemyView))
            {
                _enemiesInRange.Add(enemyView.Instance);
                // TODO: 확인 끝나면 삭제 — 이 로그가 아예 안 뜨면 AttackRangeTrigger 쪽 콜라이더/
                // Rigidbody2D/Is Trigger 설정 문제 (물리 이벤트 자체가 안 들어오는 것).
                Debug.Log($"[PlayerDebug] 사거리 진입: {enemyView.Instance.Data.displayName} (현재 {_enemiesInRange.Count}마리)");
            }
        }

        private void HandleEnemyExitedRange(Collider2D other)
        {
            if (other.TryGetComponent(out EnemyView enemyView))
            {
                _enemiesInRange.Remove(enemyView.Instance);
                Debug.Log($"[PlayerDebug] 사거리 이탈: {enemyView.Instance.Data.displayName} (현재 {_enemiesInRange.Count}마리)"); // TODO: 확인 끝나면 삭제
            }
        }

        public void TakeDamage(float amount, Vector2? sourcePosition = null)
        {
            if (_invulnerabilityRemaining > 0f || _isDead)
            {
                return;
            }

            PlayerPartContext context = MakePartContext(0f);
            float appliedDamage = Mathf.Max(0f, amount);
            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                appliedDamage = Mathf.Max(0f, effect.ModifyIncomingDamage(context, appliedDamage));
            }

            if (appliedDamage <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - appliedDamage);
            _invulnerabilityRemaining = data.hitInvulnerabilityDuration;
            _hitFlashRemaining = hitFlashDuration;
            Damaged?.Invoke(appliedDamage);

            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                effect.OnDamaged(context, appliedDamage);
            }

            if (CurrentHealth <= 0f)
            {
                foreach (PlayerPartEffectBase effect in _partEffects)
                {
                    if (effect.TryPreventPlayerDeath(context))
                    {
                        return;
                    }
                }

                _isDead = true;
                Died?.Invoke();
            }
        }

        public PlayerPartRuntimeState GetPartRuntimeState(PlayerPartEffectBase effect)
        {
            if (!_partStates.TryGetValue(effect, out PlayerPartRuntimeState state))
            {
                state = new PlayerPartRuntimeState();
                _partStates[effect] = state;
            }

            return state;
        }

        public void QueueAttackShot(EnemyInstance target, float damage, float delay)
        {
            if (delay <= 0f)
            {
                ResolveAttackShot(target, damage);
                return;
            }

            _queuedAttackShots.Add(new PlayerAttackShot
            {
                target = target,
                damage = damage,
                delay = delay,
            });
        }

        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        public void GrantInvulnerability(float duration)
        {
            _invulnerabilityRemaining = Mathf.Max(_invulnerabilityRemaining, duration);
        }

        public void RestoreFromPartEffect(float healthRatio, float invulnerabilityDuration)
        {
            _isDead = false;
            CurrentHealth = Mathf.Max(0.01f, MaxHealth * Mathf.Clamp01(healthRatio));
            GrantInvulnerability(invulnerabilityDuration);
        }

        public void BeginForcedDash(float duration)
        {
            _forcedDashDirection = LastMoveInput.sqrMagnitude > 0.0001f
                ? LastMoveInput.normalized
                : _forcedDashDirection;
            _forcedDashRemaining = Mathf.Max(_forcedDashRemaining, duration);
        }

        public bool TryPreventBaseDefeat(BaseController baseController)
        {
            PlayerPartContext context = MakePartContext(0f);
            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                if (effect.TryPreventBaseDefeat(context, baseController))
                {
                    return true;
                }
            }

            return false;
        }

        private void TickPartEffects(float deltaTime)
        {
            PlayerPartContext context = MakePartContext(deltaTime);
            foreach (PlayerPartEffectBase effect in _partEffects)
            {
                effect.OnTick(context);
            }
        }

        private void TickQueuedAttackShots(float deltaTime)
        {
            for (int index = _queuedAttackShots.Count - 1; index >= 0; index--)
            {
                PlayerAttackShot shot = _queuedAttackShots[index];
                shot.delay -= deltaTime;
                if (shot.delay > 0f)
                {
                    continue;
                }

                _queuedAttackShots.RemoveAt(index);
                EnemyInstance target = shot.target != null && shot.target.IsAlive
                    ? shot.target
                    : EnemyTargeting.FindNearestInRange(
                        _enemiesInRange, transform.position, data.attackRange);
                ResolveAttackShot(target, shot.damage);
            }
        }

        private void ResolveAttackShot(EnemyInstance target, float damage)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            target.TakeDamage(damage, transform.position);
            FakeProjectile.Spawn(fakeProjectilePrefab, transform.position, target.position, data);
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayPlayerAttack();
            }
        }

        private PlayerPartContext MakePartContext(float deltaTime)
        {
            return new PlayerPartContext
            {
                self = this,
                deltaTime = deltaTime,
                activeEnemies = _enemiesInRange,
            };
        }

    }
}
