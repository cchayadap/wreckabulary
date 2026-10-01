using System;
using System.Text;

namespace Wreckabulary.Rules
{
    /// <summary>
    /// A private-room code for LAN play: the host's IPv4 address and port, packed so it can be
    /// read out across the room. Home networks get the shortest codes:
    /// <list type="bullet">
    /// <item>192.168.x.x: 8 characters (192.168.1.23:7777 is "04BH-WR83")</item>
    /// <item>10.x.x.x and 172.16-31.x.x: 10 characters (172.20.5.9:7777 is "840M4-HWRBK")</item>
    /// <item>any other IPv4 address: 12 characters (81.2.69.160:7777 is "A414B8-0YC7BB")</item>
    /// </list>
    /// It uses Crockford base32 (no I, L, O or U). When typing, case, dashes and spaces don't
    /// matter, and I/L read as 1 and O as 0. Each code ends in a CRC, so every single mistyped
    /// character and every swapped neighbouring pair is caught rather than sending the player
    /// to a stranger's address. Internet play will use relay join codes instead.
    /// </summary>
    public static class RoomCode
    {
        const string Digits = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        // CRC generators without their top term: x^8+x^2+x+1 and x^12+x^11+x^3+x^2+x+1.
        // Both catch every error burst up to their width, and a swapped pair of 5-bit
        // characters is at most a 10-bit burst that neither generator divides.
        const ulong Crc8 = 0x07;
        const ulong Crc12 = 0x80F;

        public static string Encode(byte a, byte b, byte c, byte d, ushort port)
        {
            if (a == 192 && b == 168)
            {
                ulong data = ((ulong)c << 24) | ((ulong)d << 16) | port;
                return Format((data << 8) | Crc(data, 32, 8, Crc8), 8);
            }
            if (a == 10 || (a == 172 && b >= 16 && b <= 31))
            {
                ulong tag = a == 10 ? 0UL : 1UL;
                ulong host = a == 10 ? ((ulong)b << 16) | ((ulong)c << 8) | d : ((ulong)(b - 16) << 16) | ((ulong)c << 8) | d;
                ulong data = (tag << 40) | (host << 16) | port;
                return Format((data << 8) | Crc(data, 42, 8, Crc8), 10);
            }
            ulong full = ((ulong)a << 40) | ((ulong)b << 32) | ((ulong)c << 24) | ((ulong)d << 16) | port;
            return Format((full << 12) | Crc(full, 48, 12, Crc12), 12);
        }

        /// <summary>Encodes "192.168.1.23" and a port. Returns null for anything that isn't a dotted IPv4 address.</summary>
        public static string Encode(string ipv4, ushort port)
        {
            return TryParseIp(ipv4, out byte[] ip) ? Encode(ip[0], ip[1], ip[2], ip[3], port) : null;
        }

        public static bool TryDecode(string code, out string ipv4, out ushort port)
        {
            ipv4 = null;
            port = 0;
            if (code == null) return false;
            ulong packed = 0;
            int count = 0;
            foreach (char raw in code)
            {
                if (raw == '-' || raw == ' ') continue;
                char ch = char.ToUpperInvariant(raw);
                if (ch == 'I' || ch == 'L') ch = '1';
                if (ch == 'O') ch = '0';
                int digit = Digits.IndexOf(ch);
                if (digit < 0) return false;
                if (++count > 12) return false;
                packed = (packed << 5) | (uint)digit;
            }

            ulong a, b, c, d;
            switch (count)
            {
                case 8:
                {
                    ulong data = packed >> 8;
                    if ((packed & 0xFF) != Crc(data, 32, 8, Crc8)) return false;
                    a = 192; b = 168; c = (data >> 24) & 255; d = (data >> 16) & 255;
                    port = (ushort)(data & 0xFFFF);
                    break;
                }
                case 10:
                {
                    ulong data = packed >> 8;
                    if ((packed & 0xFF) != Crc(data, 42, 8, Crc8)) return false;
                    ulong tag = data >> 40;
                    ulong host = (data >> 16) & 0xFFFFFF;
                    if (tag == 0)
                    {
                        a = 10; b = host >> 16;
                    }
                    else if (tag == 1 && host < (1UL << 20))
                    {
                        a = 172; b = 16 + (host >> 16);
                    }
                    else return false;
                    c = (host >> 8) & 255; d = host & 255;
                    port = (ushort)(data & 0xFFFF);
                    break;
                }
                case 12:
                {
                    ulong data = packed >> 12;
                    if ((packed & 0xFFF) != Crc(data, 48, 12, Crc12)) return false;
                    a = (data >> 40) & 255; b = (data >> 32) & 255; c = (data >> 24) & 255; d = (data >> 16) & 255;
                    port = (ushort)(data & 0xFFFF);
                    break;
                }
                default:
                    return false;
            }
            ipv4 = $"{a}.{b}.{c}.{d}";
            return true;
        }

        static string Format(ulong packed, int chars)
        {
            var sb = new StringBuilder(chars + 1);
            for (int i = chars - 1; i >= 0; i--)
            {
                sb.Append(Digits[(int)((packed >> (i * 5)) & 31)]);
                if (i == chars / 2) sb.Append('-');
            }
            return sb.ToString();
        }

        /// <summary>The remainder of data·x^width divided by the generator, most significant bit first.</summary>
        static ulong Crc(ulong data, int bits, int width, ulong generator)
        {
            ulong top = 1UL << (width - 1);
            ulong mask = (1UL << width) - 1;
            ulong crc = 0;
            for (int i = bits - 1; i >= 0; i--)
            {
                bool feedback = (((data >> i) & 1) != 0) ^ ((crc & top) != 0);
                crc = (crc << 1) & mask;
                if (feedback) crc ^= generator;
            }
            return crc;
        }

        static bool TryParseIp(string s, out byte[] ip)
        {
            ip = new byte[4];
            if (string.IsNullOrEmpty(s)) return false;
            var parts = s.Split('.');
            if (parts.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 3) return false;
                foreach (char ch in parts[i]) if (ch < '0' || ch > '9') return false;
                int n = int.Parse(parts[i]);
                if (n > 255) return false;
                ip[i] = (byte)n;
            }
            return true;
        }
    }
}
