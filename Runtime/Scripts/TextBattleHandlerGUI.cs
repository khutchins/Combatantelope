using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combatantelope {
    public class TextBattleHandlerGUI : MonoBehaviour, ITextBattleHandler {
        [SerializeField] TMP_Text InputText;
        [SerializeField] ScrollRect LogRect;
        [SerializeField] TMP_Text LogText;

        void Awake() {
            InputText.text = "";
            LogText.text = "";
        }

        public void InputRequest(params string[] input) {
            InputText.text += string.Join("\n", input);
        }

        public void ClearInputRequest() {
            InputText.text = "";
        }

        public void Log(string log) {
            LogText.text += log + "\n";
            if (LogRect != null) {
                Canvas.ForceUpdateCanvases();
                LogRect.normalizedPosition = Vector2.zero;
            }
        }
    }
}