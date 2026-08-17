UnityEngine.Canvas canvas = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Canvas>(
    UnityEngine.FindObjectsInactive.Include);
if (canvas == null)
{
    throw new System.InvalidOperationException("TitleScene Canvas를 찾지 못했습니다.");
}

UnityEngine.Transform title = canvas.transform.Find("TitleBackground");
UnityEngine.Transform lobby = canvas.transform.Find("MainMenuBackground");
if (title == null || lobby == null)
{
    throw new System.InvalidOperationException("타이틀 또는 로비 루트를 찾지 못했습니다.");
}

title.gameObject.SetActive(false);
lobby.gameObject.SetActive(true);
UnityEngine.CanvasGroup group = lobby.GetComponent<UnityEngine.CanvasGroup>();
if (group != null)
{
    group.alpha = 1f;
    group.interactable = true;
    group.blocksRaycasts = true;
}
