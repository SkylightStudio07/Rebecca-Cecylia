using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Tower;
using RCCom.Effects.Tower;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 건설된 타워 1기. 매니저 아키텍처 원칙(ARCHITECTURE.md 5단계): 타워는 설치 슬롯 제한으로
    /// 개체 수가 적어(공격3/스킬1/파워2, 카드로 확장돼도 6~8개 수준) Enemy처럼 순수 C#으로 뺄
    /// 필요 없이 일반 MonoBehaviour로 둔다. 별도 TowerManager 없이 자기 Update/트리거 콜백에서
    /// 직접 ITowerEffect 훅을 호출한다.
    ///
    /// 프리팹은 타워 종류별로 따로 만들지 않고 1개만 재사용한다 (EnemyView와 같은 이유).
    /// MapManager가 Instantiate 직후 Build(definition)을 호출해 정의를 주입한다 — Awake에서
    /// 바로 definition.effects를 읽으면 Unity가 Instantiate 시점에 곧바로 Awake를 실행해버려서
    /// definition이 아직 비어있는 채로 호출되는 타이밍 문제가 있었기 때문에, OnBuild 훅 호출을
    /// Awake가 아니라 이 Build() 호출 시점으로 옮겼다.
    ///
    /// 아군 사거리 출입(스킬 타워)·적 사거리 진입(공격 타워) 감지는 Collider2D 트리거로 하므로,
    /// 이 프리팹과 EnemyView 양쪽에 Collider2D(+최소 한쪽에 Rigidbody2D)가 필요하다.
    /// </summary>
    public class TowerInstance : MonoBehaviour
    {
        /// <summary>
        /// 현재 씬에 살아있는 모든 타워 인스턴스. 업그레이드 카드(사거리 확장 등)가 이미
        /// 지어진 타워에도 즉시 반영되도록 순회할 대상이 필요해서 둔다 — 별도 TowerManager를
        /// 만들지 않기로 한 원칙과 절충: 매니저가 아니라 인스턴스 자기 자신을 등록/해제한다.
        /// </summary>
        public static readonly List<TowerInstance> All = new();

        public TowerDefinition definition;

        [Tooltip("아트 임포트 해상도/PPU가 에셋마다 달라도 항상 이 월드 크기(가장 긴 축 기준)로 보이도록 자동 스케일")]
        [SerializeField] private float targetVisualSize = 0.9f;

        /// <summary>
        /// 공격 효과 등 주기적 효과가 자체적으로 쓰는 타이머. 효과(SO)는 여러 타워 인스턴스가
        /// 공유하는 상태 없는(stateless) 자산이라 인스턴스별 값은 반드시 여기(런타임 쪽)에 둔다.
        /// </summary>
        public float cooldownRemaining;

        /// <summary>
        /// 자신에게 걸린 스킬/파워 타워 버프 목록. 데미지 계산 시점에만 조회하고 스탯 자체는
        /// 건드리지 않는다. 스킬 타워가 OnAllyEnterRange/OnAllyExitRange에서 Add/Remove한다.
        /// </summary>
        public List<ITowerAura> activeAuras = new();

        /// <summary>
        /// 유닛이 시전하는 임시(지속시간 기반) 오라 저장소. activeAuras(콜라이더 출입 이벤트로
        /// 상시 등록되는 스킬 타워 오라)와는 메커니즘이 달라 별개로 둔다 — 유닛은 이동하며
        /// 타워처럼 트리거 콜라이더를 갖지 않으므로, 매 틱 사거리 내 타워를 스캔해 짧은 버프를
        /// 계속 갱신하는 폴링 방식(AllyUnitInstance.ApplyStatMultipliers와 동일한 철학)만 가능하다.
        /// </summary>
        private readonly RefreshableAuraBag<object, object, ITowerAura> _temporaryAuras = new();

        private readonly List<EnemyInstance> _enemiesInRange = new();
        private bool _isBuilt;

        public TowerData Data => definition.Data;
        public Vector2 Position => transform.position;

        /// <summary>RangeIndicator가 참조 — 사거리 개념 없는 파워 타워는 0.</summary>
        public float DisplayRange => Data.DisplayRange;

        private void Awake()
        {
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        /// <summary>MapManager가 Instantiate 직후 1회 호출한다.</summary>
        public void Build(TowerDefinition towerDefinition)
        {
            definition = towerDefinition;
            _isBuilt = true;

            if (definition.sprite != null && TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                spriteRenderer.sprite = definition.sprite;
                float scale = SpriteFit.CalculateUniformScale(definition.sprite, targetVisualSize);
                transform.localScale = new Vector3(scale, scale, 1f);
            }

            RefreshRangeCollider();

            TowerContext ctx = MakeContext();
            foreach (ITowerEffect effect in definition.effects)
            {
                effect.OnBuild(ctx);
            }
        }

        private void Update()
        {
            if (!_isBuilt)
            {
                return;
            }

            _temporaryAuras.Tick(Time.deltaTime);

            TowerContext ctx = MakeContext();
            foreach (ITowerEffect effect in definition.effects)
            {
                effect.OnTick(ctx);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isBuilt)
            {
                return;
            }

            if (other.TryGetComponent(out TowerInstance ally))
            {
                TowerContext ctx = MakeContext();
                foreach (ITowerEffect effect in definition.effects)
                {
                    effect.OnAllyEnterRange(ctx, ally);
                }
            }
            else if (other.TryGetComponent(out EnemyView enemyView))
            {
                _enemiesInRange.Add(enemyView.Instance);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out TowerInstance ally))
            {
                if (_isBuilt)
                {
                    TowerContext ctx = MakeContext();
                    foreach (ITowerEffect effect in definition.effects)
                    {
                        effect.OnAllyExitRange(ctx, ally);
                    }
                }
            }
            else if (other.TryGetComponent(out EnemyView enemyView))
            {
                _enemiesInRange.Remove(enemyView.Instance);
            }
        }

        /// <summary>
        /// 유닛이 시전하는 임시 버프(예: TowerReinforcementAuraEffect)가 사용하는 진입점.
        /// source/effect는 (제공자, 효과) 키로 여러 시전자의 버프가 서로 값을 덮어쓰지 않게
        /// 구분하는 용도일 뿐, 이 클래스는 그 실제 타입(AllyUnitInstance/SO 등)을 알 필요가
        /// 없어 object로 받는다. duration 동안만 유지되며, 매 틱 다시 호출해 갱신하지 않으면
        /// 자연 만료된다.
        /// </summary>
        public void ApplyTemporaryAura(object source, object effect, ITowerAura aura, float duration)
        {
            _temporaryAuras.Set(source, effect, aura, duration);
        }

        /// <summary>TowerDamageMath가 activeAuras/GlobalTowerAuraRegistry와 나란히 조회한다.</summary>
        public IEnumerable<ITowerAura> TemporaryAuras => _temporaryAuras.Values;

        /// <summary>
        /// 사거리 관련 업그레이드 카드(사거리 확장, 오라 확장)가 Data를 바꾼 뒤 호출해서
        /// 실제 감지 반경(Collider2D)에도 반영시킨다 — Data만 바꾸면 수치는 늘어도 실제
        /// 감지 범위(물리 콜라이더)는 그대로라 카드 효과가 눈에 안 보이는 문제를 방지.
        ///
        /// 스프라이트 자동 피팅(Build 참고) 때문에 transform.localScale이 1이 아닐 수 있는데,
        /// Collider2D의 실제 월드 반경은 로컬 반지름 × lossyScale이라 그대로 두면 사거리가
        /// 아트 크기에 따라 달라지는 버그가 생긴다. 그래서 로컬 반지름을 스케일로 나눠, 항상
        /// range(월드 유닛)와 실제 판정 반경이 정확히 일치하도록 보정한다.
        /// </summary>
        public void RefreshRangeCollider()
        {
            if (!TryGetComponent(out CircleCollider2D circle))
            {
                return;
            }

            float range = Data switch
            {
                AttackTowerData attack => attack.attackRange,
                SkillTowerData skill => skill.buffRange,
                _ => -1f,
            };

            if (range >= 0f)
            {
                float scale = transform.localScale.x;
                circle.radius = scale > 0f ? range / scale : range;
            }
        }

        private TowerContext MakeContext() => new()
        {
            self = this,
            deltaTime = Time.deltaTime,
            activeEnemies = _enemiesInRange,
        };
    }
}
