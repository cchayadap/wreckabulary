using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundHttpTransport : IOnlineHttpTransport, IDisposable
    {
        readonly Uri origin;
        readonly HttpClient client;
        readonly OnlineServerClock serverClock;
        public WordboundHttpTransport(Uri coordinatorOrigin, bool allowLoopbackHttp = false, OnlineServerClock coordinatorClock = null)
            : this(coordinatorOrigin, new HttpClientHandler { AllowAutoRedirect = false }, allowLoopbackHttp, coordinatorClock) { }
        public WordboundHttpTransport(Uri coordinatorOrigin, HttpMessageHandler handler, bool allowLoopbackHttp = false, OnlineServerClock coordinatorClock = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            serverClock = coordinatorClock;
            if (coordinatorOrigin == null || !coordinatorOrigin.IsAbsoluteUri || coordinatorOrigin.AbsolutePath != "/" ||
                coordinatorOrigin.Query != "" || coordinatorOrigin.Fragment != "" || coordinatorOrigin.UserInfo != "" ||
                !(coordinatorOrigin.Scheme == "https" || (allowLoopbackHttp && coordinatorOrigin.IsLoopback && coordinatorOrigin.Scheme == "http")))
                throw new ArgumentException("Use a pinned HTTPS coordinator origin.");
            origin = coordinatorOrigin;
            client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        }
        public async Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellationToken)
        {
            if (request == null || !request.Path.StartsWith("/v1/", StringComparison.Ordinal) || request.Path.Contains("?") ||
                request.Path.Contains("#") || request.Path.Contains("\\") || request.Path.Contains(".."))
                throw new ArgumentException("Invalid coordinator route.");
            var target = new Uri(origin, request.Path);
            if (target.GetLeftPart(UriPartial.Authority) != origin.GetLeftPart(UriPartial.Authority))
                throw new ArgumentException("Coordinator origin changed.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), target);
            if (request.AccessToken != null) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);
            if (request.IdempotencyKey != null) message.Headers.Add("Idempotency-Key", request.IdempotencyKey);
            if (request.JsonBody != null) message.Content = new StringContent(request.JsonBody, Encoding.UTF8, "application/json");
            long sent = serverClock?.Local() ?? 0;
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (serverClock != null && response.Headers.Date is DateTimeOffset date)
                serverClock.Observe(date.ToUnixTimeMilliseconds(), sent, serverClock.Local(), OnlineServerClock.MaximumResolutionMilliseconds);
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400) throw new IOException("Coordinator redirects are rejected.");
            if (response.Content.Headers.ContentLength > OnlineJson.MaximumBytes) throw new IOException("Coordinator response exceeds its limit.");
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[4096];
            while (true)
            {
                int count = await stream.ReadAsync(chunk, 0, chunk.Length, deadline.Token).ConfigureAwait(false);
                if (count == 0) break;
                if (buffer.Length + count > OnlineJson.MaximumBytes) throw new IOException("Coordinator response exceeds its limit.");
                buffer.Write(chunk, 0, count);
            }
            return new OnlineResponse((int)response.StatusCode, new UTF8Encoding(false, true).GetString(buffer.ToArray()));
        }
        public void Dispose() => client.Dispose();
    }
}
