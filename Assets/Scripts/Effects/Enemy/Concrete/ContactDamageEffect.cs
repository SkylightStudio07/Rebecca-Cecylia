using RCCom.Core;
using RCCom.Effects.Enemy;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Enemy.Concrete
{
    /// <summary>
    /// 베이스 3종(일반/돌진/탱커)이 공유하는 기본 접촉 피해 효과. GDD "다이" 섹션 반영:
    /// 적은 별도 어그로/타겟팅 없이 경로만 따라가다가, 근접 판정이 뜬 순간 1회성 고정 피해만 입힌다.
    /// "언제 부딪혔는지" 판정(거리 체크/트리거)은 매니저 책임이고, 이 효과는 판정된 순간에만 반응한다.
    /// 동일 대상 연속 피격 방지(피격 후 짧은 무적)는 피격 대상(플레이어) 쪽 IDamageable 구현이
    /// 스스로 처리할 책임 — 여기서는 관여하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Enemy/Effects/Contact Damage Effect")]
    public class ContactDamageEffect : EnemyEffectBase
    {
        public override void OnDealContactDamage(EnemyContext ctx, IDamageable target)
        {
            target.TakeDamage(ctx.self.Data.contactDamage, ctx.self.position);
        }
    }
}
