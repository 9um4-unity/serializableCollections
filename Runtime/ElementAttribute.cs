using System;

namespace Gum4.SerializableCollections
{
    // Key/Value(또는 HashSet의 Item)는 제네릭 타입 파라미터의 실제 필드라 컴파일 타임에 직접
    // attribute를 붙일 수 없다 — 대신 컬렉션 필드 자체에 이 attribute를 붙이면,
    // Editor 쪽 ElementAttributeForwarder가 지정된 Unity PropertyAttribute(Range, Min, Multiline 등)를
    // 런타임에 재구성해 Key/Value/Item을 그릴 때 대신 적용해준다.
    public enum ElementTarget { Key, Value, Item }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class ElementAttribute : Attribute
    {
        public readonly ElementTarget Target;
        public readonly Type AttributeType;
        public readonly object[] Args;

        // attributeType은 UnityEngine.PropertyAttribute를 상속해야 한다(예: typeof(RangeAttribute)).
        // args는 그 attribute의 생성자 인자를 그대로 전달한다.
        public ElementAttribute(ElementTarget target, Type attributeType, params object[] args)
        {
            Target = target;
            AttributeType = attributeType;
            Args = args;
        }
    }
}
