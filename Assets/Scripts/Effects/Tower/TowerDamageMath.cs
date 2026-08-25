using RCCom.Runtime;

namespace RCCom.Effects.Tower
{
    /// <summary>
    /// 공격 타워 계열 효과(DamageEffect, PierceDamageEffect 등)가 공유하는 데미지/공격주기
    /// 파이프라인 계산. 로컬 오라(activeAuras)와 전역 오라(GlobalTowerAuraRegistry) 둘 다 순회.
    /// DamageEffect에만 있던 private 메서드였는데, PierceDamageEffect도 동일 파이프라인이
    /// 필요해서 공유 유틸리티로 추출.
    /// </summary>
    public static class TowerDamageMath
    {
        public static float CalculateDamage(TowerInstance self, float baseDamage)
        {
            float damage = baseDamage;

            foreach (ITowerAura aura in self.activeAuras)
            {
                damage = aura.ModifyOutgoingDamage(damage);
            }

            foreach (ITowerAura aura in self.TemporaryAuras)
            {
                damage = aura.ModifyOutgoingDamage(damage);
            }

            foreach (ITowerAura aura in GlobalTowerAuraRegistry.Auras)
            {
                damage = aura.ModifyOutgoingDamage(damage);
            }

            return damage;
        }

        public static float CalculateAttackInterval(TowerInstance self, float baseInterval)
        {
            float interval = baseInterval;

            foreach (ITowerAura aura in self.activeAuras)
            {
                interval = aura.ModifyAttackInterval(interval);
            }

            foreach (ITowerAura aura in self.TemporaryAuras)
            {
                interval = aura.ModifyAttackInterval(interval);
            }

            foreach (ITowerAura aura in GlobalTowerAuraRegistry.Auras)
            {
                interval = aura.ModifyAttackInterval(interval);
            }

            return interval;
        }
    }
}
