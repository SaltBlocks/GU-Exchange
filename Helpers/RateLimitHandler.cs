using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GU_Exchange.Helpers
{
    internal class RateLimitHandler : DelegatingHandler
    {
        #region Class properties.
        private readonly int _requestsPerSecond;
        private readonly Queue<DateTime> _requestTimes = new Queue<DateTime>();
        private readonly object _lockObject = new object();
        private CancellationTokenSource _cts;
        #endregion

        #region Default constructor.
        /// <summary>
        /// Construct a <see cref="DelegatingHandler"/> that doesn't create more api requests per second than the provided maximum requests per second.
        /// Keeps a buffer of 5 seconds to allow short bursts of a larger number of requests to pass and keeps the actual number of requests within a 10% margin of the largest number allowed.
        /// </summary>
        /// <param name="innerHandler"></param>
        /// <param name="requestsPerSecond"></param>
        public RateLimitHandler(HttpMessageHandler innerHandler, int requestsPerSecond) : base(innerHandler)
        {
            _cts = new CancellationTokenSource();
            _requestsPerSecond = requestsPerSecond;
        }
        #endregion

        #region Handle request.
        /// <summary>
        /// Called whenever a request is made using the handler. If the request is an api call, will ensure that it is held until it no longer exceeds the set rate limit.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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
        #endregion

        #region Supporting functions.
        /// <summary>
        /// Reserve the specified number of requests for an external api call.
        /// </summary>
        /// <param name="requestAmount"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Cancel all requests that are awaiting a slot to make an api call.
        /// </summary>
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

        /// <summary>
        /// Cancel all requests and free all available slots for future api calls.
        /// </summary>
        public void CancelRequestsAndReset()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _requestTimes.Clear();
        }
        #endregion
    }
}
