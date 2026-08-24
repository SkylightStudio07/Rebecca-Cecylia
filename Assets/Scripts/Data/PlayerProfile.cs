using System;
using System.Collections.Generic;

namespace RCCom.Data
{
    /// <summary>
    /// 씬 재시작으로 사라지는 전투 세션과 분리해 보존하는 계정 데이터의 모양.
    /// 계산 가능한 웨이브·스테이지 조건은 진행 기록에서 판정하고, 구매처럼 명시적 소유가
    /// 필요한 조건만 ID 목록으로 저장한다. 표시 연출 이력은 소유 상태와 별도로 유지한다.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public const int CurrentSchemaVersion = 5;

        public const int MaxOperatorAffinity = 100;
        public const int ReturnAffinityWithoutParticipation = 2;
        public const int ReturnAffinityWithParticipation = 5;

        public int schemaVersion = CurrentSchemaVersion;
        public int bestWave;
        /// <summary>
        /// 전투 결과로 누적되는 계정 재화. 전투 중에 소비하는 GameManager.Gold와 달리
        /// 씬을 넘어도 유지되는 값이므로 PlayerProfile만 원본으로 둔다.
        /// </summary>
        public int commodity;
        public string selectedOperatorId = string.Empty;

        /// <summary>
        /// JsonUtility는 Dictionary를 직렬화하지 않으므로 목록으로 저장한다.
        /// 목록 항목이 없는 오퍼레이터는 호감도 0으로 간주한다.
        /// </summary>
        public List<OperatorAffinityRecord> operatorAffinities = new List<OperatorAffinityRecord>();

        /// <summary>
        /// 해금 여부 자체는 bestWave에서 계속 계산한다. 이 목록은 해금 상태를 중복 저장하는
        /// 값이 아니라, 획득 연출을 이미 끝까지 본 오퍼레이터만 기록해 재접속 때 같은 연출이
        /// 반복되는 것을 막는 표시 이력이다.
        /// </summary>
        public List<string> presentedOperatorAcquisitionIds = new List<string>();

        /// <summary>
        /// 구매처럼 조건을 만족한 뒤에도 소유 상태를 보존해야 하는 오퍼레이터 목록.
        /// 웨이브·스테이지 조건은 각 진행 기록에서 계산하므로 여기에 중복 저장하지 않는다.
        /// </summary>
        public List<string> acquiredOperatorIds = new List<string>();

        /// <summary>최초 클리어 여부와 스테이지 보상 판정에 사용하는 영구 Stage ID 목록.</summary>
        public List<string> clearedStageIds = new List<string>();

        /// <summary>
        /// 결과 화면에서 귀환한 오퍼레이터. 실제 보상은 메인 로비가 열린 뒤 정산해,
        /// 전투 결과 화면에서 즉시 호감도가 오르는 것을 막는다.
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

        public int AddCommodity(int amount)
        {
            commodity = Math.Max(0, commodity + Math.Max(0, amount));
            return commodity;
        }

        public bool TrySpendCommodity(int amount)
        {
            int normalizedAmount = Math.Max(0, amount);
            if (commodity < normalizedAmount)
            {
                return false;
            }

            commodity -= normalizedAmount;
            return true;
        }

        public bool HasAcquiredOperator(string operatorId)
        {
            return ContainsId(acquiredOperatorIds, operatorId);
        }

        public bool AcquireOperator(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || HasAcquiredOperator(operatorId))
            {
                return false;
            }

            acquiredOperatorIds ??= new List<string>();
            acquiredOperatorIds.Add(operatorId);
            return true;
        }

        public bool TryPurchaseOperator(string operatorId, int price)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || HasAcquiredOperator(operatorId) ||
                !TrySpendCommodity(price))
            {
                return false;
            }

            return AcquireOperator(operatorId);
        }

        public bool HasClearedStage(string stageId)
        {
            return ContainsId(clearedStageIds, stageId);
        }

        public bool MarkStageCleared(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId) || HasClearedStage(stageId))
            {
                return false;
            }

            clearedStageIds ??= new List<string>();
            clearedStageIds.Add(stageId);
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
        /// 로비 진입 또는 클릭 폴백에서 미수령 귀환 보상을 소비한다. 참전 오퍼레이터면
        /// +5, 다른 오퍼레이터면 +2이며, 현재 로비는 참전 오퍼레이터를 표시하므로
        /// 기본 흐름은 +5다.
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
            int previousAffinity = GetOperatorAffinity(interactedOperatorId);
            AddOperatorAffinity(interactedOperatorId, perReturn * pendingReturnCount);
            // 상한에 막힌 값을 알림에 표시하지 않도록 요청량이 아닌 실제 증가량을 반환한다.
            grantedAffinity = GetOperatorAffinity(interactedOperatorId) - previousAffinity;

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

        public bool HasPresentedOperatorAcquisition(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || presentedOperatorAcquisitionIds == null)
            {
                return false;
            }

            return presentedOperatorAcquisitionIds.Exists(id =>
                string.Equals(id, operatorId, StringComparison.Ordinal));
        }

        public bool MarkOperatorAcquisitionPresented(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId) || HasPresentedOperatorAcquisition(operatorId))
            {
                return false;
            }

            presentedOperatorAcquisitionIds ??= new List<string>();
            presentedOperatorAcquisitionIds.Add(operatorId);
            return true;
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

        private static bool ContainsId(List<string> ids, string id)
        {
            return !string.IsNullOrWhiteSpace(id) && ids != null && ids.Exists(candidate =>
                string.Equals(candidate, id, StringComparison.Ordinal));
        }
    }
}
