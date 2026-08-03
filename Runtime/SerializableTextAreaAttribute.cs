using UnityEngine;

namespace Gum4.SerializableCollections
{
    // Unity 내장 [TextArea]는 List/배열 필드에 붙이면 그 안의 모든 하위 프로퍼티(Key 포함)에도
    // 전파되어, 문자열이 아닌 Key에도 TextAreaDrawer가 적용되며 오작동한다.
    // 이 속성은 전용 타입이라 Unity가 매칭할 내장 드로어가 없으므로 전파되어도 무시되고,
    // SerializableDictionaryDrawer가 fieldInfo를 통해 직접 읽어 Value에만 수동으로 적용한다.
    public sealed class SerializableTextAreaAttribute : PropertyAttribute
    {
        public readonly int minLines;
        public readonly int maxLines;

        public SerializableTextAreaAttribute(int minLines = 3, int maxLines = 5)
        {
            this.minLines = minLines;
            this.maxLines = maxLines;
        }
    }
}
