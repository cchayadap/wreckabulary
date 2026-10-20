using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    internal sealed class WordboundCourseFields
    {
        readonly Dictionary<string, int> selected = new Dictionary<string, int>(StringComparer.Ordinal);
        public void Draw(object value, string path = "course", int depth = 0)
        {
            if (depth > 10) throw new ArgumentException("Course form nesting exceeds its supported bound.");
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                string label = ObjectNames.NicifyVariableName(field.Name);
                object previous = field.GetValue(value);
                field.SetValue(value, DrawValue(previous, field.FieldType, label, path + "." + field.Name, depth));
            }
        }
        object DrawValue(object value, Type type, string label, string path, int depth)
        {
            if (type == typeof(string)) return EditorGUILayout.DelayedTextField(label, (string)value ?? "");
            if (type == typeof(int)) return EditorGUILayout.DelayedIntField(label, (int)value);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, (bool)value);
            if (type == typeof(CoursePoint))
            {
                var point = (CoursePoint)value;
                var p = EditorGUILayout.Vector3IntField(label + " (mm)", new Vector3Int(point.X, point.Y, point.Z));
                return new CoursePoint(p.x, p.y, p.z);
            }
            if (type == typeof(CourseBox))
            {
                var box = (CourseBox)value;
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                var min = (CoursePoint)DrawValue(box.Min, typeof(CoursePoint), "Minimum", path + ".min", depth + 1);
                var max = (CoursePoint)DrawValue(box.Max, typeof(CoursePoint), "Maximum", path + ".max", depth + 1);
                EditorGUI.indentLevel--;
                return new CourseBox(min, max);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)value;
                Type element = type.GetGenericArguments()[0];
                int index = selected.TryGetValue(path, out var old) ? old : 0;
                index = Mathf.Clamp(index, 0, Math.Max(0, list.Count - 1));
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label + " (" + list.Count + ")", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(list.Count >= CourseLimits.ListEntries))
                    if (GUILayout.Button("Add", GUILayout.Width(45)))
                    {
                        object item = element == typeof(string) ? "" : Activator.CreateInstance(element);
                        var id = element.GetField("Id");
                        if (id?.FieldType == typeof(string)) id.SetValue(item, WorkshopCommands.Unique(element.Name.Replace("Course", ""),
                            list.Cast<object>().Select(existing => (string)id.GetValue(existing))));
                        list.Add(item); index = list.Count - 1; GUI.changed = true;
                    }
                using (new EditorGUI.DisabledScope(list.Count == 0))
                    if (GUILayout.Button("Remove", GUILayout.Width(65))) { list.RemoveAt(index); index = Math.Max(0, index - 1); GUI.changed = true; }
                EditorGUILayout.EndHorizontal();
                if (list.Count != 0)
                {
                    index = EditorGUILayout.Popup("Selected", index, list.Cast<object>().Select((item, i) =>
                        i + ": " + (element == typeof(string) ? (string)item : item.GetType().GetField("Id")?.GetValue(item)?.ToString() ??
                            item.GetType().GetField("PuzzleId")?.GetValue(item)?.ToString() ?? element.Name.Replace("Course", ""))).ToArray());
                    list[index] = DrawValue(list[index], element, "Value", path + "[" + index + "]", depth + 1);
                }
                selected[path] = index;
                EditorGUILayout.EndVertical();
                return list;
            }
            if (type.Namespace != typeof(CourseDocument).Namespace || !type.Name.StartsWith("Course", StringComparison.Ordinal))
                throw new ArgumentException("Unsupported course form field: " + type.Name);
            Draw(value, path, depth + 1);
            return value;
        }
        public void DrawSection(CourseDocument document, string section)
        {
            var field = typeof(CourseDocument).GetField(section);
            field.SetValue(document, DrawValue(field.GetValue(document), field.FieldType,
                ObjectNames.NicifyVariableName(section), "course." + section, 0));
        }
    }
}
