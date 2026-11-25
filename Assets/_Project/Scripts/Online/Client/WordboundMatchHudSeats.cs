using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const float SeatTile = 46f, SeatSnugTile = 40f, SeatBarHeight = 18f, SeatNumberPad = 3f, SeatOutline = 2f, SeatYou = 2f,
            CentrePad = 10f, CentreScoreGap = 6f, CentreClockShare = .55f, CentreMark = 14f, CentreMarkGap = 3f;
        const int SeatLabelFont = 20, SeatNumberFont = 16, CentreClockFont = 22, CentreScoreFont = 18;
        static readonly Color HudOutSeat = KitHex(0x6d7472), HudOutShade = KitHex(0x0b1515, .55f);
        static readonly string[] SeatNumbers = { "1", "2", "3", "4", "5", "6", "7", "8" };
        GUIStyle kitSeatLabel, kitSeatNumber, kitCentreClock, kitCentreScore;
        MatchSnapshot seatFrame;
        MatchActorState seatActor;
        string seatRoster;
        int seatCount, seatSplit, seatMine = -1;
        bool seatTwoTeams;
        int[] seatOrder = Array.Empty<int>(), seatTeam = Array.Empty<int>(), seatReading = Array.Empty<int>();
        float[] seatShare = Array.Empty<float>(), seatPeak = Array.Empty<float>();
        HealthBand[] seatBand = Array.Empty<HealthBand>();
        bool[] seatOut = Array.Empty<bool>();
        string[] seatLook = Array.Empty<string>();
        Color[] seatColour = Array.Empty<Color>(), seatTeamColour = Array.Empty<Color>();
        GUIContent[] seatLabel = Array.Empty<GUIContent>(), seatNumber = Array.Empty<GUIContent>();
        Rect[] seatTileRects = Array.Empty<Rect>();
        Rect seatStripRect, seatCentreRect, seatLeadRect;

        bool SeatStripShown => GuidanceVisible && PlayerBar.Visible(frame.Binding.Seats.Count);

        void RefreshSeats()
        {
            if (frame == null || ReferenceEquals(seatFrame, frame) && ReferenceEquals(seatActor, actor)) return;
            seatFrame = frame;
            seatActor = actor;
            var binding = frame.Binding;
            if (!string.Equals(binding.Hash, seatRoster, StringComparison.Ordinal)) ArrangeRoster(binding);
            for (int i = 0; i < seatCount; i++) ReadSeat(i, binding.Seats[i].PlayerId);
        }

        void ArrangeRoster(MatchBinding binding)
        {
            seatRoster = binding.Hash;
            var seats = binding.Seats;
            if (seatCount != seats.Count || seatOrder.Length != seats.Count) SizeSeats(seats.Count);
            seatMine = -1;
            for (int i = 0; i < seatCount; i++)
            {
                seatTeam[i] = seats[i].Team;
                seatColour[i] = TeamColour(i);
                seatTeamColour[i] = TeamColour(seats[i].Team);
                seatPeak[i] = 0f;
                seatReading[i] = int.MinValue;
                seatLook[i] = null;
                seatNumber[i].text = "";
                if (seats[i].PlayerId == actorId) seatMine = i;
            }
            int mine = seatMine >= 0 ? seatTeam[seatMine] : team;
            seatSplit = ArrangeSeats(seats, mine, mode != null && mode.TimeLimitSeconds > 0, seatOrder, out seatTwoTeams);
        }

        void SizeSeats(int count)
        {
            seatCount = count;
            seatOrder = new int[count]; seatTeam = new int[count]; seatReading = new int[count];
            seatShare = new float[count]; seatPeak = new float[count];
            seatBand = new HealthBand[count]; seatOut = new bool[count]; seatLook = new string[count];
            seatColour = new Color[count]; seatTeamColour = new Color[count]; seatTileRects = new Rect[count];
            seatLabel = new GUIContent[count]; seatNumber = new GUIContent[count];
            for (int i = 0; i < count; i++) { seatLabel[i] = new GUIContent(); seatNumber[i] = new GUIContent(); }
        }

        void ReadSeat(int index, string id)
        {
            var state = id == actorId && actor != null ? actor : ActorOf(id);
            var progress = ProgressOf(id);
            bool away = progress != null && (progress.Eliminated || !progress.Connected || progress.Returned);
            bool alive = state != null && state.Health > 0f && !away;
            if (state != null) seatPeak[index] = Mathf.Max(seatPeak[index], state.Health, HealthFloor);
            seatShare[index] = alive ? SeatVitals.Share(state.Health, healthMax > 0f ? healthMax : seatPeak[index]) : 0f;
            seatBand[index] = SeatVitals.Band(seatShare[index]);
            seatOut[index] = away || state != null && state.Health <= 0f;
            int reading = state == null ? SeatVitals.Hidden : SeatVitals.Reading(state.Health, alive, false, 0f, false);
            if (reading != seatReading[index]) { seatReading[index] = reading; seatNumber[index].text = SeatVitals.Label(reading); }
            string look = state?.Appearance ?? "";
            if (string.Equals(look, seatLook[index], StringComparison.Ordinal)) return;
            seatLook[index] = look;
            seatLabel[index].text = SeatLabelFor(look, index);
        }

        static string SeatLabelFor(string look, int index)
        {
            if (look.Length != 0)
            {
                var outfit = Outfit.Deserialize(look);
                if (LetterBadge.IsLetter(outfit.Letter)) return outfit.Letter;
            }
            return index >= 0 && index < SeatNumbers.Length ? SeatNumbers[index] : (index + 1).ToString();
        }

        MatchActorState ActorOf(string id)
        {
            var actors = frame.Actors;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].PlayerId == id) return actors[i];
            return null;
        }

        PlayerProgress ProgressOf(string id)
        {
            if (frame == null) return null;
            var players = frame.Progress.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i].Id == id) return players[i];
            return null;
        }

        static int ArrangeSeats(IReadOnlyList<WordboundSeat> seats, int mine, bool divided, int[] order, out bool twoTeams)
        {
            int count = Math.Min(seats.Count, order.Length), rival = -1, own = 0;
            bool two = true;
            for (int i = 0; i < count; i++)
            {
                int side = seats[i].Team;
                if (side == mine) own++;
                else if (rival < 0) rival = side;
                else if (side != rival) two = false;
            }
            twoTeams = two && own > 0 && rival >= 0;
            if (!twoTeams)
            {
                for (int i = 0; i < count; i++) order[i] = i;
                return divided ? (count + 1) / 2 : count;
            }
            int at = 0;
            for (int i = 0; i < count; i++) if (seats[i].Team == mine) order[at++] = i;
            for (int i = 0; i < count; i++) if (seats[i].Team != mine) order[at++] = i;
            return own;
        }

        float DrawSeatStrip(float width, Rect status)
        {
            RefreshSeats();
            RefreshScore();
            if (seatCount < 2 || kitSeatNumber == null) return DrawScoreStrip(width, status);
            float tile = PlayerBar.Snug(seatCount) ? SeatSnugTile : SeatTile, high = tile + SeatBarHeight + SeatOutline * 2f;
            float leftWide = SideWidth(0, seatSplit, tile), rightWide = SideWidth(seatSplit, seatCount, tile);
            float centre = rightWide > 0f ? CentreWidth() : 0f, total = leftWide + centre + rightWide;
            float left = Mathf.Round((width - total) * .5f), top = EyeMargin;
            bool crowded = (status.width > 0f && left < status.xMax + ScoreGap) || left + total > ScoreRightEdge(width) - ScoreGap;
            if (crowded) top = Mathf.Max(status.yMax, ScoreRightBottom()) + ScoreNotesGap;
            PaintSide(0, seatSplit, left, top, tile);
            var middle = new Rect(left + leftWide, top, centre, high);
            if (centre > 0f) PaintCentre(middle);
            PaintSide(seatSplit, seatCount, middle.xMax, top, tile);
            var area = new Rect(left, top, total, high);
            if (Painting) { seatStripRect = area; seatCentreRect = middle; }
            return area.yMax + ScoreNotesGap;
        }

        bool RunStarts(int position, int from) =>
            position == from || seatTeam[seatOrder[position]] != seatTeam[seatOrder[position - 1]];

        float SideWidth(int from, int to, float tile)
        {
            float wide = 0f;
            for (int p = from; p < to; p++) wide += tile + (RunStarts(p, from) ? SeatOutline * 2f : 0f);
            return wide;
        }

        void PaintSide(int from, int to, float x, float top, float tile)
        {
            for (int start = from; start < to;)
            {
                int end = start + 1;
                while (end < to && !RunStarts(end, from)) end++;
                var outline = new Rect(x, top, (end - start) * tile + SeatOutline * 2f, tile + SeatBarHeight + SeatOutline * 2f);
                KitFrame(outline, SeatOutline, seatTwoTeams ? seatTeamColour[seatOrder[start]] : HudGlassEdge);
                for (int p = start; p < end; p++)
                    PaintSeat(p, new Rect(x + SeatOutline + (p - start) * tile, top + SeatOutline, tile, tile));
                x = outline.xMax;
                start = end;
            }
        }

        void PaintSeat(int position, Rect tile)
        {
            int index = seatOrder[position];
            bool away = seatOut[index];
            KitFlat(tile, away ? HudOutSeat : seatColour[index]);
            KitPlain(tile, seatLabel[index], kitSeatLabel, HudInk);
            if (away) KitFlat(tile, HudOutShade);
            if (index == seatMine) KitFrame(tile, SeatYou, Color.white);
            var bar = new Rect(tile.x, tile.yMax, tile.width, SeatBarHeight);
            PaintVitalsBar(bar, seatNumber[index], seatShare[index], seatBand[index], kitSeatNumber, SeatNumberPad);
            if (Painting) seatTileRects[position] = tile;
        }

        float CentreWidth()
        {
            float clock = scoreClockShown ? Mathf.Max(kitCentreClock.CalcSize(scoreClock).x, kitCentreClock.CalcSize(scoreClockWidest).x) : 0f;
            float score = scoreSidesShown ? kitCentreScore.CalcSize(scoreSeparator).x + 2f * (CentreScoreGap + CentreMarkGap + CentreMark +
                Mathf.Max(kitCentreScore.CalcSize(scoreMine).x, kitCentreScore.CalcSize(scoreTheirs).x)) : 0f;
            float inner = Mathf.Max(clock, score);
            return inner > 0f ? Mathf.Ceil(inner + CentrePad * 2f) : 0f;
        }

        void PaintCentre(Rect area)
        {
            KitFlat(area, KeyCapFill);
            KitFrame(area, SeatOutline, HudGlassEdge);
            var inner = new Rect(area.x, area.y + SeatOutline, area.width, area.height - SeatOutline * 2f);
            bool both = scoreClockShown && scoreSidesShown;
            if (Painting) seatLeadRect = default;
            float split = both ? Mathf.Round(inner.height * CentreClockShare) : inner.height;
            if (scoreClockShown) KitPlain(new Rect(inner.x, inner.y, inner.width, split), scoreClock, kitCentreClock, EyeInk);
            if (!scoreSidesShown) return;
            var row = both ? new Rect(inner.x, inner.y + split, inner.width, inner.height - split) : inner;
            float middle = row.center.x, separator = kitCentreScore.CalcSize(scoreSeparator).x;
            float mine = kitCentreScore.CalcSize(scoreMine).x, theirs = kitCentreScore.CalcSize(scoreTheirs).x;
            KitPlain(new Rect(middle - separator * .5f - CentreScoreGap - mine, row.y, mine, row.height), scoreMine, kitCentreScore, scoreMineColour);
            KitPlain(new Rect(middle - separator * .5f, row.y, separator, row.height), scoreSeparator, kitCentreScore, EyeInk);
            var theirsRect = new Rect(middle + separator * .5f + CentreScoreGap, row.y, theirs, row.height);
            KitPlain(theirsRect, scoreTheirs, kitCentreScore, scoreTheirsColour);
            if (scoreLead == ScoreLine.Left) PaintCentreMark(middle - separator * .5f - CentreScoreGap - mine - CentreMarkGap - CentreMark, row.center.y);
            else if (scoreLead == ScoreLine.Right) PaintCentreMark(theirsRect.xMax + CentreMarkGap, row.center.y);
        }

        void PaintCentreMark(float x, float middle)
        {
            if (!scoreTrophy) scoreTrophy = LobbyIcons.Get(LobbyIcons.Trophy).texture;
            var at = new Rect(Mathf.Round(x), Mathf.Round(middle - CentreMark * .5f), CentreMark, CentreMark);
            KitImage(at, scoreTrophy, ArrowTint);
            if (Painting) seatLeadRect = at;
        }
    }
}
