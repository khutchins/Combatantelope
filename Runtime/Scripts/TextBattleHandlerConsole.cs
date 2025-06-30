using UnityEngine;

namespace Combatantelope {
    public class TextBattleHandlerConsole : ITextBattleHandler {
        public void InputRequest(params string[] input) {
            foreach (var inputItem in input) {
                Debug.Log(inputItem);
            }
        }

        public void ClearInputRequest() {
        }

        public void Log(string log) {
            Debug.Log(log);
        }
    }
}