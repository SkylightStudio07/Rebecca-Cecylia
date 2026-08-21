using RCCom.Definitions.Stage;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// TitleScene에서 고른 전투 모드를 DefenseScene까지 전달하는 세션 경계.
    /// 씬 재로드(Retry)에는 유지하고 새 애플리케이션 실행에서는 초기화한다.
    /// </summary>
    public static class BattleSession
    {
        public static BattleMode Mode { get; private set; } = BattleMode.Endless;
        public static StageDefinition SelectedStage { get; private set; }
        public static bool IsStageMode => Mode == BattleMode.Stage && SelectedStage != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationRun()
        {
            Mode = BattleMode.Endless;
            SelectedStage = null;
        }

        public static void SelectEndless()
        {
            Mode = BattleMode.Endless;
            SelectedStage = null;
        }

        public static void SelectStage(StageDefinition stage)
        {
            if (stage == null || !stage.IsPlayable)
            {
                throw new System.ArgumentException("실행할 수 없는 스테이지를 선택할 수 없습니다.", nameof(stage));
            }

            Mode = BattleMode.Stage;
            SelectedStage = stage;
        }
    }
}
