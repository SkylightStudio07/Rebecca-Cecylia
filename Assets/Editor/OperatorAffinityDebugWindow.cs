using RCCom.Core;
using RCCom.Data;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 대사 에셋을 기다리지 않고 호감도 경계와 귀환 보상 흐름을 검증하는 Editor 전용 창.
    /// PlayerPrefs를 직접 만지지 않고 런타임과 같은 IProfileStorage 경로를 사용한다.
    /// </summary>
    public sealed class OperatorAffinityDebugWindow : EditorWindow
    {
        private string _operatorId = "cassia";
        private int _affinity;
        private IProfileStorage _storage;

        [MenuItem("RCCom/Debug/Operator Affinity")]
        public static void Open()
        {
            GetWindow<OperatorAffinityDebugWindow>("Operator Affinity");
        }

        private void OnEnable()
        {
            _storage = new PlayerPrefsProfileStorage();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("호감도 디버그", EditorStyles.boldLabel);
            _operatorId = EditorGUILayout.TextField("Operator ID", _operatorId);
            _affinity = EditorGUILayout.IntSlider("Affinity", _affinity, 0,
                PlayerProfile.MaxOperatorAffinity);

            if (GUILayout.Button("현재 프로필 불러오기"))
            {
                _affinity = _storage.Load().GetOperatorAffinity(_operatorId);
            }

            if (GUILayout.Button("호감도 설정"))
            {
                PlayerProfile profile = _storage.Load();
                profile.SetOperatorAffinity(_operatorId, _affinity);
                _storage.Save(profile);
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("귀환 보상 예약"))
            {
                PlayerProfile profile = _storage.Load();
                profile.QueueBattleReturn(_operatorId);
                _storage.Save(profile);
            }

            if (GUILayout.Button("귀환 보상 초기화"))
            {
                PlayerProfile profile = _storage.Load();
                profile.pendingReturnOperatorId = string.Empty;
                profile.pendingReturnCount = 0;
                _storage.Save(profile);
            }

            PlayerProfile current = _storage.Load();
            EditorGUILayout.HelpBox(
                $"현재 {current.GetOperatorAffinity(_operatorId)}/100\n" +
                $"단계: {current.GetOperatorAffinityTier(_operatorId)}\n" +
                $"대기 귀환: {current.pendingReturnOperatorId} × {current.pendingReturnCount}",
                MessageType.Info);
        }
    }
}
