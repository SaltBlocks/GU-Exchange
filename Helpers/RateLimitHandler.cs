using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GU_Exchange.Helpers
{
    internal class RateLimitHandler : DelegatingHandler
    {
        private readonly int _requestsPerSecond;
        private readonly Queue<DateTime> _requestTimes = new Queue<DateTime>();
        private readonly object _lockObject = new object();
        private CancellationTokenSource _cts;

        public RateLimitHandler(HttpMessageHandler innerHandler, int requestsPerSecond) : base(innerHandler)
        {
            _cts = new CancellationTokenSource();
            _requestsPerSecond = requestsPerSecond;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri? uri = request.RequestUri;
            if (uri != null && uri.Host.Contains("api"))
            {
                DateTime delayUntil;
                lock (_lockObject)
                {
                    // Maintain only the last 5 request times
                    while (_requestTimes.Count > _requestsPerSecond)
                    {
                        _requestTimes.Dequeue();
                    }

                    // Calculate the time to delay the current request
                    delayUntil = _requestTimes.Count == _requestsPerSecond ? _requestTimes.Peek().AddSeconds(1) : DateTime.Now;
                    DateTime requestTime = delayUntil > DateTime.Now ? delayUntil : DateTime.Now;
                    _requestTimes.Enqueue(requestTime);
                }

                // Wait until we can make the request without exceeding the rate limit.
                TimeSpan delay = delayUntil - DateTime.Now;
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token))
                        {
                            await Task.Delay(delay, linkedCts.Token);
                        }
                    } catch (OperationCanceledException)
                    {
                        return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    }
                }
            }
            return await base.SendAsync(request, cancellationToken);
        }

        public async Task ReserveRequests(int requestAmount, CancellationToken? cancellationToken = null)
        {
            DateTime delayUntil = DateTime.Now;
            lock (_lockObject)
            {
                for (int i = 0; i < requestAmount; i++)
                {
                    // Maintain only the last 5 request times
                    while (_requestTimes.Count > _requestsPerSecond)
                    {
                        _requestTimes.Dequeue();
                    }

                    // Calculate the time to delay the current request
                    delayUntil = _requestTimes.Count == _requestsPerSecond ? _requestTimes.Peek().AddSeconds(1) : DateTime.Now;
                    DateTime requestTime = delayUntil > DateTime.Now ? delayUntil : DateTime.Now;
                    _requestTimes.Enqueue(requestTime);
                }
            }

            // Wait until we can make the request without exceeding the rate limit.
            TimeSpan delay = delayUntil - DateTime.Now;
            if (delay > TimeSpan.Zero)
            {
                if (cancellationToken != null)
                {
                    using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource((CancellationToken)cancellationToken, _cts.Token))
                    {
                        await Task.Delay(delay, linkedCts.Token);
                    }
                }
                else
                    await Task.Delay(delay, _cts.Token);
            }
        }

        public void CancelRequestsAndReset()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _requestTimes.Clear();
        }
    }
}
