using System;
using UnityEngine;

namespace RCCom.Data
{
    /// <summary>
    /// 적 한 종류(EnemyKind)의 스탯 원본 데이터. MonoBehaviour/ScriptableObject가 아닌 순수 데이터 컨테이너.
    /// 실제 값은 이후 SO(EnemyDefinition) 단계에서 인스펙터로 채운다.
    /// </summary>
    [Serializable]
    public class EnemyData
    {
        public string enemyId;
        public string displayName;
        public EnemyKind kind;

        [Header("전투 스탯")]
        public float maxHealth;
        public float moveSpeed;
        public float contactDamage;
        public float attackRange;
        public float attackInterval = 1f;

        [Header("웨이브 예산 시스템 (GDD 확정값)")]
        public float waveCost;
        public int minWave;

        [Header("처치 보상")]
        public int goldReward;
        public int expReward;
    }
}
