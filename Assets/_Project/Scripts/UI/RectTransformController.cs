using Sirenix.OdinInspector;
using UnityEngine;

[DefaultExecutionOrder(-40)]
[RequireComponent(typeof(RectTransform))]
public class RectTransformController : MonoBehaviour {
    [FoldoutGroup("Runtime")] public RectTransform RectTransform = null!;
    [FoldoutGroup("Runtime")] public RectTransform ParentRectTransform = null!;
    [FoldoutGroup("Runtime")] public Vector2 InitialAnchoredPositionRatio;
    [FoldoutGroup("Runtime")] public Vector2 InitialSizeRatio;

    private void Awake() {
        RectTransform = GetComponent<RectTransform>();
        ParentRectTransform = transform.parent?.GetComponent<RectTransform>();
    }

    private void Start() {
        InitialSizeRatio = new Vector2(
            RectTransform.rect.width / ParentRectTransform.rect.size.x,
            RectTransform.rect.height / ParentRectTransform.rect.size.y
        );
        InitialAnchoredPositionRatio = new Vector2(
            RectTransform.anchoredPosition.x / ParentRectTransform.rect.size.x,
            RectTransform.anchoredPosition.y / ParentRectTransform.rect.size.y
        );
    }

    void Update() {
        // Apply proportional position
        RectTransform.anchoredPosition = new Vector2(
            ParentRectTransform.rect.size.x * InitialAnchoredPositionRatio.x,
            ParentRectTransform.rect.size.y * InitialAnchoredPositionRatio.y
        );

        // Apply proportional size
        RectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            ParentRectTransform.rect.size.x * InitialSizeRatio.x
        );
        RectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            ParentRectTransform.rect.size.y * InitialSizeRatio.y
        );
    }

    public float GetAnchoredX() {
        return RectTransform.anchoredPosition.x;
    }
    public float GetAnchoredY() {
        return RectTransform.anchoredPosition.y;
    }
    public float GetWidth() {
        return RectTransform.rect.width;
    }
    public float GetHeight() {
        return RectTransform.rect.height;
    }
}