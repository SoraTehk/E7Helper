using Sirenix.OdinInspector;
using UnityEngine;

namespace SoraTehk.E7Helper {
    /// <summary>
    /// Main entry point for the project (act as a Main class of a game)
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class Main : SingletonBehaviour<Main> {
        [field: FoldoutGroup("Scene"), SerializeField, SceneObjectsOnly] public InputManager InputManager { get; private set; } = null!;

        private void Awake() {
            InputManager.Construct();
            ServiceLocator.Register(InputManager);
        }
    }
}