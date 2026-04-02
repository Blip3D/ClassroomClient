using UnityEngine;
using UnityEngine.UI;
using ClassroomClient.Core;

namespace ClassroomClient.HUD
{
    public class StatusIndicator : MonoBehaviour
    {
        private Image dot;

        void Awake()
        {
            dot = gameObject.AddComponent<Image>();
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(20, 20);
            rt.anchoredPosition = new Vector2(-20, -20);
            dot.color = Color.red;
        }

        public void UpdateState(ConnectionState state)
        {
            if (dot == null) return;
            dot.color = state switch
            {
                ConnectionState.InLobby => Color.green,
                ConnectionState.InSession => new Color(0.13f, 0.59f, 0.95f),
                ConnectionState.Connecting or ConnectionState.Reconnecting => Color.yellow,
                _ => Color.red
            };
        }
    }
}
