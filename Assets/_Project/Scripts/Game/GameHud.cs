using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Match cards, a physical letter bag, a craft drawer and independent touch controls.</summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI title, subtitle, timer, scoreboard, instruction, checklist;
        [SerializeField] GameObject checklistPanel;
        public Canvas UiCanvas { get; private set; }
        public bool TouchControlsShown => touchRoot && touchRoot.activeSelf;
        public PlayerController LocalPlayer { get; private set; }

        static readonly Color Ink = new(0.12f, 0.18f, 0.19f), Cream = new(0.98f, 0.95f, 0.86f);
        static readonly Color Mint = new(0.44f, 0.85f, 0.73f), Coral = new(0.96f, 0.47f, 0.38f);
        static readonly Color Dark = new(0.10f, 0.16f, 0.19f, 0.94f);
        readonly List<PlayerCard> cards = new();
        readonly List<RecipeView> recipes = new();
        readonly List<Sprite> ownedSprites = new();
        readonly List<Texture2D> ownedTextures = new();
        readonly List<TouchStick> sticks = new();
        readonly Dictionary<TouchAction, (TouchActionButton button, TextMeshProUGUI label)> skillButtons = new();
        readonly Dictionary<string, Texture2D> itemIcons = new(StringComparer.Ordinal);
        readonly TextMeshProUGUI[] tileLabels = new TextMeshProUGUI[18];
        readonly Image[] tileFaces = new Image[18];
        IReadOnlyList<PlayerController> players;
        Func<PlayerController, int> wins;
        int roundsToWin;
        bool showWins;
        PlayerJoinManager joins;
        Typewriter typewriter;
        RectTransform safe;
        GameObject touchRoot, bagRoot, craftRoot;
        GameObject desktopHints;
        GameObject typewriterControls, homeButton;
        TextMeshProUGUI typewriterChoice;
        TextMeshProUGUI bagLabel, heldLabel, craftStatus, buildLabel, playLabel;
        Image craftProgress;
        Button buildButton;
        RectTransform playRect;
        Texture2D iconAtlas;
        Sprite roundSprite, panelSprite;
        Rect lastSafe;
        int lastWidth, lastHeight;
        float nextRefresh;

        sealed class PlayerCard
        {
            public GameObject root;
            public Image badge, fill;
            public TextMeshProUGUI initial, name, status;
        }
        sealed class RecipeView
        {
            public Button button;
            public Image face;
            public TextMeshProUGUI text;
            public int index;
            public RawImage icon;
        }

        void Start()
        {
            UiCanvas = GetComponentInParent<Canvas>();
            if (!UiCanvas) UiCanvas = gameObject.AddComponent<Canvas>();
            UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (!UiCanvas.TryGetComponent<GraphicRaycaster>(out _)) UiCanvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = UiCanvas.GetComponent<CanvasScaler>() ?? UiCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!EventSystem.current)
            {
                var events = new GameObject("UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            joins = FindFirstObjectByType<PlayerJoinManager>();
            typewriter = FindFirstObjectByType<Typewriter>();
            iconAtlas = Resources.Load<Texture2D>("UI/ActionIcons");
            foreach (var item in GameConfig.Current.Items.All)
            {
                var icon = Resources.Load<Texture2D>("UI/Items/" + item.Id);
                if (icon) itemIcons[item.Id] = icon;
            }
            roundSprite = MakeShape(true);
            panelSprite = MakeShape(false);
            safe = Rect("Safe HUD", UiCanvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            StyleLegacyText();
            BuildCards(); BuildBag(); BuildCraftDrawer(); BuildTouchControls();
            BuildNavigation();
            ShowTouchControls(Application.isMobilePlatform || Touchscreen.current != null);
            ApplySafeArea();
        }

        void StyleLegacyText()
        {
            if (scoreboard) scoreboard.gameObject.SetActive(false);
            if (title) { title.fontSize = 88f; title.raycastTarget = false; title.rectTransform.sizeDelta = new Vector2(1450f, 170f); }
            if (subtitle) { subtitle.fontSize = 32f; subtitle.raycastTarget = false; }
            if (timer)
            {
                PlaceText(timer, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(320f, 72f), 42f);
            }
            if (instruction)
            {
                PlaceText(instruction, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1200f, 108f), 26f);
                instruction.textWrappingMode = TextWrappingModes.Normal;
            }
            if (checklist)
            {
                PlaceText(checklist, new Vector2(0f, 1f), new Vector2(35f, -265f), new Vector2(380f, 330f), 27f);
                checklist.rectTransform.pivot = new Vector2(0f, 1f);
            }
            if (checklistPanel)
            {
                var rt = (RectTransform)checklistPanel.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(20f, -245f); rt.sizeDelta = new Vector2(410f, 365f);
                if (checklistPanel.TryGetComponent<Image>(out var image))
                { image.sprite = panelSprite; image.type = Image.Type.Sliced; image.color = Dark; image.raycastTarget = false; }
            }
        }

        static void PlaceText(TextMeshProUGUI text, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = anchor;
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize; text.raycastTarget = false;
        }

        void BuildCards()
        {
            for (int i = 0; i < 4; i++)
            {
                var anchor = new Vector2(i < 2 ? 0f : 1f, 1f);
                var rt = Panel($"Roommate {i + 1}", safe, anchor, new Vector2(i < 2 ? 24f : -24f, -24f - (i % 2) * 76f), new Vector2(282f, 68f), anchor, Dark);
                var badge = Panel("Initial badge", rt, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(45f, 45f), new Vector2(0f, 0.5f), Mint, true).GetComponent<Image>();
                var card = new PlayerCard { root = rt.gameObject, badge = badge };
                card.initial = Text("Initial", badge.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 28f, Ink);
                card.name = Text("Name", rt, new Vector2(0f, 1f), new Vector2(68f, -8f), new Vector2(204f, 25f), 20f, Cream, TextAlignmentOptions.Left);
                card.name.rectTransform.pivot = new Vector2(0f, 1f);
                card.status = Text("Status", rt, Vector2.zero, new Vector2(68f, 8f), new Vector2(204f, 21f), 15f, Cream, TextAlignmentOptions.Left);
                card.status.rectTransform.pivot = Vector2.zero;
                card.fill = Fill("Health", Panel("Health track", rt, new Vector2(0f, 0.5f), new Vector2(68f, -5f), new Vector2(194f, 8f), new Vector2(0f, 0.5f), new Color(1f, 1f, 1f, 0.12f)), Mint);
                cards.Add(card); rt.gameObject.SetActive(false);
            }
        }

        void BuildBag()
        {
            var rt = Panel("Letter bag", safe, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(544f, 142f), new Vector2(0.5f, 0f), Dark);
            bagRoot = rt.gameObject;
            bagLabel = Text("Bag count", rt, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(500f, 28f), 18f, Cream);
            bagLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            for (int i = 0; i < tileLabels.Length; i++)
            {
                var tile = Panel($"Letter {i + 1}", rt, new Vector2(0f, 1f), new Vector2(23f + (i % 9) * 56f, -48f - (i / 9) * 38f), new Vector2(48f, 32f), new Vector2(0f, 1f), Cream);
                tileFaces[i] = tile.GetComponent<Image>();
                tileLabels[i] = Text("Letter", tile, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 32f), 24f, Ink);
            }
            heldLabel = Text("Gear", rt, new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(500f, 24f), 15f, Mint);
            heldLabel.rectTransform.pivot = new Vector2(0.5f, 0f); bagRoot.SetActive(false);
            var hint = Text("Desktop controls", safe, new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(1030f, 45f), 15f, Cream);
            hint.text = "WASD move  •  mouse aim  •  click smash  •  SPACE jump  •  SHIFT dash\nE grab/throw  •  hold Q craft  •  F place  •  TAB swap  •  hold R drop  •  right click block";
            desktopHints = hint.gameObject;
        }

        void BuildCraftDrawer()
        {
            var rt = Panel("Craft drawer", safe, new Vector2(0.5f, 0f), new Vector2(0f, 178f), new Vector2(566f, 400f), new Vector2(0.5f, 0f), Dark);
            craftRoot = rt.gameObject;
            Text("Heading", rt, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(510f, 36f), 29f, Cream).text = "MAKE SOMETHING";
            craftStatus = Text("Craft status", rt, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(520f, 40f), 18f, Mint);
            craftStatus.textWrappingMode = TextWrappingModes.Normal;
            for (int i = 0; i < 4; i++)
            {
                var row = Panel($"Recipe {i + 1}", rt, new Vector2(0.5f, 1f), new Vector2(0f, -105f - i * 53f), new Vector2(500f, 46f), new Vector2(0.5f, 1f), Dark);
                var view = new RecipeView { face = row.GetComponent<Image>(), button = row.gameObject.AddComponent<Button>() };
                view.face.raycastTarget = true;
                view.icon = Rect("Object preview", row, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(42f, 42f)).gameObject.AddComponent<RawImage>();
                view.icon.raycastTarget = false;
                view.text = Text("Recipe", row, new Vector2(0.5f, 0.5f), new Vector2(25f, 0f), new Vector2(412f, 40f), 20f, Cream, TextAlignmentOptions.Left);
                view.button.onClick.AddListener(() => { if (LocalPlayer && view.index >= 0) LocalPlayer.Summoner.Select(view.index); });
                recipes.Add(view);
            }
            MakeButton("Previous", rt, Vector2.zero, new Vector2(30f, 16f), new Vector2(66f, 48f), "<", () => { if (LocalPlayer) LocalPlayer.Summoner.Step(-1); });
            MakeButton("Next", rt, new Vector2(1f, 0f), new Vector2(-30f, 16f), new Vector2(66f, 48f), ">", () => { if (LocalPlayer) LocalPlayer.Summoner.Step(1); });
            buildButton = MakeButton("Build", rt, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(248f, 48f), "BUILD", ConfirmCraft);
            buildLabel = buildButton.GetComponentInChildren<TextMeshProUGUI>();
            MakeButton("Cancel", rt, Vector2.one, new Vector2(-8f, -8f), new Vector2(40f, 40f), "X", CancelCraft);
            craftProgress = Fill("Building", Panel("Progress track", rt, new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(500f, 9f), new Vector2(0.5f, 0f), new Color(1f, 1f, 1f, 0.12f)), Mint);
            craftRoot.SetActive(false);
        }

        void BuildTouchControls()
        {
            var rt = Rect("Touch controls", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; touchRoot = rt.gameObject;
            AddStick(rt, false, new Vector2(135f, 128f), Vector2.zero, 190f);
            AddStick(rt, true, new Vector2(-128f, 142f), new Vector2(1f, 0f), 152f);
            AddSkill(rt, TouchAction.Attack, "SMASH", new Vector2(-280f, 167f), 112f, Coral);
            AddSkill(rt, TouchAction.Dodge, "DASH", new Vector2(-250f, 297f), 89f, Mint);
            AddSkill(rt, TouchAction.Jump, "JUMP", new Vector2(-123f, 294f), 89f, Cream);
            AddSkill(rt, TouchAction.Block, "BLOCK", new Vector2(-384f, 270f), 89f, Cream);
            AddSkill(rt, TouchAction.Grab, "GRAB", new Vector2(-384f, 161f), 89f, Cream);
            AddSkill(rt, TouchAction.Craft, "CRAFT", new Vector2(-503f, 270f), 89f, Mint);
            AddSkill(rt, TouchAction.Deploy, "PLACE", new Vector2(-503f, 161f), 89f, Cream);
            AddSkill(rt, TouchAction.Drop, "HOLD DROP", new Vector2(-384f, 51f), 76f, Cream);
            AddSkill(rt, TouchAction.Swap, "SWAP", new Vector2(-503f, 51f), 76f, Cream);
            playLabel = MakeButton("Play or Ready", rt, new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(200f, 50f), "PLAY", JoinOrReady).GetComponentInChildren<TextMeshProUGUI>();
            playRect = (RectTransform)playLabel.transform.parent;
            MakeButton("Touch switch", safe, Vector2.one, new Vector2(-22f, -182f), new Vector2(156f, 36f), "TOUCH", () => ShowTouchControls(!TouchControlsShown)).gameObject.SetActive(!Application.isMobilePlatform);
        }

        void AddStick(Transform parent, bool aim, Vector2 position, Vector2 anchor, float diameter)
        {
            var rt = Panel(aim ? "Aim stick" : "Move stick", parent, anchor, position, new Vector2(diameter, diameter), new Vector2(0.5f, 0.5f), new Color(0.12f, 0.18f, 0.19f, 0.72f), true);
            rt.GetComponent<Image>().raycastTarget = true;
            var ring = Panel("Ring", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter - 12f, diameter - 12f), new Vector2(0.5f, 0.5f), new Color(0.76f, 0.90f, 0.83f, 0.20f), true);
            var knob = Panel("Thumb", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter * 0.36f, diameter * 0.36f), new Vector2(0.5f, 0.5f), Cream, true);
            Text("Hint", ring, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, 30f), 19f, Cream).text = aim ? "AIM" : "MOVE";
            var stick = rt.gameObject.AddComponent<TouchStick>(); stick.knob = knob; stick.aims = aim; stick.radius = diameter * 0.28f;
            stick.Initialise(Camera.main); sticks.Add(stick);
        }

        void BuildNavigation()
        {
            homeButton = MakeButton("Return home", safe, Vector2.one, new Vector2(-22f, -226f), new Vector2(156f, 42f), "HOME", () =>
            {
                TouchBinding.Shared.ReleaseAll();
                Session.GoHome();
            }).gameObject;
            homeButton.SetActive(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Session.HubScene);
            var panel = Panel("Typewriter touch menu", safe, new Vector2(0.5f, 0f), new Vector2(0f, 178f), new Vector2(580f, 166f), new Vector2(0.5f, 0f), Dark);
            typewriterControls = panel.gameObject;
            typewriterChoice = Text("Mode choice", panel, new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(540f, 54f), 25f, Cream);
            typewriterChoice.textWrappingMode = TextWrappingModes.Normal;
            MakeButton("Previous mode", panel, Vector2.zero, new Vector2(18f, 22f), new Vector2(132f, 52f), "PREV", () => TouchBinding.Shared.Pulse(TouchAction.Up));
            MakeButton("Next mode", panel, Vector2.zero, new Vector2(162f, 22f), new Vector2(132f, 52f), "NEXT", () => TouchBinding.Shared.Pulse(TouchAction.Down));
            MakeButton("Choose mode", panel, Vector2.zero, new Vector2(306f, 22f), new Vector2(132f, 52f), "SELECT", () => TouchBinding.Shared.Pulse(TouchAction.Grab));
            MakeButton("Leave typewriter", panel, Vector2.zero, new Vector2(450f, 22f), new Vector2(112f, 52f), "BACK", () => { if (typewriter) typewriter.Close(); });
            typewriterControls.SetActive(false);
        }

        void AddSkill(Transform parent, TouchAction action, string label, Vector2 position, float diameter, Color accent)
        {
            var rt = Panel(label, parent, new Vector2(1f, 0f), position, new Vector2(diameter, diameter), new Vector2(0.5f, 0.5f), accent, true);
            rt.GetComponent<Image>().raycastTarget = true;
            var inset = Panel("Face", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter - 7f, diameter - 7f), new Vector2(0.5f, 0.5f), Dark, true);
            var icon = CreateImage("Icon", inset, new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(diameter * 0.77f, diameter * 0.77f), Color.white);
            icon.sprite = Icon(action); icon.preserveAspect = true; if (!icon.sprite) icon.enabled = false;
            var text = Text("Label", rt, new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(diameter + 34f, 26f), 15f, Cream);
            text.rectTransform.pivot = new Vector2(0.5f, 1f); text.text = label;
            var button = rt.gameObject.AddComponent<TouchActionButton>(); button.action = action;
            skillButtons[action] = (button, text);
            if (action == TouchAction.Craft) { button.sendsInput = false; button.pressed = ToggleCraft; }
        }

        public void ShowTouchControls(bool on)
        {
            if (!touchRoot) return;
            touchRoot.SetActive(on); TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled = on;
            TouchBinding.Shared.OverlayDesktop = on && LocalPlayer && LocalPlayer.Binding is DesktopBinding;
            TouchBinding.Shared.OverlayBindingId = on && LocalPlayer && LocalPlayer.Binding is not TouchBinding ? LocalPlayer.Binding?.Id : null;
            if (desktopHints) desktopHints.SetActive(!on);
            if (!on && LocalPlayer) LocalPlayer.Summoner.Close();
        }
        void JoinOrReady()
        {
            if (!joins) return;
            if (!LocalPlayer) { TouchBinding.Shared.Enabled = true; LocalPlayer = joins.Join(TouchBinding.Shared); TouchBinding.Shared.OverlayDesktop = false; TouchBinding.Shared.OverlayBindingId = null; }
            else TouchBinding.Shared.Pulse(TouchAction.Start);
        }
        void ToggleCraft()
        {
            if (!LocalPlayer || !LocalPlayer.CanAct) return;
            if (LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling) CancelCraft();
            else TouchBinding.Shared.SetCraftOpen(true);
        }
        void ConfirmCraft() { if (LocalPlayer) LocalPlayer.Summoner.CraftSelected(); TouchBinding.Shared.SetCraftOpen(false); }
        void CancelCraft()
        {
            if (LocalPlayer) { LocalPlayer.Summoner.Close(); LocalPlayer.Summoner.CancelCraft(); }
            TouchBinding.Shared.SetCraftOpen(false);
        }

        void LateUpdate()
        {
            if (!safe) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafe) ApplySafeArea();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.08f;
            FindLocalPlayer(); RefreshCards(); RefreshBag(); RefreshCraft(); RefreshSkills(); RefreshNavigation();
            // Round start/retry lives in ModeActions. Keep this join control separate so their
            // canvases cannot overlap after a touch player has joined.
            if (playRect) playRect.gameObject.SetActive(!LocalPlayer);
            if (playLabel) playLabel.text = "PLAY";
        }
        void FindLocalPlayer()
        {
            if (LocalPlayer && LocalPlayer.isActiveAndEnabled) return;
            LocalPlayer = null;
            foreach (var p in World.Players) if (p && (p.Binding is TouchBinding || p.Binding is DesktopBinding)) { LocalPlayer = p; break; }
            if (!LocalPlayer) foreach (var p in World.Players) if (p && p.Binding is not ScriptedBinding) { LocalPlayer = p; break; }
            TouchBinding.Shared.OverlayDesktop = TouchControlsShown && LocalPlayer && LocalPlayer.Binding is DesktopBinding;
            TouchBinding.Shared.OverlayBindingId = TouchControlsShown && LocalPlayer && LocalPlayer.Binding is not TouchBinding ? LocalPlayer.Binding?.Id : null;
        }
        void RefreshCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var current = players ?? joins?.Players;
                var card = cards[i]; var p = current != null && i < current.Count ? current[i] : null;
                card.root.SetActive(p); if (!p) continue;
                card.badge.color = p.Color; card.initial.text = p.Initial.ToString();
                card.name.text = p.Name + (showWins && wins != null ? $"   {wins(p)}/{roundsToWin}" : "");
                card.fill.fillAmount = p.Health.Fraction; card.fill.color = p.Health.Fraction > 0.35f ? Mint : Coral;
                card.status.text = p.IsEliminated ? "WRECKED" : p.IsDowned ? $"REVIVE ME  {Mathf.CeilToInt(p.Health.BleedOutLeft)}s" : $"{Mathf.CeilToInt(p.Health.Current)} HP   {p.Inventory.TotalCount}/{p.Inventory.Capacity} letters";
            }
        }
        void RefreshBag()
        {
            bagRoot.SetActive(LocalPlayer); if (!LocalPlayer) return;
            var inv = LocalPlayer.Inventory;
            bagLabel.text = $"YOUR LETTER BAG   {inv.TotalCount}/{inv.Capacity}" + (inv.ReservedCount > 0 ? $"   ({inv.ReservedCount} building)" : "");
            for (int i = 0; i < tileLabels.Length; i++)
            {
                bool occupied = i < inv.Count; tileFaces[i].color = occupied ? Cream : new Color(0.98f, 0.95f, 0.86f, 0.12f);
                tileLabels[i].text = occupied ? inv.Letters[i].ToString() : ""; tileFaces[i].gameObject.SetActive(i < inv.Capacity);
            }
            var weapon = LocalPlayer.Combat.Weapon;
            var spare = LocalPlayer.Combat.StoredGear;
            heldLabel.text = LocalPlayer.Combat.IsHolding ? (weapon ? $"IN HAND: {weapon.word}" + (spare ? $"   •   SPARE: {spare.word}   •   SWAP" : "   •   attack to use") : "FURNITURE   •   GRAB to throw   •   HOLD DROP") : spare ? $"SPARE: {spare.word}   •   SWAP to equip" : "Empty hands   •   smash furniture for letters";
        }
        void RefreshCraft()
        {
            if (!LocalPlayer) { craftRoot.SetActive(false); return; }
            var summon = LocalPlayer.Summoner; bool visible = summon.IsSpelling || summon.IsCrafting; craftRoot.SetActive(visible);
            if (!visible) { if (TouchBinding.Shared.CraftOpen && !TouchBinding.Shared.CraftPressPending) TouchBinding.Shared.SetCraftOpen(false); return; }
            bool building = summon.IsCrafting;
            craftStatus.text = building ? $"BUILDING {summon.CraftWord}…  {Mathf.RoundToInt(summon.CraftProgress * 100f)}%" : summon.Ready.Count > 0 ? "Pick a word. Its letters become a real object." : "Smash furniture and find the missing letters.";
            craftProgress.fillAmount = building ? summon.CraftProgress : 0f;
            buildButton.interactable = !building && summon.SelectedWord != null; buildLabel.text = building ? "BUILDING…" : "BUILD";
            int start = Mathf.Max(0, summon.Selected - 1);
            for (int i = 0; i < recipes.Count; i++)
            {
                var view = recipes[i]; view.index = start + i; bool ready = view.index < summon.Ready.Count;
                view.button.interactable = ready && !building;
                if (ready)
                {
                    var word = summon.Ready[view.index]; bool selected = view.index == summon.Selected;
                    view.face.color = selected ? Mint : new Color(0.2f, 0.29f, 0.30f); view.text.color = selected ? Ink : Cream;
                    view.text.text = $"{word.word}     <size=70%>{word.word.Length} letters</size>";
                    view.icon.texture = itemIcons.TryGetValue(word.word, out var texture) ? texture : null;
                    view.icon.color = Color.white; view.icon.enabled = view.icon.texture;
                }
                else
                {
                    int hint = view.index - summon.Ready.Count; view.face.color = new Color(0.2f, 0.29f, 0.30f, 0.5f); view.text.color = new Color(0.98f, 0.95f, 0.86f, 0.5f);
                    view.text.text = hint < summon.Hints.Count ? $"{summon.Hints[hint].entry.word}     <size=70%>find {summon.Hints[hint].missing}</size>" : "";
                    view.icon.texture = hint < summon.Hints.Count && itemIcons.TryGetValue(summon.Hints[hint].entry.word, out var texture) ? texture : null;
                    view.icon.color = new Color(1f, 1f, 1f, 0.45f); view.icon.enabled = view.icon.texture;
                }
            }
        }
        void RefreshSkills()
        {
            if (!LocalPlayer) return;
            var combat = LocalPlayer.Combat;
            var weapon = combat.Weapon;
            bool canAct = LocalPlayer.CanAct && !LocalPlayer.IsDodging;
            bool free = canAct && !LocalPlayer.Summoner.IsSpelling && !LocalPlayer.Summoner.IsCrafting && !combat.IsChanneling;
            foreach (var pair in skillButtons)
            {
                bool available = pair.Key switch
                {
                    TouchAction.Block => free && weapon && weapon.Shield != null,
                    TouchAction.Deploy => free && weapon && weapon.Definition?.Deploy != null,
                    TouchAction.Drop => free && combat.IsHolding,
                    TouchAction.Swap => free && combat.StoredGear,
                    TouchAction.Dodge => canAct && LocalPlayer.Health.CanDodge,
                    TouchAction.Jump => canAct,
                    TouchAction.Craft => canAct && (LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling || !combat.IsChanneling),
                    _ => free,
                };
                pair.Value.button.SetAvailable(available);
            }
            skillButtons[TouchAction.Grab].label.text = combat.IsReviving ? "REVIVING" : combat.IsHolding ? "THROW" : combat.DownedTeammateNearby() ? "HOLD REVIVE" : "GRAB";
            skillButtons[TouchAction.Craft].label.text = LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling ? "CANCEL" : "CRAFT";
            skillButtons[TouchAction.Attack].label.text = weapon && weapon.Definition != null ? weapon.Definition.Family switch
            { HandlingFamily.Thrown => "THROW", HandlingFamily.Ranged => "FIRE", HandlingFamily.Heal or HandlingFamily.Buff => "USE", _ => "SMASH" } : "SMASH";
        }
        void RefreshNavigation()
        {
            bool usingMenu = typewriter && LocalPlayer && typewriter.User == LocalPlayer;
            typewriterControls.SetActive(usingMenu);
            if (!usingMenu) return;
            var mode = typewriter.Modes[typewriter.Selected];
            typewriterChoice.text = mode.label + "\n<size=65%>" + mode.blurb + "</size>";
        }
        void ApplySafeArea()
        {
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            safe.anchorMin = new Vector2(lastSafe.xMin / lastWidth, lastSafe.yMin / lastHeight); safe.anchorMax = new Vector2(lastSafe.xMax / lastWidth, lastSafe.yMax / lastHeight);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            // Portrait browser windows keep the bag above the thumb controls; mobile players
            // normally use landscape, but resizing must never hide the craft economy.
            if (bagRoot)
            {
                bool portrait = lastWidth < lastHeight;
                var bag = (RectTransform)bagRoot.transform;
                bag.anchorMin = bag.anchorMax = new Vector2(0.5f, portrait ? 1f : 0f);
                bag.pivot = new Vector2(0.5f, portrait ? 1f : 0f);
                bag.anchoredPosition = new Vector2(0f, portrait ? -235f : 20f);
            }
            if (instruction)
            {
                bool portrait = lastWidth < lastHeight;
                float logicalWidth = lastSafe.width / Mathf.Max(0.01f, UiCanvas.scaleFactor);
                instruction.rectTransform.sizeDelta = new Vector2(Mathf.Min(1200f, logicalWidth - 64f), 108f);
                instruction.rectTransform.anchoredPosition = new Vector2(0f, portrait ? -195f : -122f);
            }
            if (playRect)
            {
                bool portrait = lastWidth < lastHeight;
                playRect.anchorMin = playRect.anchorMax = portrait ? Vector2.zero : new Vector2(0.5f, 1f);
                playRect.pivot = playRect.anchorMin;
                playRect.anchoredPosition = portrait ? new Vector2(30f, 340f) : new Vector2(0f, -185f);
            }
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused) return; TouchBinding.Shared.ReleaseAll();
            foreach (var stick in sticks) if (stick) stick.Release(); if (LocalPlayer) LocalPlayer.Summoner.Close();
        }
        void OnDisable() => TouchBinding.Shared.ReleaseAll();
        void OnDestroy()
        {
            foreach (var sprite in ownedSprites) if (sprite) Destroy(sprite);
            foreach (var texture in ownedTextures) if (texture) Destroy(texture);
        }
        public void SetTitle(string text, string sub = "") { if (title) title.text = text; if (subtitle) subtitle.text = sub; }
        public void SetInstruction(string main, string hint = "") { if (instruction) instruction.text = string.IsNullOrEmpty(hint) ? main : $"{main}\n<size=70%><color=#FFF4E0CC>{hint}</color></size>"; }
        public void SetChecklist(string text) { if (checklist) checklist.text = text; if (checklistPanel) checklistPanel.SetActive(!string.IsNullOrEmpty(text)); }
        public void SetTimer(string text) { if (timer) timer.text = text; }
        public void SetScoreboard(IReadOnlyList<PlayerController> currentPlayers, Func<PlayerController, int> currentWins, int targetWins, bool winsVisible)
        { players = currentPlayers; wins = currentWins; roundsToWin = targetWins; showWins = winsVisible; }

        Sprite Icon(TouchAction action)
        {
            if (!iconAtlas || (int)action > 8) return null;
            // Measured silhouettes avoid bleed where the generated atlas differs from a regular grid.
            var bounds = new Rect[] { new(34,19,391,376), new(453,100,369,286), new(925,28,280,373), new(39,445,365,336), new(499,434,313,353), new(874,439,333,346), new(35,833,381,375), new(487,813,281,405), new(836,870,410,314) };
            var r = bounds[(int)action]; float sx = iconAtlas.width / 1254f, sy = iconAtlas.height / 1254f;
            var sprite = Sprite.Create(iconAtlas, new Rect(r.x * sx, iconAtlas.height - (r.y + r.height) * sy, r.width * sx, r.height * sy), new Vector2(0.5f, 0.5f), 100f);
            ownedSprites.Add(sprite); return sprite;
        }
        Sprite MakeShape(bool circle)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = circle ? "HUD circle" : "HUD panel", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - 31.5f), dy = Mathf.Abs(y - 31.5f);
                float distance = circle ? Mathf.Sqrt(dx * dx + dy * dy) - 30f : new Vector2(Mathf.Max(dx - 20f, 0f), Mathf.Max(dy - 20f, 0f)).magnitude - 11f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, circle ? Vector4.zero : new Vector4(14f, 14f, 14f, 14f));
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }
        RectTransform Rect(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = position; rt.sizeDelta = size; return rt;
        }
        RectTransform Panel(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color color, bool circle = false)
        {
            var rt = Rect(label, parent, anchor, position, size); rt.pivot = pivot;
            var image = rt.gameObject.AddComponent<Image>(); image.sprite = circle ? roundSprite : panelSprite;
            image.type = circle ? Image.Type.Simple : Image.Type.Sliced; image.color = color; image.raycastTarget = false; return rt;
        }
        Image CreateImage(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(label, parent, anchor, position, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        Image Fill(string label, Transform parent, Color color)
        {
            var image = CreateImage(label, parent, Vector2.zero, Vector2.zero, Vector2.zero, color); image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero; image.sprite = panelSprite;
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; return image;
        }
        TextMeshProUGUI Text(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Rect(label, parent, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset; text.fontSize = fontSize; text.fontStyle = FontStyles.Bold;
            text.color = color; text.alignment = alignment; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false; return text;
        }
        Button MakeButton(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string caption, Action action)
        {
            var rt = Panel(label, parent, anchor, position, size, anchor, Mint); rt.GetComponent<Image>().raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => action?.Invoke());
            Text("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(8f, 4f), 20f, Ink).text = caption; return button;
        }
    }
}
