using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

static class Runner
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("Supply the ephemeral JOSE fixture path.");
            var data = OnlineJson.Parse(File.ReadAllText(args[0]));
            var settings = OnlineSettings.Read(data["settings"]); var content = OnlineContent.Read(data["content"]);
            var expected = new OnlineMatchExpectation(data["matchId"].String(), "1", data["rosterHash"].String(), settings, content);
            var verifier = new OnlineAdmissionVerifier("wordbound-interop", data["jwks"]);
            long now = (long)data["now"].Number();
            var match = verifier.VerifyStart(data["certificate"].String(), expected, now);
            var admitted = verifier.VerifyAdmission(data["grant"].String(), match, data["playerId"].String(), data["secret"].String(), now);
            if (admitted.PlayerId != data["playerId"].String()) throw new Exception("JOSE identity mismatch.");
            Console.WriteLine("JOSE_INTEROP passed: JavaScript RS256, roster hash and secret commitment verified by C#.");
            await CheckHttp(data["playerId"].String(), data["secret"].String());
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("ONLINE_INTEROP failed: " + error.GetType().Name + ": " + error.Message);
            return 1;
        }
    }

    static async Task CheckHttp(string player, string secret)
    {
        using var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start(); int number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
        var origin = new Uri("http://127.0.0.1:" + number + "/");
        try { using var rejected = new WordboundHttpTransport(origin); throw new Exception("Insecure origin accepted."); }
        catch (ArgumentException) { }
        using var listener = new HttpListener();
        listener.Prefixes.Add(origin.AbsoluteUri); listener.Start();
        using var http = new WordboundHttpTransport(origin, true);
        var client = new OnlineCoordinatorClient(http);
        var server = Task.Run(async () => {
            for (int i = 0; i < 4; i++)
            {
                var context = await listener.GetContextAsync();
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                string input = await reader.ReadToEndAsync();
                string output;
                if (i == 0)
                {
                    if (context.Request.RawUrl != "/v1/guests" || OnlineJson.Parse(input)["displayName"].String() != "HTTP test")
                        throw new Exception("Wrong guest HTTP request.");
                    output = "{\"playerId\":\"" + player + "\",\"accessToken\":\"test.access.token\",\"refreshToken\":\"" + player +
                        "." + secret + "\",\"expiresIn\":900}";
                }
                else
                {
                    if (context.Request.Headers["Authorization"] != "Bearer test.access.token") throw new Exception("Missing bearer header.");
                    if (i == 1)
                    {
                        if (context.Request.Headers["Idempotency-Key"] != "stable-key" || !OnlineJson.Parse(input).Has("settings"))
                            throw new Exception("Lost operation key or body.");
                        output = "{}";
                    }
                    else if (i == 2)
                    {
                        context.Response.StatusCode = 302; context.Response.RedirectLocation = "https://invalid.example/";
                        output = "";
                    }
                    else output = new string(' ', OnlineJson.MaximumBytes + 1);
                }
                byte[] bytes = Encoding.UTF8.GetBytes(output); context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length); context.Response.Close();
            }
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.CreateGuest("HTTP test", deadline.Token);
        await client.CreateParty(new OnlineSettings("TimeRun", "Solo", false, "asia", "1", "1"), "stable-key", deadline.Token);
        bool redirectRejected = false;
        try { await client.Profile(deadline.Token); } catch (IOException) { redirectRejected = true; }
        bool oversizedRejected = false;
        try { await client.Profile(deadline.Token); } catch (IOException) { oversizedRejected = true; }
        await server.WaitAsync(deadline.Token);
        if (!redirectRejected || !oversizedRejected) throw new Exception("HTTP boundary did not fail closed.");
        Console.WriteLine("HTTP_TRANSPORT passed: real loopback request headers/body, HTTPS default, redirect and size rejection.");
    }
}
