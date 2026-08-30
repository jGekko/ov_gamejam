#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SpriteDebrisBurst))]
public class SpriteDebrisBurstEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SpriteDebrisBurst emitter = (SpriteDebrisBurst)target;

        // Dibujar campos del inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(12);

        if (emitter.mode == SpriteDebrisBurst.BurstMode.PixelPerfect2DSprites)
        {
            // Botón de prueba para modo Pixel Perfect 2D Sprites
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f, 1f);
            if (GUILayout.Button("▶ Probar Burst 2D (Tamaño Exacto de Sprites)", GUILayout.Height(36)))
            {
                if (Application.isPlaying)
                {
                    emitter.Play();
                }
                else
                {
                    Debug.Log("[SpriteDebrisBurst] En modo Editor, entra en Play Mode para probar la física de los fragmentos, o pulsa Play durante la partida.");
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "⭐ MODO PIXEL PERFECT 2D (RECOMENDADO):\n" +
                "• Los fragmentos se generan con el TAMAÑO Y PROPORCIÓN EXACTA de tus sprites originales.\n" +
                "• 0 distorsión 3D (estrictamente en el plano 2D XY).\n" +
                "• Usa 'Scale Multiplier' para agrandar o achicar (1.0 = tamaño nativo 100% fiel al pixel art).",
                MessageType.Info
            );
        }
        else
        {
            // Botón de configuración para modo ParticleSystem
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f, 1f);
            if (GUILayout.Button("⚡ Configurar Particle System 2D Plano", GUILayout.Height(36)))
            {
                Undo.RecordObject(emitter.gameObject, "Setup Particle System 2D");
                emitter.SetupNativeParticleSystem();
                EditorUtility.SetDirty(emitter.gameObject);
            }

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 1f);
            if (GUILayout.Button("▶ Probar Particle System", GUILayout.Height(28)))
            {
                emitter.Play();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Configura un ParticleSystem nativo alineado al plano 2D (Facing) para evitar inclinaciones 3D.",
                MessageType.Info
            );
        }
    }
}
#endif
