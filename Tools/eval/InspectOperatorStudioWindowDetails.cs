UnityEditor.EditorWindow[] windows = UnityEngine.Resources.FindObjectsOfTypeAll<RCCom.EditorTools.OperatorStudioWindow>();
for (int i = 0; i < windows.Length; i++)
{
    UnityEngine.Debug.Log($"[InspectOperatorStudioWindowDetails] title={windows[i].titleContent.text}, " +
        $"position={windows[i].position}, hasFocus={windows[i].hasFocus}");
}
