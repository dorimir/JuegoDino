using UnityEngine;

public static class EnemyDeathUI
{
    public static void DrawDeathScreen(EnemyAI killerDino)
    {
        if (!EnemyAI.GlobalPlayerDead || EnemyAI.KillerDino != killerDino) return;

        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
        GUI.color = oldColor;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Screen.height / 14, 34, 66),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.95f, 0.15f, 0.15f, 1f);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Screen.height / 28, 18, 28),
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = Color.white;

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Screen.height / 36, 15, 22),
            alignment = TextAnchor.MiddleCenter
        };
        hintStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.95f);

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;

        string subText;
        if (killerDino != null && killerDino.IsSmallDino)
        {
            subText = "Has sido alcanzado y devorado por un dinosaurio pequeño.";
        }
        else if (killerDino != null && killerDino.IsMediumDino)
        {
            subText = "Has sido alcanzado y devorado por un dinosaurio mediano.";
        }
        else
        {
            subText = "Has sido alcanzado y devorado por el dinosaurio grande (T-Rex).";
        }

        GUI.Label(new Rect(centerX - 350, centerY - 90, 700, 70), "¡HAS MUERTO!", titleStyle);
        GUI.Label(new Rect(centerX - 350, centerY - 15, 700, 40), subText, subStyle);
        GUI.Label(new Rect(centerX - 350, centerY + 45, 700, 40), "Presiona [ R ] para volver a intentarlo", hintStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.Clamp(Screen.height / 36, 16, 22),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        if (GUI.Button(new Rect(centerX - 150, centerY + 95, 300, 48), "REINICIAR PARTIDA", buttonStyle))
        {
            EnemyAI.RestartGame();
        }
    }
}
