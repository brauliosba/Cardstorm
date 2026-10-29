using System;
using System.Collections.Generic;
using System.Linq;
using Cardstorm.Cards;
using UnityEditor;
using UnityEngine;

namespace Cardstorm.EditorTools
{
    // Desplegable para elegir la subclase de un campo [SerializeReference]; debajo se editan sus campos.
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        const string NoneLabel = "(ninguno)";

        static readonly Dictionary<Type, (Type[] types, GUIContent[] labels)> Cache = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(position, label.text, "Usa [SerializeReference] con [SubclassSelector].");
                EditorGUI.EndProperty();
                return;
            }

            var (types, labels) = GetOptions(BaseType());
            var current = property.managedReferenceValue?.GetType();
            int index = current == null ? 0 : Array.IndexOf(types, current) + 1;

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            int chosen = EditorGUI.Popup(line, label, index, labels);
            if (chosen != index)
            {
                property.managedReferenceValue = chosen == 0 ? null : Activator.CreateInstance(types[chosen - 1]);
                property.serializedObject.ApplyModifiedProperties();
            }

            if (property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
                foreach (var child in Children(property))
                {
                    float h = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                    y += h + EditorGUIUtility.standardVerticalSpacing;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.propertyType != SerializedPropertyType.ManagedReference || property.managedReferenceValue == null)
                return height;

            foreach (var child in Children(property))
                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        // Tipo declarado del campo; en listas y arrays, el de sus elementos.
        Type BaseType()
        {
            var type = fieldInfo.FieldType;
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return type;
        }

        static (Type[] types, GUIContent[] labels) GetOptions(Type baseType)
        {
            if (Cache.TryGetValue(baseType, out var options)) return options;

            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsGenericType && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToArray();

            var labels = new GUIContent[types.Length + 1];
            labels[0] = new GUIContent(NoneLabel);
            for (int i = 0; i < types.Length; i++)
                labels[i + 1] = new GUIContent(DisplayName(types[i]));

            options = (types, labels);
            Cache[baseType] = options;
            return options;
        }

        // ProjectileEffect → «Projectile».
        static string DisplayName(Type type)
        {
            const string suffix = "Effect";
            string name = type.Name;
            return name.Length > suffix.Length && name.EndsWith(suffix) ? name[..^suffix.Length] : name;
        }

        static IEnumerable<SerializedProperty> Children(SerializedProperty property)
        {
            var it = property.Copy();
            var end = property.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                yield return it.Copy();
            }
        }
    }
}
