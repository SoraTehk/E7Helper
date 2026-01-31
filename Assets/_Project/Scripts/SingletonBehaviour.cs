using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace SoraTehk.E7Helper {
    public class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour {
        protected static T? gInstance;

        protected static bool TryGetInstance([NotNullWhen(true)] out T? instance) {
            instance = null;
            if (gInstance != null) {
                instance = gInstance;
                return true;
            }

            var instances = FindObjectsByType<T>(FindObjectsSortMode.None);

            // Only found 1
            if (instances.Length == 1) {
                gInstance = instances[0];
                instance = gInstance;
                return true;
            }

            Debug.LogWarning($"Instance of {typeof(T).Name} not found");
            return false;
        }
    }
}