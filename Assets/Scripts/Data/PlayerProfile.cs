using System;
using System.Collections.Generic;

namespace RCCom.Data
{
    /// <summary>
    /// 씬 재시작으로 사라지는 전투 세션과 분리해 보존하는 계정 데이터의 모양.
    /// 해금 여부는 bestWave와 OperatorDefinition.requiredBestWave로 계산하므로 같은 상태를
    /// 목록으로 중복 저장하지 않는다 — 두 값이 어긋나 잠금 상태가 모순되는 일을 막기 위함이다.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public const int CurrentSchemaVersion = 2;

        public const int MaxOperatorAffinity = 100;
        public const int ReturnAffinityWithoutParticipation = 2;
        public const int ReturnAffinityWithParticipation = 5;

        public int schemaVersion = CurrentSchemaVersion;
        public int bestWave;
        public string selectedOperatorId = string.Empty;

        /// <summary>
        /// JsonUtility는 Dictionary를 직렬화하지 않으므로 목록으로 저장한다.
        /// 목록 항목이 없는 오퍼레이터는 호감도 0으로 간주한다.
        /// </summary>
        public List<OperatorAffinityRecord> operatorAffinities = new List<OperatorAffinityRecord>();

        /// <summary>
        /// 결과 화면에서 귀환한 오퍼레이터. 실제 보상은 로비에서 해당 오퍼레이터를
        /// 클릭할 때 정산해, 전투 직후 자동으로 호감도가 오르는 것을 막는다.
        /// </summary>
        public string pendingReturnOperatorId = string.Empty;
        public int pendingReturnCount;

        /// <summary>
        /// 결과 화면이 전달한 도달 웨이브를 누적 최고 기록에 반영한다.
        /// 저장 호출 여부를 소비자가 판단할 수 있게 실제로 기록이 갱신됐을 때만 true를 반환한다.
        /// </summary>
        public bool TryRecordBestWave(int reachedWave)
        {
            int normalizedWave = Math.Max(0, reachedWave);
            if (normalizedWave <= bestWave)
            {
                return false;
            }

            bestWave = normalizedWave;
            return true;
        }

        public int GetOperatorAffinity(string operatorId)
        {
            OperatorAffinityRecord record = FindAffinityRecord(operatorId);
            return record == null ? 0 : ClampAffinity(record.affinity);
        }

        public void SetOperatorAffinity(string operatorId, int value)
        {
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return;
            }

            OperatorAffinityRecord record = FindOrCreateAffinityRecord(operatorId);
            record.affinity = ClampAffinity(value);
        }

        public int AddOperatorAffinity(string operatorId, int amount)
        {
            int next = GetOperatorAffinity(operatorId) + amount;
            SetOperatorAffinity(operatorId, next);
            return GetOperatorAffinity(operatorId);
        }

        /// <summary>
        /// 결과 화면이 호출하는 귀환 보상 예약. 같은 판에서 게임오버 이벤트가 중복
        /// 전달되더라도 GameManager의 가드와 별개로 한 번만 예약되도록 호출자는
        /// 한 번만 실행하며, Retry 후 여러 판을 마치면 count로 합산한다.
        /// </summary>
        public void QueueBattleReturn(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return;
            }

            if (!string.Equals(pendingReturnOperatorId, operatorId, StringComparison.Ordinal))
            {
                pendingReturnOperatorId = operatorId;
                pendingReturnCount = 0;
            }

            pendingReturnCount = Math.Max(0, pendingReturnCount) + 1;
        }

        /// <summary>
        /// 로비 클릭 한 번으로 미수령 귀환 보상을 소비한다. 참전 오퍼레이터를 클릭하면
        /// +5, 다른 오퍼레이터를 클릭하면 +2이며, 현재 로비는 참전 오퍼레이터를
        /// 표시하므로 기본 흐름은 +5다.
        /// </summary>
        public bool TryClaimBattleReturn(string interactedOperatorId, out int grantedAffinity,
            out bool participated)
        {
            grantedAffinity = 0;
            participated = false;
            if (string.IsNullOrWhiteSpace(interactedOperatorId) || pendingReturnCount <= 0 ||
                string.IsNullOrWhiteSpace(pendingReturnOperatorId))
            {
                return false;
            }

            participated = string.Equals(pendingReturnOperatorId, interactedOperatorId,
                StringComparison.Ordinal);
            int perReturn = participated
                ? ReturnAffinityWithParticipation
                : ReturnAffinityWithoutParticipation;
            grantedAffinity = perReturn * pendingReturnCount;
            AddOperatorAffinity(interactedOperatorId, grantedAffinity);

            pendingReturnOperatorId = string.Empty;
            pendingReturnCount = 0;
            return true;
        }

        public OperatorAffinityTier GetOperatorAffinityTier(string operatorId)
        {
            int value = GetOperatorAffinity(operatorId);
            if (value >= 75)
            {
                return OperatorAffinityTier.Love;
            }

            if (value >= 50)
            {
                return OperatorAffinityTier.Joy;
            }

            if (value >= 25)
            {
                return OperatorAffinityTier.Favorable;
            }

            return OperatorAffinityTier.Unfamiliar;
        }

        private OperatorAffinityRecord FindAffinityRecord(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || operatorAffinities == null)
            {
                return null;
            }

            for (int i = 0; i < operatorAffinities.Count; i++)
            {
                OperatorAffinityRecord record = operatorAffinities[i];
                if (record != null && string.Equals(record.operatorId, operatorId,
                        StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        private OperatorAffinityRecord FindOrCreateAffinityRecord(string operatorId)
        {
            operatorAffinities ??= new List<OperatorAffinityRecord>();
            OperatorAffinityRecord existing = FindAffinityRecord(operatorId);
            if (existing != null)
            {
                return existing;
            }

            var created = new OperatorAffinityRecord { operatorId = operatorId };
            operatorAffinities.Add(created);
            return created;
        }

        private static int ClampAffinity(int value)
        {
            return Math.Max(0, Math.Min(MaxOperatorAffinity, value));
        }
    }
}
