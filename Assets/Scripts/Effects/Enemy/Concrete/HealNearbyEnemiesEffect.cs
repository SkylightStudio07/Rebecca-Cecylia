using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.Enemy.Concrete
{
    /// <summary>
    /// 공격 대신 자신의 attackRange 안에 있는 다른 적을 attackInterval 주기로 회복한다.
    /// 주기 상태는 공유 SO가 아니라 EnemyInstance가 소유해 여러 힐러가 같은 에셋을 안전하게 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Enemy/Effects/Heal Nearby Enemies Effect")]
    public sealed class HealNearbyEnemiesEffect : EnemyEffectBase
    {
        [Tooltip("한 주기마다 범위 안의 각 적에게 적용할 회복량")]
        [SerializeField, Min(0f)] private float healAmount = 5f;

        [Tooltip("활성화하면 힐러 자신도 회복 대상에 포함합니다.")]
        [SerializeField] private bool includeSelf;

        [Header("회복 연출")]
        [Tooltip("회복 발동 순간 힐러 중심에서 한 번 퍼지는 공용 원형 파장 프리팹")]
        [SerializeField] private GameObject healingPulsePrefab;

        [Tooltip("파장의 색·두께·확산 시간을 조절하는 힐러 전용 시각 설정")]
        [SerializeField] private ShockwaveRingVisualEffect healingPulseVisual;

        public override void OnTick(EnemyContext ctx)
        {
            if (ctx == null || ctx.self == null || ctx.activeEnemies == null)
            {
                return;
            }

            float interval = ctx.self.Data.attackInterval;
            if (!ctx.self.TryConsumeEffectInterval(this, ctx.deltaTime, interval))
            {
                return;
            }

            float range = Mathf.Max(0f, ctx.self.Data.attackRange);
            float rangeSquared = range * range;
            for (int i = 0; i < ctx.activeEnemies.Count; i++)
            {
                EnemyInstance target = ctx.activeEnemies[i];
                if (target == null || !target.IsAlive || (!includeSelf && target == ctx.self))
                {
                    continue;
                }

                if ((target.position - ctx.self.position).sqrMagnitude > rangeSquared)
                {
                    continue;
                }

                target.Heal(healAmount);
            }

            // 회복 대상의 현재 체력과 무관하게 스킬 주기가 발동했음을 보여준다. 판정과 연출을
            // 분리해 VFX 참조가 비어 있어도 실제 회복은 그대로 동작한다.
            ShockwaveRing.Spawn(
                healingPulsePrefab,
                new Vector3(ctx.self.position.x, ctx.self.position.y, 0f),
                range,
                healingPulseVisual);
        }
    }
}
