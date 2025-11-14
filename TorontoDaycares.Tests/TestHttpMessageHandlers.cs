namespace TorontoDaycares.Tests
{
    internal class NullHttpClientHandler : HttpClientHandler
    {
        public List<HttpRequestMessage> Requests { get; set; } = new List<HttpRequestMessage>();
        private readonly HttpResponseMessage _response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        public void SetResponseContent(string content)
        {
            _response.Content = new StringContent(content);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_response);
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _response;
        }
    }

    //internal class NullHttpClientHandler : HttpClientHandler
    //{
    //    public List<HttpRequestMessage> Requests { get; set; } = [];

    //    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    //    {
    //        return Task.FromResult(Send(request, cancellationToken));
    //    }

    //    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    //    {
    //        Requests.Add(request);

    //        return new HttpResponseMessage()
    //        {
    //            StatusCode = System.Net.HttpStatusCode.OK
    //        };
    //    }
    //}

    internal class ThrowIfCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Fail("HttpClient should not be called when cache file exists");
            throw new InvalidOperationException();
        }
    }

    internal class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
