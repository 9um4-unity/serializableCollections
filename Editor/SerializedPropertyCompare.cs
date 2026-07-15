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
            SerializedPropertyType.ObjectReference => $"o:{p.objectReferenceEntityIdValue}",
            SerializedPropertyType.Color           => $"c:{p.colorValue}",
            SerializedPropertyType.Vector2         => $"v2:{p.vector2Value}",
            SerializedPropertyType.Vector3         => $"v3:{p.vector3Value}",
            SerializedPropertyType.Generic         => GenericString(p),
            _                                      => p.propertyPath,
        };

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
