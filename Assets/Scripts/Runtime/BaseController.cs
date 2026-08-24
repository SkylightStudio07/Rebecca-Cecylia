using System;
using RCCom.Core;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 거점(수비 대상). Player와 마찬가지로 개체가 하나뿐이라 그냥 MonoBehaviour로 둔다
    /// (매니저 아키텍처 원칙상 별도 BaseManager도 만들지 않음).
    ///
    /// 적이 웨이포인트 경로 끝에 도달하면 EnemyInstance가 이 컴포넌트의 TakeDamage를 직접
    /// 호출한다 (물리 충돌이 아니라 "경로 완주" 판정 — WaveManager가 스폰 시 이 컴포넌트를
    /// 목표로 넘겨줌). 플레이어처럼 물리 접촉으로도 맞을 수 있게 하려면 Collider2D를 추가로
    /// 붙여도 되지만(EnemyView가 IDamageable을 범용으로 감지하므로 자동으로 동작함), 필수는 아님.
    ///
    /// 씬에 하나뿐인 게 보장되는 오브젝트라 정적 Instance로 노출 — 재생의 축(파워 타워) 같은
    /// 효과가 ScriptableObject 안에 씬 참조를 직접 들고 있는 어색함을 피하기 위함.
    /// </summary>
    public class BaseController : MonoBehaviour, IDamageable
    {
        public static BaseController Instance { get; private set; }

        public float maxHealth;

        public float CurrentHealth { get; private set; }
        public event Action Defeated;

        /// <summary>거점이 피해를 입을 때마다 알림 — 오퍼레이터 대사 등 UI가 구독.</summary>
        public event Action<float> Damaged;

        private void Awake()
        {
            Instance = this;
            CurrentHealth = maxHealth;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void TakeDamage(float amount, Vector2? sourcePosition = null)
        {
            if (CurrentHealth <= 0f)
            {
                return;
            }

            CurrentHealth -= amount;
            Debug.Log($"[BaseDebug] 거점 피격 -{amount} (남은 체력 {CurrentHealth}/{maxHealth})"); // TODO: 확인 끝나면 삭제
            Damaged?.Invoke(amount);

            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0f;
                Defeated?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (CurrentHealth <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        /// <summary>
        /// 거점 보강 카드용 — 최대체력을 늘리고, 늘어난 만큼 현재체력도 함께 채워준다
        /// (증가분만큼 보너스 회복되는 느낌을 주기 위한 설계 선택).
        /// </summary>
        public void IncreaseMaxHealth(float multiplier)
        {
            float oldMax = maxHealth;
            maxHealth *= multiplier;
            CurrentHealth += maxHealth - oldMax;
        }
    }
}
