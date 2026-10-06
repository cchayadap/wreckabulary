using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    public class BackToHub : MonoBehaviour
    {
        GameHud hud;

        void Update()
        {
            if (!hud) hud = FindFirstObjectByType<GameHud>();
            bool pressed = !hud && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) pressed |= pad.selectButton.wasPressedThisFrame;
            if (pressed)
            {
                if (Time.timeScale == 0f) Time.timeScale = 1f;
                Session.GoHome();
            }
        }
    }
}
