using System.Text;
using UnityEditor;

namespace Gum4.SerializableCollections.Editor
{
    // 중복 하이라이트용 SerializedProperty 값 비교 문자열 생성.
    internal static class SerializedPropertyCompare
    {
        public static string ValueString(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.String          => $"s:{p.stringValue}",
            SerializedPropertyType.Integer         => $"i:{p.intValue}",
            SerializedPropertyType.Float           => $"f:{p.floatValue}",
            SerializedPropertyType.Boolean         => $"b:{p.boolValue}",
            SerializedPropertyType.Enum            => $"e:{p.enumValueIndex}",
            SerializedPropertyType.ObjectReference => $"o:{ObjectReferenceId(p)}",
            SerializedPropertyType.Color           => $"c:{p.colorValue}",
            SerializedPropertyType.Vector2         => $"v2:{p.vector2Value}",
            SerializedPropertyType.Vector3         => $"v3:{p.vector3Value}",
            SerializedPropertyType.Generic         => GenericString(p),
            _                                      => p.propertyPath,
        };

        // EntityId는 6000.4에서 instanceID를 대체하며 도입됐다 — 그 이전 에디터에서는 컴파일되지 않으므로
        // instanceID로 폴백한다. 둘 다 같은 오브젝트에 대해 세션 내 안정적인 식별자라 비교 용도로는 동등하다.
#if UNITY_6000_4_OR_NEWER
        private static string ObjectReferenceId(SerializedProperty p) => p.objectReferenceEntityIdValue.ToString();
#else
        private static string ObjectReferenceId(SerializedProperty p) => p.objectReferenceInstanceIDValue.ToString();
#endif

        // struct 등 복합 타입은 자식 프로퍼티를 재귀 순회해 비교 문자열을 조립한다
        private static string GenericString(SerializedProperty p)
        {
            var sb = new StringBuilder();
            var iter = p.Copy();
            var end = p.GetEndProperty();
            if (!iter.NextVisible(true)) return string.Empty;
            while (!SerializedProperty.EqualContents(iter, end))
            {
                sb.Append(ValueString(iter)).Append('|');
                if (!iter.NextVisible(false)) break;
            }
            return sb.ToString();
        }
    }
}
