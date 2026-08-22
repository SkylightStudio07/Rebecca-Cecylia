using System;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 타워 설치와 아군 배치가 같은 포인터 입력을 동시에 소유하지 않도록 씬 범위 모드를 조율한다.
    /// 개체를 관리하는 Manager가 아니라 두 입력 Controller 사이의 작은 상호 배제 경계다.
    /// </summary>
    public class DeploymentInputModeController : MonoBehaviour
    {
        public DeploymentInputMode CurrentMode { get; private set; }

        public event Action<DeploymentInputMode> ModeChanged;

        public static DeploymentInputModeController Resolve(DeploymentInputModeController configured)
        {
            if (configured != null)
            {
                return configured;
            }

            DeploymentInputModeController existing =
                FindFirstObjectByType<DeploymentInputModeController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            // 씬 YAML을 직접 고치지 않아도 기존 씬에서 즉시 동작하게 하되, static 캐시는 두지
            // 않아 Retry로 씬이 다시 만들어질 때 입력 상태도 새 Controller와 함께 초기화한다.
            var modeObject = new GameObject(nameof(DeploymentInputModeController));
            return modeObject.AddComponent<DeploymentInputModeController>();
        }

        public void EnterMode(DeploymentInputMode mode)
        {
            if (mode == CurrentMode)
            {
                return;
            }

            CurrentMode = mode;
            ModeChanged?.Invoke(CurrentMode);
        }

        public void ClearMode(DeploymentInputMode ownerMode)
        {
            // 이전 모드의 늦은 취소 요청이 새 모드까지 꺼버리지 않도록 소유 모드가 같을 때만 해제한다.
            if (CurrentMode == ownerMode)
            {
                EnterMode(DeploymentInputMode.None);
            }
        }
    }
}
