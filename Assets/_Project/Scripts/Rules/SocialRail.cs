using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules
{
    [Flags]
    public enum SocialServices { None = 0, Friends = 1, Requests = 2, Nearby = 4, Chat = 8 }

    public enum ServiceState { Offline, NotChecked, Checking, Reachable, NoReply }

    public enum ServiceDot { None, Unknown, Good, Bad }

    public readonly struct RailLayout
    {
        public readonly float Width, Portrait, Top, Bottom, Right, Inset, Available, PanelWidth;
        public readonly bool Docked;

        public RailLayout(float width, float top, float bottom, float available, bool docked)
        {
            Width = width;
            Portrait = width - 2f * SocialRail.Pad;
            Top = top;
            Bottom = bottom;
            Right = SocialRail.Margin;
            Available = available;
            Docked = docked;
            Inset = docked ? SocialRail.Margin + width + SocialRail.Gap : 0f;
            PanelWidth = SocialRail.PanelWidth;
        }

        public float Left(float canvasWidth) => canvasWidth - Right - Width;

        public float PanelLeft(float canvasWidth) => Left(canvasWidth) - SocialRail.PanelGap - PanelWidth;
    }

    public static class SocialRail
    {
        public const float Width = 88f, TouchWidth = 96f, TouchPixels = 640f, Margin = 12f, Gap = 8f, Pad = 10f, Spacing = 8f;
        public const float DockMinimum = 1280f, PanelWidth = 320f, PanelGap = 8f, TopGap = 6f, Divider = 16f;
        public const float CountHeight = 48f, FooterClearance = 160f;
        public const string Server = "Server", Party = "Party", PortraitPrefix = "Portrait ", Invite = "Invite slot";
        public const string Friends = "Friends", Recent = "Recent", Nearby = "Nearby", Requests = "Requests", AddFriend = "Add friend";
        public const string SitOut = "Couch players sit out online.";

        public static float RailWidth(float screenPixelsHigh, bool touch) => touch || screenPixelsHigh < TouchPixels ? TouchWidth : Width;

        public static RailLayout Layout(float canvasWidth, float canvasHeight, float barHeight, float screenPixelsHigh, bool touch)
        {
            float width = RailWidth(screenPixelsHigh, touch);
            bool docked = canvasWidth - width >= DockMinimum;
            float top = barHeight + TopGap, bottom = docked ? Margin : FooterClearance;
            return new RailLayout(width, top, bottom, Math.Max(0f, canvasHeight - top - bottom), docked);
        }

        public static float Needed(RailLayout layout, int portraits, bool invite, int buttons)
        {
            int slots = Math.Max(0, portraits) + (invite ? 1 : 0);
            float step = layout.Portrait + Spacing;
            return 2f * Pad + CountHeight + Spacing + slots * step + Divider + Math.Max(0, buttons) * step;
        }

        public static bool Fits(RailLayout layout, int portraits, bool invite, int buttons) =>
            Needed(layout, portraits, invite, buttons) <= layout.Available;

        public static int PartyCount(int couchSeats, bool onlineActive, int onlineMembers) =>
            Clamp(onlineActive ? Math.Max(1, onlineMembers) : couchSeats);

        public static string CountText(int count) =>
            Clamp(count).ToString(CultureInfo.InvariantCulture) + "/" + PartyPlates.Places.ToString(CultureInfo.InvariantCulture);

        public static string Heading(int count, bool online) => "Party  " + CountText(count) + (online ? MapCardText.Separator + "online" : "");

        public static IReadOnlyList<PartyPlate> Portraits(string you, int couchGuests, bool onlineActive, IReadOnlyList<string> members)
        {
            if (onlineActive)
            {
                var online = PlayPageText.OnlineParty(members);
                if (online.Count > 0) return online;
            }
            return PartyPlates.Lineup(you, couchGuests, null).Where(plate => plate.Stands).ToArray();
        }

        public static bool InviteSlot(int count) => count < PartyPlates.Places;

        public static IReadOnlyList<string> Tabs(SocialServices services)
        {
            var tabs = new List<string>();
            if ((services & SocialServices.Friends) != 0) tabs.Add(Friends);
            tabs.Add(Recent);
            if ((services & SocialServices.Nearby) != 0) tabs.Add(Nearby);
            if ((services & SocialServices.Requests) != 0) tabs.Add(Requests);
            return tabs;
        }

        public static IReadOnlyList<string> Buttons(SocialServices services)
        {
            var buttons = new List<string>();
            if ((services & SocialServices.Nearby) != 0) buttons.Add(Nearby);
            buttons.Add(Recent);
            if ((services & SocialServices.Requests) != 0) buttons.Add(AddFriend);
            return buttons;
        }

        public static bool Chat(SocialServices services) => (services & SocialServices.Chat) != 0;

        public static IReadOnlyList<string> FocusOrder(int portraits, bool invite, IReadOnlyList<string> buttons)
        {
            var order = new List<string> { Server, Party };
            for (int place = 1; place <= Math.Min(Math.Max(portraits, 0), PartyPlates.Places); place++)
                order.Add(PortraitPrefix + place.ToString(CultureInfo.InvariantCulture));
            if (invite) order.Add(Invite);
            if (buttons != null) order.AddRange(buttons);
            return order;
        }

        public static string JumpTarget => Party;

        static int Clamp(int count) => Math.Min(Math.Max(count, 1), PartyPlates.Places);
    }

    public sealed class ServiceCheck
    {
        public const double Every = 30d;
        public const int NotRecorded = -1;
        readonly Func<double> clock;
        readonly bool online;
        double started, checkedAt;
        bool everChecked;

        public ServiceState State { get; private set; }
        public int Milliseconds { get; private set; } = NotRecorded;

        public ServiceCheck(bool online, Func<double> clock)
        {
            this.online = online;
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            State = online ? ServiceState.NotChecked : ServiceState.Offline;
        }

        public bool Due => online && State != ServiceState.Checking && (!everChecked || clock() - checkedAt >= Every);

        public async Task Run(Func<CancellationToken, Task> call, CancellationToken cancellation)
        {
            if (call == null || !Due) return;
            var before = State;
            State = ServiceState.Checking;
            started = clock();
            bool answered;
            try
            {
                await call(cancellation);
                answered = true;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                State = before;
                return;
            }
            catch (Exception)
            {
                answered = false;
            }
            checkedAt = clock();
            everChecked = true;
            Milliseconds = answered ? (int)Math.Round(Math.Max(0d, checkedAt - started) * 1000d) : NotRecorded;
            State = answered ? ServiceState.Reachable : ServiceState.NoReply;
        }
    }

    public static class ServiceText
    {
        public const string Offline = "Offline", NotChecked = "Not checked", Checking = "Checking", NoReply = "No reply";
        public const string ByClock = " (by clock)", ServiceWord = "Service ", OfflineLine = "Offline. Online play is not in this build.";
        public const string InParty = "In party", Searching = "Searching";

        public static string Short(ServiceState state, int milliseconds) => state switch
        {
            ServiceState.Offline => Offline,
            ServiceState.Reachable when milliseconds >= 0 => Ms(milliseconds),
            ServiceState.Checking when milliseconds >= 0 => Ms(milliseconds),
            ServiceState.Checking => Checking,
            ServiceState.NoReply => NoReply,
            _ => NotChecked,
        };

        public static string Region(string regionId) => QuickPlayRegions.Name(regionId) + ByClock;

        public static string Pill(ServiceState state, int milliseconds, string regionId) =>
            state != ServiceState.NoReply && state != ServiceState.Offline && milliseconds >= 0
                ? QuickPlayRegions.Name(regionId) + MapCardText.Separator + Short(state, milliseconds)
                : Short(state, milliseconds);

        public static string Line(ServiceState state, int milliseconds, string regionId, bool inParty, bool searching)
        {
            if (state == ServiceState.Offline) return OfflineLine;
            string service = Short(state, milliseconds);
            string line = Region(regionId) + MapCardText.Separator + (milliseconds >= 0 && state != ServiceState.NoReply ? ServiceWord + service : service);
            if (searching) return line + MapCardText.Separator + Searching;
            return inParty ? line + MapCardText.Separator + InParty : line;
        }

        public static ServiceDot Dot(ServiceState state) => state switch
        {
            ServiceState.Offline => ServiceDot.None,
            ServiceState.Reachable => ServiceDot.Good,
            ServiceState.NoReply => ServiceDot.Bad,
            _ => ServiceDot.Unknown,
        };

        static string Ms(int milliseconds) => milliseconds.ToString(CultureInfo.InvariantCulture) + " ms";
    }
}
