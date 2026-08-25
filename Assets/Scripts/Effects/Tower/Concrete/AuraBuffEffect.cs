using System.Collections.Generic;
using RCCom.Data;
using RCCom.Effects.Tower;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Tower.Concrete
{
    /// <summary>
    /// 스킬 타워 기본 효과: 사거리 내 공격 타워에게 상시 공격력/공속 버프를 준다.
    /// 사거리 이탈 시 자동으로 사라진다 (ITowerAura 파이프라인 조회 방식).
    ///
    /// 주의: 이 효과 자산(SO)은 여러 스킬 타워 인스턴스가 공유하는 상태 없는(stateless) 자산이어야
    /// 하지만, ITowerAura.ModifyOutgoingDamage(float)는 파라미터가 baseDamage 하나뿐이라
    /// "어느 스킬 타워가 준 버프인지"를 그 자리에서 알 수 없다. 그래서 (제공자, 대상) 쌍으로
    /// 버프 인스턴스를 감싸 이 효과 자산 안에 임시로 보관한다 — 같은 정의(Definition)를 쓰는
    /// 스킬 타워가 여러 대 지어져도 서로 값이 섞이지 않게 하기 위함.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Tower/Effects/Aura Buff Effect")]
    public class AuraBuffEffect : TowerEffectBase
    {
        private readonly Dictionary<(TowerInstance provider, TowerInstance ally), SimpleTowerAura> _activeAuras = new();

        public override void OnAllyEnterRange(TowerContext ctx, TowerInstance ally)
        {
            if (ctx.self.Data is not SkillTowerData data)
            {
                return;
            }

            var aura = new SimpleTowerAura(data.damageBuffMultiplier, data.attackSpeedBuffMultiplier);
            _activeAuras[(ctx.self, ally)] = aura;
            ally.activeAuras.Add(aura);
        }

        public override void OnAllyExitRange(TowerContext ctx, TowerInstance ally)
        {
            (TowerInstance, TowerInstance) key = (ctx.self, ally);
            if (!_activeAuras.TryGetValue(key, out SimpleTowerAura aura))
            {
                return;
            }

            ally.activeAuras.Remove(aura);
            _activeAuras.Remove(key);
        }
    }
}
