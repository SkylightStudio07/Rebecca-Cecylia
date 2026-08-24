// 실제 URP 카메라에서 범위 파동을 확인하기 위한 일회성 Play Mode 프리뷰다.
// 씬 에셋은 저장하지 않으며 Play Mode를 끝내면 생성물과 카메라 변경이 모두 사라진다.
if (!UnityEngine.Application.isPlaying)
{
    throw new System.InvalidOperationException("Range Pulse 프리뷰는 Play Mode에서 실행해야 합니다.");
}

RCCom.Definitions.Unit.AllyUnitDefinition definition =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Unit.AllyUnitDefinition>(
        "Assets/Data/AllyUnits/calliste-drone/AllyUnitDefinition.asset");
UnityEngine.GameObject prefab =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
        "Assets/Data/Prefabs/AllyUnitView.prefab");

if (definition == null || prefab == null || definition.visualEffects == null ||
    definition.visualEffects.Count != 1)
{
    throw new System.InvalidOperationException("Calliste 드론 Definition 또는 범위 비주얼 연결이 올바르지 않습니다.");
}

foreach (UnityEngine.Camera existingCamera in
         UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(
             UnityEngine.FindObjectsInactive.Include,
             UnityEngine.FindObjectsSortMode.None))
{
    existingCamera.enabled = false;
}

foreach (UnityEngine.Canvas canvas in
         UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(
             UnityEngine.FindObjectsInactive.Include,
             UnityEngine.FindObjectsSortMode.None))
{
    canvas.enabled = false;
}

var cameraObject = new UnityEngine.GameObject("RangePulseAuraPreviewCamera");
UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
camera.enabled = true;
camera.orthographic = true;
camera.orthographicSize = 11.5f;
camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
camera.backgroundColor = new UnityEngine.Color(0.008f, 0.015f, 0.035f, 1f);
camera.depth = 1000f;
cameraObject.transform.position = new UnityEngine.Vector3(0f, 0f, -10f);

var unit = new RCCom.Runtime.AllyUnitInstance();
unit.Spawn(
    definition,
    new[] { new UnityEngine.Vector2(-1f, 0f), UnityEngine.Vector2.zero });

UnityEngine.GameObject viewObject = UnityEngine.Object.Instantiate(prefab);
viewObject.name = "RangePulseAuraPreviewUnit";
RCCom.Runtime.AllyUnitView view = viewObject.GetComponent<RCCom.Runtime.AllyUnitView>();
view.Bind(unit);

System.Reflection.FieldInfo runtimeField = typeof(RCCom.Runtime.AllyUnitView).GetField(
    "_visualRuntimes",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var runtimes = runtimeField?.GetValue(view) as
    System.Collections.Generic.List<RCCom.Runtime.Visuals.IAllyUnitVisualRuntime>;
if (runtimes == null || runtimes.Count != 1)
{
    throw new System.InvalidOperationException("AllyUnitView가 범위 비주얼 런타임을 만들지 못했습니다.");
}

var pulse = definition.visualEffects[0] as
    RCCom.Effects.UnitVisual.Concrete.RangePulseVisualEffect;
runtimes[0].Tick(pulse.PulseDuration * 0.5f);

// 캡처 타이밍과 무관하게 같은 중간 반경을 보여 주도록 프리뷰만 일시정지한다.
UnityEngine.Time.timeScale = 0f;
UnityEngine.Debug.Log(
    $"[RangePulseAuraPreview] 반경 {definition.data.attackRange}, " +
    $"색 {pulse.AuraColor}, 셰이더 {pulse.Material.shader.name} 프리뷰 준비 완료");
