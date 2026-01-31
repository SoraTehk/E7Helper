using Sirenix.OdinInspector;
using UnityEngine;
using uWindowCapture;

public class CanvasController : MonoBehaviour {
    [FoldoutGroup("Scene")] public UwcWindowTexture WindowCapture = null!;
    [FoldoutGroup("Runtime")] public Canvas Canvas = null!;
    [FoldoutGroup("Runtime")] public CanvasGroup CanvasGroup = null!;

    private void Awake() {
        Canvas = GetComponent<Canvas>();
        CanvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update() {
        if (WindowCapture.window == null) return;

        CanvasGroup.alpha = !WindowCapture.window.isMinimized ? 1 : 0;
    }
}