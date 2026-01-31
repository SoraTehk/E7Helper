using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace SoraTehk.E7Helper {
    public partial class ServiceLocator : SingletonBehaviour<ServiceLocator> {
        // ReSharper disable once Unity.RedundantSerializeFieldAttribute
        [SerializeField, ReadOnly] private readonly Dictionary<Type, object> m_Services = new();

        public static void Register<T>(T instance) where T : class {
            if (!TryGetInstance(out var ins)) return;
            ins.m_Services[typeof(T)] = instance;
        }

        public static bool TryGet<T>([NotNullWhen(true)] out T? service) where T : class {
            service = null;
            if (!TryGetInstance(out var ins)) return false;
            if (ins.m_Services.TryGetValue(typeof(T), out var value)) {
                service = value as T;
                return service != null;
            }

            return false;
        }

        public static void Unregister<T>(T instance) where T : class {
            if (!TryGetInstance(out var ins)) return;
            ins.m_Services.Remove(typeof(T));
        }
    }

    [ShowOdinSerializedPropertiesInInspector]
    public partial class ServiceLocator : ISerializationCallbackReceiver, ISupportsPrefabSerialization {
        [SerializeField, HideInInspector] private SerializationData m_SerializationData;

        SerializationData ISupportsPrefabSerialization.SerializationData {
            get => m_SerializationData;
            set => m_SerializationData = value;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize() {
            UnitySerializationUtility.DeserializeUnityObject(this, ref m_SerializationData);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() {
            UnitySerializationUtility.SerializeUnityObject(this, ref m_SerializationData);
        }
    }
}