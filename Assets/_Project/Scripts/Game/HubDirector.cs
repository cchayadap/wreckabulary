using System.Collections;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>The house: roommates walk in through the front door, then pick a mode at the typewriter.</summary>
    public class HubDirector : MonoBehaviour
    {
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] GameHud hud;
        [SerializeField] Typewriter typewriter;
        [SerializeField] Wardrobe wardrobe;
        [Tooltip("The door leaf, pivoting on its hinge. Swings open whenever someone walks in.")]
        [SerializeField] Transform door;
        [SerializeField] float doorOpenAngle = -100f;

        Coroutine swing;

        void Start()
        {
            Music.Play(Track.Cozy);
            joins.Joined += _ => OpenDoor();
            joins.RespawnKnockedOut = true;
            if (joins.Players.Count > 0) OpenDoor();
        }

        void Update()
        {
            if (joins.Players.Count == 0)
                hud.SetInstruction(ControlHints.Join("walk in"), ControlHints.Players);
            else if (typewriter.User)
                hud.SetInstruction("Choose a mode", "Up/down to choose  •  grab or attack to pick  •  spell to get up");
            else if (wardrobe && wardrobe.User)
                hud.SetInstruction("", "");
            else
                hud.SetInstruction("Walk up to the typewriter and press grab", "Dress up at the wardrobe  •  more roommates can walk in any time");
            hud.SetScoreboard(joins.Players, _ => 0, 0, false);
        }

        public void OpenDoor()
        {
            if (!door) return;
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(Swing());
            Sfx.Play(Sound.Door, door.position, 0.7f);
        }

        IEnumerator Swing()
        {
            yield return Rotate(0f, doorOpenAngle, 0.25f);
            yield return new WaitForSeconds(1.2f);
            yield return Rotate(doorOpenAngle, 0f, 0.4f);
        }

        IEnumerator Rotate(float from, float to, float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                door.localRotation = Quaternion.Euler(0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)), 0f);
                yield return null;
            }
            door.localRotation = Quaternion.Euler(0f, to, 0f);
        }
    }
}
