RCCom.UI.OperatorManagementUI ui = UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorManagementUI>(
    UnityEngine.FindObjectsInactive.Include);
if (ui == null) { throw new System.InvalidOperationException("OperatorManagementUI를 찾지 못했습니다."); }
UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(ui);
UnityEngine.UI.Button button = serialized.FindProperty("deployButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.UI.Image image = button != null ? button.GetComponent<UnityEngine.UI.Image>() : null;
UnityEngine.UI.SpriteState state = button != null ? button.spriteState : default;
if (button == null || image == null || image.sprite == null ||
    image.sprite.name != "OperatorManagementDeployButtonSheet_0" ||
    state.highlightedSprite == null || state.highlightedSprite.name != "OperatorManagementDeployButtonSheet_1" ||
    state.pressedSprite == null || state.pressedSprite.name != "OperatorManagementDeployButtonSheet_1" ||
    state.selectedSprite == null || state.selectedSprite.name != "OperatorManagementDeployButtonSheet_0" ||
    state.disabledSprite == null || state.disabledSprite.name != "OperatorManagementDeployButtonSheet_0")
{
    throw new System.InvalidOperationException("Deploy 버튼 SpriteSwap 상태 배선이 올바르지 않습니다.");
}
UnityEngine.Debug.Log("[OperatorDeploy] Normal/Selected/Disabled _0, Highlighted/Pressed _1 검증 통과");
