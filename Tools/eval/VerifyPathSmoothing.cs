// 웨이포인트 경로 스플라인 베이킹 검증기와 기존 아군 전투 코어 검증기를 한 번에 돌린다.
// 스플라인 베이킹은 MapManager가 넘기는 경로 배열의 정점 수를 크게 늘리므로, 진행도 캐시를
// 포함한 전투 회귀까지 같이 확인해야 의미가 있다.
RCCom.EditorTools.PathSmoothingVerifier.Verify();
RCCom.EditorTools.AllyUnitCombatVerifier.Verify();

// 실제 씬의 MapManager가 지금 어떤 값으로 몇 개의 정점을 만들어내는지도 같이 찍는다 —
// 코드가 통과해도 인스펙터 값이 0이면 곡선이 실제로는 적용되지 않기 때문이다.
var mapManager = UnityEngine.Object.FindFirstObjectByType<RCCom.Managers.MapManager>();
if (mapManager == null)
{
    UnityEngine.Debug.Log("[VerifyPathSmoothing] 열린 씬에 MapManager가 없어 인스펙터 값 확인은 건너뜀.");
}
else
{
    var serialized = new UnityEditor.SerializedObject(mapManager);
    float smoothness = serialized.FindProperty("pathSmoothness").floatValue;
    float spacing = serialized.FindProperty("maxPointSpacing").floatValue;
    var waypoints = serialized.FindProperty("waypoints");

    var controlPoints = new System.Collections.Generic.List<UnityEngine.Vector2>();
    for (int i = 0; i < waypoints.arraySize; i++)
    {
        var transformRef = waypoints.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.Transform;
        if (transformRef != null)
        {
            controlPoints.Add(transformRef.position);
        }
    }

    var baked = RCCom.Core.PathSmoothing.GenerateSmoothPath(controlPoints, smoothness, spacing);
    UnityEngine.Debug.Log(
        $"[VerifyPathSmoothing] 씬 MapManager: pathSmoothness={smoothness}, maxPointSpacing={spacing}, " +
        $"제어점 {controlPoints.Count}개 → 베이킹 정점 {baked.Length}개");
}
