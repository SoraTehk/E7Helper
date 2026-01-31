using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoraTehk.E7Helper {
    public class InputManager : MonoBehaviour {
        [field: FoldoutGroup("Project"), SerializeField, AssetsOnly] public InputActionAsset InputActionAsset { get; private set; } = null!;

        [field: FoldoutGroup("Runtime"), SerializeField] public InputAction ToggleConsoleAction { get; private set; } = null!;

        public void Construct() {
            ToggleConsoleAction = InputActionAsset.FindActionMap("Player").FindAction("ToggleConsole");
        }
    }
}