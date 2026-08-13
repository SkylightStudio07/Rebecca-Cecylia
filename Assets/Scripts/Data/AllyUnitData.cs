using System;
using UnityEngine;

namespace RCCom.Data
{
    /// <summary>
    /// 모든 아군 유닛이 공유하는 스탯 모양. 유닛 종류별 행동 차이는 파생 클래스가 아니라
    /// AllyUnitDefinition의 효과 SO 조립으로 만든다 — 신규 오퍼레이터가 새 C# 타입을 요구하지 않게 하기 위함이다.
    /// </summary>
    [Serializable]
    public class AllyUnitData
    {
        [Header("식별")]
        public string unitId;
        public string displayName;

        [Header("배치")]
        [Min(0)] public int deployCost;

        [Header("기본 스탯")]
        [Min(0.01f)] public float maxHealth = 1f;
        [Min(0f)] public float moveSpeed = 1f;
        [Min(0f)] public float attackDamage = 1f;
        [Min(0.01f)] public float attackInterval = 1f;
        [Min(0f)] public float attackRange = 1f;
        [Min(0f)] public float detectionRange = 2f;
        [Min(0f)] public float projectileSpeed = 8f;
    }
}
