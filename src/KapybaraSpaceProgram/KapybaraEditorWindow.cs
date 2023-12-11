using UnityEngine;

namespace KapybaraSpaceProgram;

public class KapybaraEditorWindow : SpaceWarp.API.UI.Appbar.AppbarMenu
{
    public override void DrawWindow(int windowID)
    {
        ScrollPos = GUILayout.BeginScrollView(ScrollPos);
        foreach (var material in KapybaraSpaceProgramPlugin.Materials)
        {
            GUILayout.Label(material.Key);
            var color = material.Value[0].color;
            GUILayout.BeginHorizontal();
            GUILayout.Label("R");
            color.r = GUILayout.HorizontalSlider(color.r, 0, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("G");
            color.g = GUILayout.HorizontalSlider(color.g, 0, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("B");
            color.b = GUILayout.HorizontalSlider(color.b, 0, 1);
            GUILayout.EndHorizontal();
            foreach (var mat in material.Value)
            {
                mat.color = color;
            }
            GUILayout.Label(ColorUtility.ToHtmlStringRGB(color));
        }
        GUILayout.EndScrollView();
    }

    protected Vector2 ScrollPos = Vector2.zero;
    protected override float Width => 512;
    protected override float Height => 512;
    protected override float X => 100;
    protected override float Y => 100;
}