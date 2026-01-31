using Sirenix.OdinInspector;
using UnityEngine;

namespace SoraTehk.E7Helper {
    public class UIClickInterceptor : MonoBehaviour {
        [field: FoldoutGroup("Config"), SerializeField] public bool IsClickThrough { get; private set; } = false;
    }
}