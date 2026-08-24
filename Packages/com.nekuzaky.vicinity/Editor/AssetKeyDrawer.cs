using UnityEditor;
using UnityEngine;

namespace Nekuzaky.Vicinity.Editor
{
    [CustomPropertyDrawer(typeof(AssetKey))]
    internal sealed class AssetKeyDrawer : PropertyDrawer
    {
        #region Unity API

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty kind = property.FindPropertyRelative("m_sourceKind");
            SerializedProperty reference = property.FindPropertyRelative("m_directReference");
            SerializedProperty address = property.FindPropertyRelative("m_address");

            Rect line = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            Rect valueRect = new Rect(line.x, line.y, line.width - KindWidth - Gap, line.height);
            Rect kindRect = new Rect(line.xMax - KindWidth, line.y, KindWidth, line.height);

            DrawValue((AssetSourceKind)kind.enumValueIndex, valueRect, reference, address);
            kind.enumValueIndex = EditorGUI.Popup(kindRect, kind.enumValueIndex, KindLabels);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        #endregion

        #region Privates

        private const float KindWidth = 96f;
        private const float Gap = 4f;

        private static readonly string[] KindLabels = { "Prefab", "Resources", "Addressable" };

        private static void DrawValue(
            AssetSourceKind kind,
            Rect valueRect,
            SerializedProperty reference,
            SerializedProperty address)
        {
            if (kind == AssetSourceKind.DirectReference)
            {
                reference.objectReferenceValue = EditorGUI.ObjectField(
                    valueRect,
                    reference.objectReferenceValue,
                    typeof(GameObject),
                    false);

                return;
            }

            string hint = kind == AssetSourceKind.Resources
                ? "Path inside a Resources folder"
                : "Addressable address";

            address.stringValue = DrawTextWithHint(valueRect, address.stringValue, hint);
        }

        private static string DrawTextWithHint(Rect rect, string value, string hint)
        {
            string typed = EditorGUI.TextField(rect, value);

            if (!string.IsNullOrEmpty(typed))
            {
                return typed;
            }

            EditorGUI.LabelField(rect, " " + hint, EditorStyles.centeredGreyMiniLabel);
            return typed;
        }

        #endregion
    }
}
