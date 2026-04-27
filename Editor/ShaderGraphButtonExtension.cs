using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

namespace DH.ShaderGraphUnbatcher
{
    [InitializeOnLoad]
    public class ShaderGraphButtonExtension
    {
        static ShaderGraphButtonExtension()
        {
            Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(Editor editor)
        {
            if (editor.target == null)
                return;

            string path = AssetDatabase.GetAssetPath(editor.target);

            if (string.IsNullOrEmpty(path) || !path.EndsWith(".shadergraph"))
                return;

            if (editor.GetType().Name != "ShaderGraphImporterEditor")
                return;

            DrawBtn(path);
        }

        private static void DrawBtn(string path)
        {
            bool prev = GUI.enabled;
            GUI.enabled = true;
            EditorGUILayout.Space(10);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Export without SRP_Batcher", GUILayout.Height(30)))
            {
                ExportShaderWithDisabledBatching(path);
            }

            EditorGUILayout.Space();
            GUI.enabled = prev;
        }

        private static void ExportShaderWithDisabledBatching(string assetPath)
        {
            string title = "Exporting Shader";
            string info = "Generating code via reflection...";

            try
            {
                // --- NEW PACKAGE CACHE LOGIC ---
                string targetPath = assetPath.Replace(".shadergraph", "_NoBatching.shader");

                if (assetPath.StartsWith("Packages/"))
                {
                    int option = EditorUtility.DisplayDialogComplex(
                        "Package Cache Warning",
                        "This Shader Graph is located in the Package Cache. Changes made here will not persist.\n\nWould you like to save the exported .shader to 'Assets/ExportedShaders' instead?",
                        "Yes (Save to Assets)",
                        "Cancel",
                        "No (Try saving in Package)"
                    );

                    if (option == 1)
                    {
                        return; 
                    }
                    else if (option == 0)
                    {
                        string directory = "Assets/ExportedShaders";
                        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                        targetPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(assetPath) + "_NoBatching.shader");
                    }
                }
                // -------------------------------------

                EditorUtility.DisplayProgressBar(title, info, 0.1f);

                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                // Note: Type.GetType for ShaderGraphImporterEditor requires exact Assembly name
                Type importerType = Type.GetType("UnityEditor.ShaderGraph.ShaderGraphImporterEditor, Unity.ShaderGraph.Editor");

                if (importerType == null)
                {
                    Debug.LogError("Could not find ShaderGraphImporterEditor. Is Shader Graph installed?");
                    return;
                }

                var methods = importerType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var getGraphDataMethod = methods.FirstOrDefault(m => m.Name.Contains("GetGraphData"));

                // Note: Static method invocation uses 'null' as first argument
                object graphData = getGraphDataMethod.Invoke(null, new object[] { importer });

                EditorUtility.DisplayProgressBar(title, "Invoking Generator...", 0.5f);

                Type generatorType = Type.GetType("UnityEditor.ShaderGraph.Generator, Unity.ShaderGraph.Editor");
                Type generationModeType = Type.GetType("UnityEditor.ShaderGraph.GenerationMode, Unity.ShaderGraph.Editor");
                object modeForReals = Enum.Parse(generationModeType, "ForReals");

                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                var ctors = generatorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var ctor = ctors.OrderByDescending(c => c.GetParameters().Length).First();
                var ctorParams = ctor.GetParameters();

                object[] args = new object[ctorParams.Length];
                for (int i = 0; i < ctorParams.Length; i++)
                {
                    Type pType = ctorParams[i].ParameterType;
                    if (pType.Name.Contains("GraphData")) args[i] = graphData;
                    else if (pType == generationModeType) args[i] = modeForReals;
                    else if (pType == typeof(string)) args[i] = assetName;
                    else if (pType == typeof(bool)) args[i] = true;
                    else args[i] = null;
                }

                object generator = ctor.Invoke(args);
                string shaderCode = (string)generatorType.GetProperty("generatedShader").GetValue(generator);

                if (string.IsNullOrEmpty(shaderCode))
                {
                    Debug.LogError("Generator returned empty code. Try saving the Shader Graph first.");
                    return;
                }

                EditorUtility.DisplayProgressBar(title, "Applying No-Batching hacks...", 0.8f);

                // Modify code to disable SRP Batcher
                string modifiedCode = shaderCode.Replace("Tags {", "Tags { \"DisableBatching\" = \"True\" ");

                if (modifiedCode.Contains("Properties"))
                {
                    string propLine = "\n        [HideInInspector]_SBPBreaker(\"SBPBreaker\", Float) = 0";
                    int insertIndex = modifiedCode.IndexOf('{', modifiedCode.IndexOf("Properties")) + 1;
                    modifiedCode = modifiedCode.Insert(insertIndex, propLine);
                }

                if (modifiedCode.Contains("HLSLPROGRAM"))
                {
                    modifiedCode = modifiedCode.Replace("HLSLPROGRAM", "HLSLPROGRAM\nfloat _SBPBreaker;");
                }

                if (modifiedCode.Contains(".color;"))
                {
                    modifiedCode = modifiedCode.Replace(".color;", ".color + (_SBPBreaker);");
                }

                string originalNameLine = shaderCode.Substring(0, shaderCode.IndexOf('\n'));
                string newNameLine = originalNameLine.Replace("Shader \"", "Shader \"Exported/DisabledBatching_");
                modifiedCode = modifiedCode.Replace(originalNameLine, newNameLine);

                EditorUtility.DisplayProgressBar(title, "Saving file...", 0.9f);

                // Write to file (using the new targetPath)
                File.WriteAllText(targetPath, modifiedCode);

                AssetDatabase.ImportAsset(targetPath);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Shader>(targetPath));

                Debug.Log($"<color=green>Shader successfully generated to: {targetPath}</color>");
            }
            catch (Exception e)
            {
                Debug.LogError("Export error: " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif