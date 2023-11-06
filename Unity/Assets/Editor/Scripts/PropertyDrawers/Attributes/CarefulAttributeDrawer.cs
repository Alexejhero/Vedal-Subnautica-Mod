﻿using System;
using System.Reflection;
using Editor.Scripts.PropertyDrawers.Objects;
using Editor.Scripts.PropertyDrawers.Utilities;
using HarmonyLib;
using SCHIZO.Attributes;
using SCHIZO.Items.Data.Crafting;
using UnityEditor;
using UnityEngine;

namespace Editor.Scripts.PropertyDrawers.Attributes
{
    [CustomPropertyDrawer(typeof(CarefulAttribute))]
    public sealed class CarefulAttributeDrawer : PropertyDrawer
    {
        private bool _opened;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, DrawerUtils.ControlId(property.propertyPath + "label", position), label);

            Rect strRect = new Rect(position.x, position.y, position.width - 55, position.height);
            Rect openRect = new Rect(position.x + position.width - 50, position.y, 50, position.height);

            if (IsEmpty(property)) _opened = true;

            using (new EditorGUI.DisabledScope(!_opened))
            {
                Type type = property.serializedObject.targetObject.GetType();
                FieldInfo field = AccessTools.Field(type, property.name);

                if (field.FieldType == typeof(Item)) ItemDrawer.DrawItem(property, strRect);
                else EditorGUI.PropertyField(strRect, property, GUIContent.none);
            }

            using (new EditorGUI.DisabledScope(_opened))
            {
                if (GUI.Button(openRect, "Edit"))
                {
                    if (EditorUtility.DisplayDialog("Careful!", "This field is not supposed to be changed after it has been set. Are you sure you want to edit it?", "Yes", "No"))
                    {
                        _opened = true;
                    }
                }
            }

            EditorGUI.EndProperty();

            property.serializedObject.ApplyModifiedProperties();
        }

        private static bool IsEmpty(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    return string.IsNullOrWhiteSpace(property.stringValue);

                case SerializedPropertyType.ObjectReference:
                    return !property.objectReferenceValue;

                case SerializedPropertyType.ExposedReference:
                    return !property.exposedReferenceValue;

                default:
                    return false;
            }
        }
    }
}