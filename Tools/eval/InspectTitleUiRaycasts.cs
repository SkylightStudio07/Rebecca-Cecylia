RCCom.UI.OperatorManagementUI management = UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorManagementUI>(
    UnityEngine.FindObjectsInactive.Include);
if (management == null) { throw new System.InvalidOperationException("OperatorManagementUI를 찾지 못했습니다."); }
UnityEngine.Transform canvas = management.transform.parent;
UnityEngine.Transform title = canvas.Find("TitleBackground");
UnityEngine.Transform lobby = canvas.Find("MainMenuBackground");
if (title != null) { title.gameObject.SetActive(false); }
if (lobby != null) { lobby.gameObject.SetActive(true); }
management.Open();
UnityEngine.Canvas.ForceUpdateCanvases();

UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(management);
UnityEngine.UI.Button back = serialized.FindProperty("backButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.UI.Button deploy = serialized.FindProperty("deployButton").objectReferenceValue as UnityEngine.UI.Button;
UnityEngine.Transform configuration = canvas.Find("MainMenuBackground/CommandMenuPanels/Configuration");

string report = string.Empty;
System.Action<string, UnityEngine.RectTransform> inspect = (label, rect) =>
{
    if (rect == null)
    {
        report += label + "=null; ";
        return;
    }

    UnityEngine.Vector3[] corners = new UnityEngine.Vector3[4];
    rect.GetWorldCorners(corners);
    UnityEngine.Canvas rootCanvas = canvas.GetComponent<UnityEngine.Canvas>();
    UnityEngine.Vector2 screen = UnityEngine.RectTransformUtility.WorldToScreenPoint(
        rootCanvas != null ? rootCanvas.worldCamera : null,
        (corners[0] + corners[2]) * 0.5f);
    var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
    {
        position = screen
    };
    var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
    UnityEngine.EventSystems.EventSystem.current.RaycastAll(data, hits);
    string names = string.Empty;
    for (int i = 0; i < hits.Count; i++)
    {
        if (i > 0) { names += " > "; }
        UnityEngine.Transform hitTransform = hits[i].gameObject.transform;
        string path = hitTransform.name;
        for (UnityEngine.Transform p = hitTransform.parent; p != null; p = p.parent) { path = p.name + "/" + path; }
        UnityEngine.UI.Graphic graphic = hits[i].gameObject.GetComponent<UnityEngine.UI.Graphic>();
        names += path + "[" + (graphic != null ? graphic.GetType().Name + ",raycast=" + graphic.raycastTarget : "no-graphic") + "]";
    }
    report += label + " @" + screen + " => " + names + "; ";
};

inspect("Back", back != null ? back.transform as UnityEngine.RectTransform : null);
inspect("Deploy", deploy != null ? deploy.transform as UnityEngine.RectTransform : null);
if (back != null)
{
    UnityEngine.UI.Image backImage = back.GetComponent<UnityEngine.UI.Image>();
    report += "BackGraphic enabled=" + (backImage != null && backImage.enabled) +
        ", raycast=" + (backImage != null && backImage.raycastTarget) +
        ", cull=" + (backImage != null && backImage.canvasRenderer.cull) +
        ", alpha=" + (backImage != null ? backImage.color.a : -1f) +
        ", depth=" + (backImage != null ? backImage.depth : -1) +
        ", rootScale=" + management.transform.lossyScale + "; ";
}
management.Close();
UnityEngine.Canvas.ForceUpdateCanvases();
inspect("ConfigurationAfterClose", configuration as UnityEngine.RectTransform);
throw new System.InvalidOperationException(report);
