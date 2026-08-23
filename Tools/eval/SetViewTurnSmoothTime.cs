// 적/아군 View 프리팹의 회전 보간 시간 상수를 설정한다.
// 목표 각도가 웨이포인트 단위 계단 함수라, 정점을 촘촘하게 만들어도 회전은 여전히 계단이다
// (간격 0.25·속도 3 기준 약 3.6도 스텝이 초당 12회 = 눈에 띄는 래칫). 밀도가 아니라 시간축
// 필터로만 해결되므로 View에 지수 감쇠를 켠다.
const float turnSmoothTime = 0.1f;

string[] targets =
{
    "Assets/Data/Prefabs/EnemyView_Normal.prefab",
    "Assets/Data/Prefabs/AllyUnitView.prefab",
};

foreach (string path in targets)
{
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
    if (prefab == null)
    {
        throw new System.InvalidOperationException($"프리팹을 불러오지 못했습니다: {path}");
    }

    UnityEngine.MonoBehaviour view = prefab.GetComponent<RCCom.Runtime.EnemyView>();
    if (view == null)
    {
        view = prefab.GetComponent<RCCom.Runtime.AllyUnitView>();
    }

    if (view == null)
    {
        throw new System.InvalidOperationException($"View 컴포넌트를 찾지 못했습니다: {path}");
    }

    var serialized = new UnityEditor.SerializedObject(view);
    var property = serialized.FindProperty("turnSmoothTime");
    if (property == null)
    {
        throw new System.InvalidOperationException($"turnSmoothTime 필드를 찾지 못했습니다: {path}");
    }

    float before = property.floatValue;
    property.floatValue = turnSmoothTime;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(view);

    UnityEngine.Debug.Log($"[SetViewTurnSmoothTime] {path}: turnSmoothTime {before} → {property.floatValue}");
}

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
