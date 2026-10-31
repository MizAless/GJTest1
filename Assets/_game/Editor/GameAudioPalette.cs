using System;
using UnityEditor;
using UnityEngine;

public class GameAudioPalette : EditorWindow
{
    [MenuItem("Tools/Prototype/Audio/Sound palette (Play Mode)")]
    public static void Open()
    {
        GetWindow<GameAudioPalette>("Game audio");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Enter Play Mode to audition. Volume, pitch ranges and limits live in GameAudioBank.asset.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                if (GUILayout.Button(cue.ToString()))
                {
                    GameAudio.Play(cue);
                }
            }
        }
    }
}
