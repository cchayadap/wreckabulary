using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class MovingOutDirector : MonoBehaviour
    {
        public enum State { Waiting, Countdown, Playing, Complete, Failed }
        PlayerJoinManager joins;
        RoomBuilder room;
        DeliverySpawner deliveries;
        GameHud hud;
        ClearOutController clearOut;
        readonly List<Keepsake> keepsakes = new();
        Vector3 extraction;
        float began, nextCheck, finishedAt;
        public State Current { get; private set; }
        public int PackedCount => keepsakes.Count(k => k.Packed);
        public int KeepsakeCount => keepsakes.Count;
        public Vector3 ExtractionPoint => extraction;
        public float TimeLeft => Mathf.Max(0f, Match.Rules.RoundTimeLimitSeconds - (Time.time - began));

        public void Configure(PlayerJoinManager players, RoomBuilder builder, DeliverySpawner boxes, GameHud display)
        {
            joins = players; room = builder; deliveries = boxes; hud = display;
        }

        void Start()
        {
            deliveries.Running = false;
            clearOut = gameObject.AddComponent<ClearOutController>();
            clearOut.Configure(room.Layout, "MovingOut");
            extraction = new Vector3(room.Layout.ExtractionX, room.Layout.Room(room.Layout.ExtractionRoom).FloorY, room.Layout.ExtractionZ);
            joins.Joined += OnJoined;
            joins.RespawnKnockedOut = false;
            CreateVan();
            Current = State.Waiting;
            if (joins.HumanCount > 0) Restart();
        }

        void OnJoined(PlayerController p)
        {
            joins.AssignTeams();
            p.Health.ResetForRound();
            p.Frozen = Current != State.Playing;
        }

        public void Restart()
        {
            if (joins.HumanCount == 0) return;
            room.ResetRoom();
            clearOut.ResetSchedule();
            hud.HideResult();
            hud.HideCountdown();
            MatchTally.BeginMatch();
            keepsakes.Clear();
            foreach (var objective in room.Layout.Keepsakes)
            {
                var prop = room.Originals.Where(s => !keepsakes.Any(k => k.gameObject == s.gameObject))
                    .Where(s => room.Layout.RoomAt(s.transform.position.x, s.transform.position.y, s.transform.position.z) == objective.Room)
                    .OrderBy(s => Vector2.Distance(new Vector2(s.transform.position.x, s.transform.position.z), new Vector2(objective.X, objective.Z)))
                    .First();
                prop.Invulnerable = true;
                var token = prop.gameObject.AddComponent<Keepsake>();
                token.Configure(objective.Room);
                keepsakes.Add(token);
            }
            joins.AssignTeams();
            foreach (var p in joins.Players) { joins.Place(p); p.Frozen = true; }
            joins.AllowJoining = true;
            joins.RespawnKnockedOut = false;
            Current = State.Countdown;
            began = Time.time;
        }

        void Update()
        {
            switch (Current)
            {
                case State.Waiting:
                    hud.SetInstruction(ControlHints.Join("join"), "");
                    if (joins.HumanCount > 0) Restart();
                    break;
                case State.Countdown:
                    hud.SetInstruction("", "");
                    hud.ShowCountdown(Mathf.Max(1, Mathf.CeilToInt(3f - (Time.time - began))), $"MOVING OUT · {room.Layout.Name.ToUpperInvariant()}");
                    if (Time.time - began >= 3f)
                    {
                        Current = State.Playing; began = Time.time;
                        foreach (var p in joins.Players) p.Frozen = false;
                        clearOut.Begin();
                        hud.ShowGo();
                        hud.Toast($"Carry {KeepsakeCount} keepsakes to the van. Drop them inside the circle.");
                    }
                    break;
                case State.Playing:
                    hud.SetTimer(RoundManager.FormatTime(TimeLeft));
                    hud.SetInstruction(clearOut.Message, "");
                    CheckObjectives();
                    break;
                case State.Complete:
                case State.Failed:
                    if (hud.ResultShown && Time.time - finishedAt > 1.6f && joins.AnyStartPressed()) hud.ConfirmResult();
                    break;
            }
            hud.SetChecklist("<b>KEEPSAKES</b>\n" + string.Join("\n", keepsakes.Select(k => k.Packed
                ? $"<color=#8FD18B>✓ {k.Word} packed</color>" : $"{k.Word} • {k.Room}")));
            hud.SetScoreboard(joins.Players, _ => 0, 0, false);
        }

        void CheckObjectives()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + .2f;
            foreach (var keepsake in keepsakes)
            {
                if (keepsake.Packed) continue;
                var body = keepsake.GetComponent<Rigidbody>();
                if (keepsake.transform.position.y < -4f) keepsake.ReturnToStart();
                bool held = joins.Players.Any(p => p.Combat.Held == body);
                if (!held && AtVan(body.worldCenterOfMass))
                {
                    keepsake.Pack();
                    hud.Toast("A keepsake saved! Find the others.");
                }
            }
            var combatants = joins.Players.Select(p => new Combatant(p.Index, p.Team, p.Health.State)).ToArray();
            foreach (int id in WinCheck.Unrevivable(combatants)) World.PlayerById(id)?.Health.Eliminate();
            var survivors = joins.Players.Where(p => !p.IsEliminated).ToArray();
            if (survivors.Length == 0 || TimeLeft <= 0f) Finish(false);
            else if (PackedCount == KeepsakeCount && survivors.All(p => p.Health.IsAlive && AtVan(p.transform.position))) Finish(true);
        }

        public bool AtVan(Vector3 p) => World.Flat(p - extraction).sqrMagnitude <= 2.4f * 2.4f && Mathf.Abs(p.y - extraction.y) < 1.5f;

        void Finish(bool won)
        {
            bool wiped = joins.Players.All(p => p.IsEliminated);
            Current = won ? State.Complete : State.Failed;
            finishedAt = Time.time;
            clearOut.Running = false;
            foreach (var p in joins.Players) p.Frozen = true;
            World.FreezeTransient();
            joins.AllowJoining = false;
            hud.SetTimer("");
            string heading = won ? "All packed. Everybody aboard!" : wiped ? "The Movers got your crew." : "The van had to leave.";
            hud.ShowResult(HudResult.Of(1, true, won, heading, MatchTally.Finish(won)), Restart);
        }

        void CreateVan()
        {
            var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circle.name = "Van extraction circle";
            circle.transform.SetParent(transform, false);
            circle.transform.position = extraction + Vector3.up * .01f;
            circle.transform.localScale = new Vector3(4.8f, .01f, 4.8f);
            Destroy(circle.GetComponent<Collider>());
            circle.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(.34f, .63f, .59f));
            var text = new GameObject("Van label").AddComponent<TextMeshPro>();
            text.transform.SetParent(transform, false);
            text.transform.position = extraction + Vector3.up * .035f;
            text.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            text.font = GameAssets.I.font;
            text.fontSize = 6f; text.text = "VAN"; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(4f, 2f);
        }
    }

    public sealed class Keepsake : MonoBehaviour
    {
        Vector3 initial;
        TextMeshPro marker;
        public string Room { get; private set; }
        public string Word => GetComponent<Smashable>().Word;
        public bool Packed { get; private set; }
        public void Configure(string room)
        {
            Room = room; initial = transform.position;
            marker = new GameObject("Keepsake marker").AddComponent<TextMeshPro>();
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.up * 1.8f;
            marker.font = GameAssets.I.font;
            marker.fontSize = 4f; marker.color = new Color(1f, .8f, .27f);
            marker.text = "★ " + Word; marker.alignment = TextAlignmentOptions.Center;
            marker.rectTransform.sizeDelta = new Vector2(4f, 1f);
        }
        void LateUpdate() { if (marker) Popup.Billboard(marker.transform); }
        public void ReturnToStart()
        {
            transform.position = initial + Vector3.up * .2f;
            var body = GetComponent<Rigidbody>(); body.linearVelocity = body.angularVelocity = Vector3.zero;
        }
        public void Pack()
        {
            Packed = true;
            var body = GetComponent<Rigidbody>(); body.linearVelocity = body.angularVelocity = Vector3.zero; body.isKinematic = true;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            marker.text = "✓ " + Word; marker.color = new Color(.55f, .85f, .57f);
        }
    }
}
