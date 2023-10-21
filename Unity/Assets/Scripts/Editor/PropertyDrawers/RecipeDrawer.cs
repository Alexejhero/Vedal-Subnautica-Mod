﻿using SCHIZO.Enums;
using SCHIZO.Items.Data.Crafting;
using UnityEditor;
using UnityEngine;

namespace PropertyDrawers
{
    [CustomPropertyDrawer(typeof(Recipe))]
    public sealed class RecipeDrawer : PropertyDrawer
    {
        private static bool IsOk(SerializedProperty property)
        {
            if (!property.objectReferenceValue) return true;

            int instanceId = property.objectReferenceInstanceIDValue;
            Recipe recipe = (Recipe) property.objectReferenceValue;

            if (property.propertyPath.ToLower().Contains("sn") && !recipe.game.HasFlag(Game.Subnautica)) return false;
            if (property.propertyPath.ToLower().Contains("bz") && !recipe.game.HasFlag(Game.BelowZero)) return false;
            return true;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Color oldColor = GUI.backgroundColor;
            if (!IsOk(property)) GUI.backgroundColor = Color.red;

            EditorGUI.PropertyField(position, property, label);

            GUI.backgroundColor = oldColor;

            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
