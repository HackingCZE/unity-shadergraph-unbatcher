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
            if(editor.target == null)
                return;

            string path = AssetDatabase.GetAssetPath(editor.target);

            if(string.IsNullOrEmpty(path) || !path.EndsWith(".shadergraph"))
                return;

            if(editor.GetType().Name != "ShaderGraphImporterEditor")
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

            if(GUILayout.Button("Export without SRP_Batcher", GUILayout.Height(30)))
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
                EditorUtility.DisplayProgressBar(title, info, 0.1f);

                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                Type importerType = Type.GetType("UnityEditor.ShaderGraph.ShaderGraphImporterEditor, Unity.ShaderGraph.Editor");

                var methods = importerType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var getGraphDataMethod = methods.FirstOrDefault(m => m.Name.Contains("GetGraphData"));

                object graphData = getGraphDataMethod.Invoke(importer, new object[] { importer });

                EditorUtility.DisplayProgressBar(title, "Invoking Generator...", 0.5f);

                Type generatorType = Type.GetType("UnityEditor.ShaderGraph.Generator, Unity.ShaderGraph.Editor");
                Type generationModeType = Type.GetType("UnityEditor.ShaderGraph.GenerationMode, Unity.ShaderGraph.Editor");
                object modeForReals = Enum.Parse(generationModeType, "ForReals");

                string assetName = Path.GetFileNameWithoutExtension(assetPath);

                var ctors = generatorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                var ctor = ctors.OrderByDescending(c => c.GetParameters().Length).First();
                var ctorParams = ctor.GetParameters();

                object[] args = new object[ctorParams.Length];

                for(int i = 0; i < ctorParams.Length; i++)
                {
                    Type pType = ctorParams[i].ParameterType;
                    if(pType.Name.Contains("GraphData")) args[i] = graphData;
                    else if(pType == generationModeType) args[i] = modeForReals;
                    else if(pType == typeof(string)) args[i] = assetName;
                    else if(pType == typeof(bool)) args[i] = true;
                    else args[i] = null;
                }

                object generator = ctor.Invoke(args);

                string shaderCode = (string)generatorType.GetProperty("generatedShader").GetValue(generator);

                if(string.IsNullOrEmpty(shaderCode))
                {
                    Debug.LogError("Generátor vrátil prázdný kód. Zkuste Shader Graph uložit.");
                    return;
                }

                EditorUtility.DisplayProgressBar(title, "Applying No-Batching hacks...", 0.8f);

                string modifiedCode = shaderCode.Replace("Tags {", "Tags { \"DisableBatching\" = \"True\" ");

                if(modifiedCode.Contains("Properties"))
                {
                    string propLine = "\n        [HideInInspector]_SBPBreaker(\"SBPBreaker\", Float) = 0";
                    int insertIndex = modifiedCode.IndexOf('{', modifiedCode.IndexOf("Properties")) + 1;
                    modifiedCode = modifiedCode.Insert(insertIndex, propLine);
                }

                if(modifiedCode.Contains("HLSLPROGRAM"))
                {
                    modifiedCode = modifiedCode.Replace("HLSLPROGRAM", "HLSLPROGRAM\nfloat _SBPBreaker;");
                }

                if(modifiedCode.Contains(".color;"))
                {
                    string hackLines = @".color + (_SBPBreaker);";

                    modifiedCode = modifiedCode.Replace(".color;", hackLines);
                }

                string originalNameLine = shaderCode.Substring(0, shaderCode.IndexOf('\n'));
                string newNameLine = originalNameLine.Replace("Shader \"", "Shader \"Exported/DisabledBatching_");
                modifiedCode = modifiedCode.Replace(originalNameLine, newNameLine);

                EditorUtility.DisplayProgressBar(title, "Applying No-Batching hacks...", 0.9f);

                string newPath = assetPath.Replace(".shadergraph", "_NoBatching.shader");
                File.WriteAllText(newPath, modifiedCode);

                AssetDatabase.ImportAsset(newPath);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Shader>(newPath));

                Debug.Log($"<color=green>Shader successfully generated to: {newPath}</color>");
            }
            catch(Exception e)
            {
                Debug.LogError("Export error: " + e.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif