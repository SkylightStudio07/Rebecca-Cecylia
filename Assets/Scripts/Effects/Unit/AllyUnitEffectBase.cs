using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit
{
    /// <summary>
    /// IAllyUnitEffect의 편의 기본 구현. 구체 효과는 필요한 훅만 재정의한다.
    /// </summary>
    public abstract class AllyUnitEffectBase : ScriptableObject, IAllyUnitEffect
    {
        public virtual void OnSpawn(AllyUnitContext ctx) { }
        public virtual void OnTick(AllyUnitContext ctx) { }
        public virtual void OnAttack(AllyUnitContext ctx, EnemyInstance target) { }
        public virtual void OnDeath(AllyUnitContext ctx) { }
    }
}
