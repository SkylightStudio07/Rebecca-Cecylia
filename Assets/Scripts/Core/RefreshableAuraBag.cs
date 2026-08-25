using System.Collections.Generic;

namespace RCCom.Core
{
    /// <summary>
    /// (소스, 효과) 키로 값을 저장하고 지속시간이 지나면 자동 만료시키는 범용 버프 저장소.
    /// 같은 (소스, 효과) 조합의 매 틱 재적용은 한 항목만 연장(갱신)하고, 서로 다른 조합은
    /// 별개 항목으로 유지된다 — 오라형 Effect가 매 틱 사거리 내 대상에게 짧은 버프를 계속
    /// 갱신하는 관례(AllyUnitInstance.ApplyStatMultipliers가 원형)를 일반화한 것이다.
    ///
    /// SO(효과 에셋)는 여러 인스턴스가 공유하는 상태 없는 자산이어야 하므로, 이 저장소는 항상
    /// 버프를 받는 쪽의 런타임 인스턴스(AllyUnitInstance, TowerInstance 등)가 소유한다.
    /// </summary>
    public sealed class RefreshableAuraBag<TSource, TEffect, TValue>
    {
        private readonly Dictionary<(TSource source, TEffect effect), TValue> _values = new();
        private readonly Dictionary<(TSource source, TEffect effect), float> _expirations = new();
        private readonly List<(TSource source, TEffect effect)> _expiredKeys = new();
        private float _time;

        public int Count => _values.Count;
        public IEnumerable<TValue> Values => _values.Values;

        /// <summary>값을 설정(또는 갱신)한다. duration이 0 이하면 아무것도 하지 않는다(즉시 만료 취급).</summary>
        public void Set(TSource source, TEffect effect, TValue value, float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            var key = (source, effect);
            _values[key] = value;
            _expirations[key] = _time + duration;
        }

        /// <summary>내부 시계를 진행시키고 만료된 항목을 제거한다. 매 틱 한 번 호출한다.</summary>
        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            if (_values.Count == 0)
            {
                return;
            }

            _expiredKeys.Clear();
            foreach (KeyValuePair<(TSource source, TEffect effect), float> pair in _expirations)
            {
                if (pair.Value <= _time)
                {
                    _expiredKeys.Add(pair.Key);
                }
            }

            foreach ((TSource source, TEffect effect) key in _expiredKeys)
            {
                _values.Remove(key);
                _expirations.Remove(key);
            }
        }
    }
}
