using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combatantelope {
    /// <summary>
    /// Interface for providing a simple output mechanism for text-based battle systems.
    /// </summary>
    public interface ITextBattleHandler {
        void Log(string log);
        void InputRequest(params string[] input);
        void ClearInputRequest();
    }
}