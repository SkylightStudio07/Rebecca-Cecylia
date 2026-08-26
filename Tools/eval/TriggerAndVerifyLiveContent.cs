var button = UnityEngine.Object.FindFirstObjectByType<RCCom.UI.LiveContentButton>(
    UnityEngine.FindObjectsInactive.Include);
if (button == null)
{
    throw new System.InvalidOperationException("TitleScene에서 LiveContentButton을 찾지 못했습니다.");
}

button.OnClick();
UnityEngine.Debug.Log("[LiveContentVerifier] 사용자 클릭으로 원격 갱신 요청 시작");
