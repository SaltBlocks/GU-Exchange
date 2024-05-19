using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

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
                DateTime delayUntil = DateTime.Now;
                lock (_requestTimes)
                {
                    // Maintain only the max number of requests for the past 5 seconds.
                    while (_requestTimes.Count > _requestsPerSecond * 5)
                    {
                        _requestTimes.Dequeue();
                    }

                    // Check if we've logged enough requests to start rate limiting.
                    if (_requestTimes.Count == _requestsPerSecond * 5)
                    {
                        // Check if we have buffer slots available for this request, keep the max number of requests within a 10% margin.
                        if (_requestTimes.Peek() > DateTime.Now.AddSeconds(-5.5))
                        {
                            // We've exceeded the buffer in the past 5 seconds, only allow x requests per second, keep the max number of requests within a 10% margin.
                            delayUntil = _requestTimes.ElementAt(_requestTimes.Count - _requestsPerSecond).AddSeconds(1.1);
                        }
                    }
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
            lock (_requestTimes)
            {
                for (int i = 0; i < requestAmount; i++)
                {
                    // Maintain only the max number of requests for the past 5 seconds.
                    while (_requestTimes.Count > _requestsPerSecond * 5)
                    {
                        _requestTimes.Dequeue();
                    }

                    // Check if we've logged enough requests to start rate limiting.
                    if (_requestTimes.Count == _requestsPerSecond * 5)
                    {
                        // Check if we have buffer slots available for this request, keep the max number of requests within a 10% margin.
                        if (_requestTimes.Peek() > DateTime.Now.AddSeconds(-5.5))
                        {
                            // We've exceeded the buffer in the past 5 seconds, only allow x requests per second, keep the max number of requests within a 10% margin.
                            delayUntil = _requestTimes.ElementAt(_requestTimes.Count - _requestsPerSecond).AddSeconds(1.1);
                        }
                    }
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

        public void CancelRequests()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            DateTime currentTime = DateTime.Now;
            Queue<DateTime> tempQueue = new Queue<DateTime>();
            lock (_requestTimes)
            {
                while (_requestTimes.Count > 0)
                {
                    DateTime date = _requestTimes.Dequeue();
                    tempQueue.Enqueue(date <= currentTime ? date : currentTime);
                }

                while (tempQueue.Count > 0)
                {
                    _requestTimes.Enqueue(tempQueue.Dequeue());
                }
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
