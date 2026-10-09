using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    /// <summary>Routes Escape and gamepad View through the scene's pause presentation.</summary>
    public class BackToHub : MonoBehaviour
    {
        GameHud hud;

        void Start() => hud = FindAnyObjectByType<GameHud>();

        void Update()
        {
            if (KeyBindings.Busy) return;
            if (!hud) hud = FindFirstObjectByType<GameHud>();
            bool pressed = !hud && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) pressed |= pad.selectButton.wasPressedThisFrame;
            if (!pressed) return;
            if (hud) hud.TogglePause();
            else Session.GoHome();
        }
    }
}
