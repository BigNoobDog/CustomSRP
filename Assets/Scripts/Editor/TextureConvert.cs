using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Windows;

public class TextureConvert
{
    [MenuItem("Assets/TextureConvert", false, 1)]
    static void TextureConvertFun()
    {
        var selectionObject = Selection.activeObject;
        if (selectionObject is Texture2D)
        {
            Texture2D texture = selectionObject as Texture2D;
            Texture2D raw = new Texture2D(2, 2);
            Texture2D target = new Texture2D(texture.width, texture.height);
            string assetsPath = AssetDatabase.GetAssetPath(texture);
            string ioPath = Application.dataPath.Remove(Application.dataPath.Length - 6, 6) + assetsPath;
            if (File.Exists(ioPath))
                raw = TGALoader.LoadTGA(ioPath);
            target.LoadRawTextureData(raw.GetRawTextureData());
            var colors = target.GetPixels();
            for (int i = 0; i < colors.Length; i++)
            {
                if (ComperaColor(colors[i], Color.red))
                {
                    float value = 10.0f / 255.0f;
                    colors[i] = new Color(value, value, value);
                }else if (ComperaColor(colors[i], new Color(1,1,0)))
                {
                    float value = 20.0f / 255.0f;
                    colors[i] = new Color(value, value, value);
                }else if(ComperaColor(colors[i], Color.green))
                {
                    float value = 30.0f / 255.0f;
                    colors[i] = new Color(value, value, value);
                }else if(ComperaColor(colors[i], Color.blue))
                {
                    float value = 40.0f / 255.0f;
                    colors[i] = new Color(value, value, value);
                }
                else
                {
                }
            }
            target.SetPixels(colors);
            target.Apply();
            
            EditorWindow editorWindow = EditorWindow.CreateWindow<TextureConvertWindow>();
            editorWindow.Show();
            TextureConvertWindow window = editorWindow as TextureConvertWindow;
            window.raw = raw;
            window.target = target;
            window.ioPath = ioPath;
        }
    }

    static bool ComperaColor(Color m, Color n)
    {
        bool r = Mathf.Abs(m.r - n.r) < 0.004;
        bool g = Mathf.Abs(m.g - n.g) < 0.004;
        bool b = Mathf.Abs(m.b - n.b) < 0.004;
        if (r && b && g)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}

public class TextureConvertWindow : EditorWindow
{
    public Texture2D raw;
    public Texture2D target;
    public string ioPath;

    private void OnGUI()
    {
        GUILayout.Label(target, GUIStyle.none);
        if (GUILayout.Button("Save Texture"))
        {
            File.WriteAllBytes(ioPath.Insert(ioPath.Length - 4, "1"),target.EncodeToTGA());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}