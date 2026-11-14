using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct PartySeat
    {
        public readonly float X, Y, Z;
        public readonly bool Human, Eliminated;

        public PartySeat(float x, float y, float z, bool human = true, bool eliminated = false)
        {
            X = x; Y = y; Z = z;
            Human = human; Eliminated = eliminated;
        }
    }

    public readonly struct PartyArea
    {
        public readonly float MinX, MaxX, MinZ, MaxZ;

        public PartyArea(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ;
        }

        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public float CentreX => (MinX + MaxX) * .5f;
        public float CentreZ => (MinZ + MaxZ) * .5f;
    }

    public struct PartyShot
    {
        public float X, Y, Z, Size, Widest;
        public float MinX, MaxX, MinZ, MaxZ;
        public int Framed;
        public bool Solo;
    }

    public struct PartyView
    {
        public float X, Y, Z, Size;
        public float SpeedX, SpeedY, SpeedZ, SpeedSize;
        public bool Placed;

        public static PartyView At(float x, float y, float z, float size) =>
            new PartyView { X = x, Y = y, Z = z, Size = size, Placed = true };
    }

    public static class PartyCamera
    {
        public const float Rise = 30f, Back = 22f, Lean = Back / Rise;
        public const float DepthScale = .85f, HousePad = .8f, MinAspect = .5f;
        public const float MinSize = 9.3f;
        public const float Margin = 2f, SafeWidth = .84f, SafeHeight = .76f, BodyMargin = 1f;
        public const float PanRate = 4f, ZoomOutRate = 5f, ZoomInRate = 2.5f;

        public static float HouseSize(float width, float depth, float aspect) =>
            Math.Max(width / Math.Max(MinAspect, aspect), depth * DepthScale) * .5f + HousePad;

        public static float HalfWidth(float size, float aspect) => size * Math.Max(MinAspect, aspect);
        public static float HalfDepth(float size) => size / DepthScale;

        public static PartyShot Aim(IReadOnlyList<PartySeat> seats, PartyArea house, float aspect, float focusY)
        {
            aspect = Math.Max(MinAspect, aspect);
            var shot = new PartyShot { Y = focusY, Widest = HouseSize(house.Width, house.Depth, aspect) };
            bool humans = false;
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].Human && !seats[i].Eliminated) { humans = true; break; }
            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                if (seat.Eliminated || (humans && !seat.Human)) continue;
                float z = seat.Z + (seat.Y - focusY) * Lean;
                if (shot.Framed++ == 0)
                {
                    shot.MinX = shot.MaxX = seat.X;
                    shot.MinZ = shot.MaxZ = z;
                    shot.X = seat.X; shot.Z = seat.Z;
                    continue;
                }
                shot.MinX = Math.Min(shot.MinX, seat.X); shot.MaxX = Math.Max(shot.MaxX, seat.X);
                shot.MinZ = Math.Min(shot.MinZ, z); shot.MaxZ = Math.Max(shot.MaxZ, z);
            }
            float closest = Math.Min(MinSize, shot.Widest);
            if (shot.Framed == 0)
            {
                shot.X = house.CentreX; shot.Z = house.CentreZ;
                shot.Size = shot.Widest;
                return shot;
            }
            if (shot.Framed == 1 && humans)
            {
                shot.Solo = true;
                shot.X = Clamp(shot.X, house.MinX, house.MaxX);
                shot.Z = Clamp(shot.Z, house.MinZ, house.MaxZ);
                shot.Size = closest;
                return shot;
            }
            float wide = (shot.MaxX - shot.MinX + 2f * Margin) / (aspect * SafeWidth);
            float deep = (shot.MaxZ - shot.MinZ + 2f * Margin) * DepthScale / SafeHeight;
            shot.Size = Clamp(Math.Max(wide, deep) * .5f, closest, shot.Widest);
            shot.X = Centre(shot.MinX, shot.MaxX, house.MinX, house.MaxX, HalfWidth(shot.Size, aspect), HalfWidth(HousePad, aspect), SafeWidth);
            shot.Z = Centre(shot.MinZ, shot.MaxZ, house.MinZ, house.MaxZ, HalfDepth(shot.Size), HalfDepth(HousePad), SafeHeight);
            return shot;
        }

        public static PartyShot Frame(IReadOnlyList<PartySeat> seats, PartyArea house, float aspect, float focusY, ref PartyView view, float dt)
        {
            var shot = Aim(seats, house, aspect, focusY);
            if (!view.Placed)
            {
                view = PartyView.At(shot.X, shot.Y, shot.Z, shot.Size);
                return shot;
            }
            dt = Math.Max(0f, dt);
            Ease(ref view.X, ref view.SpeedX, shot.X, PanRate, dt);
            Ease(ref view.Y, ref view.SpeedY, shot.Y, PanRate, dt);
            Ease(ref view.Z, ref view.SpeedZ, shot.Z, PanRate, dt);
            Ease(ref view.Size, ref view.SpeedSize, shot.Size, shot.Size > view.Size ? ZoomOutRate : ZoomInRate, dt);
            if (shot.Framed > 0 && !shot.Solo) Contain(shot, aspect, ref view);
            return shot;
        }

        public static void Ease(ref float value, ref float speed, float goal, float rate, float dt)
        {
            float gap = value - goal;
            float push = (speed + rate * gap) * dt;
            float decay = (float)Math.Exp(-rate * dt);
            speed = (speed - rate * push) * decay;
            value = goal + (gap + push) * decay;
        }

        static void Contain(PartyShot shot, float aspect, ref PartyView view)
        {
            float across = Math.Max(shot.MaxX - view.X, view.X - shot.MinX) + BodyMargin;
            float along = Math.Max(shot.MaxZ - view.Z, view.Z - shot.MinZ) + BodyMargin;
            float least = Math.Min(shot.Widest, Math.Max(across / Math.Max(MinAspect, aspect), along * DepthScale));
            if (view.Size >= least) return;
            view.Size = least;
            view.SpeedSize = Math.Max(0f, view.SpeedSize);
        }

        static float Centre(float low, float high, float houseLow, float houseHigh, float half, float overhang, float safe)
        {
            float middle = (low + high) * .5f;
            float from = houseLow - overhang + half, to = houseHigh + overhang - half;
            float centre = from > to ? (houseLow + houseHigh) * .5f : Clamp(middle, from, to);
            float keep = half * safe - Margin;
            float least = high - keep, most = low + keep;
            centre = least > most ? middle : Clamp(centre, least, most);
            return Clamp(centre, houseLow, houseHigh);
        }

        static float Clamp(float value, float low, float high) => value < low ? low : value > high ? high : value;
    }
}
