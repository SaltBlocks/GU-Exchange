using System;
using System.Collections.Generic;
using System.Linq;
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

        public RateLimitHandler(HttpMessageHandler innerHandler, int requestsPerSecond) : base(innerHandler)
        {  
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
                    _requestTimes.Enqueue(delayUntil);
                }

                // Wait until we can make the request without exceeding the rate limit.
                TimeSpan delay = delayUntil - DateTime.Now;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
