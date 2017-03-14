using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

[CustomEditor(typeof(VIDE_Data))]
public class VIDE_DataE : Editor
{
    VIDE_Data d;
    Vector2 scrollPos = new Vector2();

    public override void OnInspectorGUI()
    {

        d = (VIDE_Data)target;
        GUIStyle b = new GUIStyle(GUI.skin.GetStyle("Label"));
        b.fontStyle = FontStyle.Bold;

        if (EditorApplication.isPlaying)
        {

            if (d.isLoaded)
            {
                GUILayout.Box("Active: " + d.diags[d.currentDiag].name, GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.Box("No dialogue Active", GUILayout.ExpandWidth(true));
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUI.skin.GetStyle("Box"), GUILayout.ExpandWidth(true), GUILayout.Height(400));
            for (int i = 0; i < d.diags.Count; i++)
            {
                if (!d.diags[i].loaded)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(i.ToString() + ". " + d.diags[i].name + ": NOT LOADED");
                    if (d.isLoaded) GUI.enabled = false;
                    if (GUILayout.Button("Load!")) d.LoadDialogues(d.diags[i].name, "");
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();

                }
                else
                {
                    EditorGUILayout.LabelField(i.ToString() + ". " + d.diags[i].name + ": LOADED", b);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();

            if (d.isLoaded) GUI.enabled = false;

            if (GUILayout.Button("Load All"))
            {
                d.LoadDialogues();
            }
            if (GUILayout.Button("Unload All"))
            {
                d.UnloadDialogues();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

        } else
        {
            GUILayout.Label("Enter PlayMode to display loaded/unloaded information");
        }


    }

}
