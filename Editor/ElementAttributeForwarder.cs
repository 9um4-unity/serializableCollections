using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Gum4.SerializableCollections;

namespace Gum4.SerializableCollections.Editor
{
    // [ElementAttribute]로 컬렉션 필드에 붙은 PropertyAttribute(Range, Min, Multiline 등)를
    // 읽어, Unity가 내장 필드에 대해 하는 것과 같은 방식으로 그 attribute에 매칭되는
    // PropertyDrawer를 찾아 Key/Value/Item에 대신 적용한다.
    //
    // Unity는 SerializedProperty의 실제 FieldInfo(경로를 반영해 리플렉션으로 찾은 것)에 붙은
    // attribute만 보고 드로어를 고른다. Key/Value는 제네릭 Pair<TKey,TValue>의 필드라
    // 인스턴스별로 attribute를 붙일 수 없으므로, 컬렉션 필드에 붙은 attribute를 여기서
    // 대신 읽어 같은 효과를 재현한다. CustomPropertyDrawer 매칭에 쓰이는
    // m_Type/m_UseForChildren, PropertyDrawer의 m_Attribute는 모두 비공개 필드라 리플렉션에
    // 의존한다 — 실패하면 기본 PropertyField로 조용히 폴백한다.
    internal static class ElementAttributeForwarder
    {
        private static readonly FieldInfo CustomDrawerTypeField =
            typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CustomDrawerUseForChildrenField =
            typeof(CustomPropertyDrawer).GetField("m_UseForChildren", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DrawerAttributeField =
            typeof(PropertyDrawer).GetField("m_Attribute", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Dictionary<Type, Type> DrawerTypeCache = new();

        public static Dictionary<ElementTarget, PropertyAttribute[]> ResolveAll(FieldInfo collectionField)
        {
            var result = new Dictionary<ElementTarget, PropertyAttribute[]>();
            if (collectionField == null) return result;

            var raw = collectionField.GetCustomAttributes(typeof(ElementAttribute), true)
                .Cast<ElementAttribute>().ToArray();
            if (raw.Length == 0) return result;

            var byTarget = new Dictionary<ElementTarget, List<PropertyAttribute>>();
            foreach (var meta in raw)
            {
                var instance = TryCreateAttribute(meta);
                if (instance == null) continue;
                if (!byTarget.TryGetValue(meta.Target, out var list))
                    byTarget[meta.Target] = list = new List<PropertyAttribute>();
                list.Add(instance);
            }

            foreach (var kv in byTarget)
                result[kv.Key] = kv.Value.ToArray();
            return result;
        }

        private static PropertyAttribute TryCreateAttribute(ElementAttribute meta)
        {
            if (meta.AttributeType == null || !typeof(PropertyAttribute).IsAssignableFrom(meta.AttributeType))
            {
                Debug.LogError($"[ElementAttribute] {meta.AttributeType}는 PropertyAttribute가 아닙니다.");
                return null;
            }
            try
            {
                return (PropertyAttribute)Activator.CreateInstance(meta.AttributeType, meta.Args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ElementAttribute] {meta.AttributeType.Name} 생성 실패: {e.Message}");
                return null;
            }
        }

        // attributes가 비어 있으면 기본 EditorGUI.PropertyField/GetPropertyHeight와 동일하게 동작한다.
        public static float GetPropertyHeight(SerializedProperty prop, GUIContent label, PropertyAttribute[] attributes)
        {
            var drawer = Resolve(attributes);
            if (drawer == null) return EditorGUI.GetPropertyHeight(prop, label, true);
            try { return drawer.GetPropertyHeight(prop, label); }
            catch { return EditorGUI.GetPropertyHeight(prop, label, true); }
        }

        public static void PropertyField(Rect rect, SerializedProperty prop, GUIContent label, PropertyAttribute[] attributes)
        {
            var drawer = Resolve(attributes);
            if (drawer == null)
            {
                EditorGUI.PropertyField(rect, prop, label, true);
                return;
            }
            try { drawer.OnGUI(rect, prop, label); }
            catch { EditorGUI.PropertyField(rect, prop, label, true); }
        }

        // 여러 attribute가 지정된 경우 매칭되는 PropertyDrawer가 있는 첫 번째 것만 적용한다(스태킹 미지원).
        private static PropertyDrawer Resolve(PropertyAttribute[] attributes)
        {
            if (attributes == null) return null;
            foreach (var attr in attributes)
            {
                var drawer = BuildDrawer(attr);
                if (drawer != null) return drawer;
            }
            return null;
        }

        private static PropertyDrawer BuildDrawer(PropertyAttribute attr)
        {
            var drawerType = FindDrawerType(attr.GetType());
            if (drawerType == null) return null;

            try
            {
                var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType);
                DrawerAttributeField?.SetValue(drawer, attr);
                return drawer;
            }
            catch
            {
                return null;
            }
        }

        private static Type FindDrawerType(Type attributeType)
        {
            if (DrawerTypeCache.TryGetValue(attributeType, out var cached)) return cached;

            Type exact = null, fallback = null;
            if (CustomDrawerTypeField != null && CustomDrawerUseForChildrenField != null)
            {
                foreach (var drawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
                {
                    foreach (var cpdObj in drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), false))
                    {
                        var cpd = (CustomPropertyDrawer)cpdObj;
                        var targetType = (Type)CustomDrawerTypeField.GetValue(cpd);
                        if (targetType == null) continue;

                        if (targetType == attributeType) { exact = drawerType; break; }

                        var useForChildren = (bool)CustomDrawerUseForChildrenField.GetValue(cpd);
                        if (useForChildren && targetType.IsAssignableFrom(attributeType) && fallback == null)
                            fallback = drawerType;
                    }
                    if (exact != null) break;
                }
            }

            var result = exact ?? fallback;
            DrawerTypeCache[attributeType] = result;
            return result;
        }
    }
}
