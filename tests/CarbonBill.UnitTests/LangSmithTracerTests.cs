using System.Net;
using CarbonBill.Modules.Extraction.Services.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public sealed class LangSmithTracerTests
{
    private sealed class MockHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task LangSmithTracer_WhenDisabledOrNoKey_ReturnsNull()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["LANGSMITH_TRACING"] = "false",
            ["LANGSMITH_API_KEY"] = ""
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"message\":\"Run created\"}");
        var client = new HttpClient(handler);
        var tracer = new LangSmithTracer(client, config, NullLogger<LangSmithTracer>.Instance);

        // Assert
        Assert.False(tracer.IsEnabled);

        // Act
        var result = await tracer.TraceRunAsync(
            "TestRun",
            "llm",
            new { test = "input" },
            new { test = "output" },
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Assert
        Assert.Null(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task LangSmithTracer_WhenEnabled_SendsTraceAndReturnsRunId()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["LANGSMITH_TRACING"] = "true",
            ["LANGSMITH_API_KEY"] = "lsv2_pt_test_key_12345",
            ["LANGSMITH_ENDPOINT"] = "https://api.smith.langchain.com",
            ["LANGSMITH_PROJECT"] = "CarbonBill"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"message\":\"Run created\"}");
        var client = new HttpClient(handler);
        var tracer = new LangSmithTracer(client, config, NullLogger<LangSmithTracer>.Instance);

        // Assert
        Assert.True(tracer.IsEnabled);

        // Act
        var runId = await tracer.TraceRunAsync(
            "GroqLlmExtractor",
            "llm",
            new { prompt = "Extract bill" },
            new { vendor = "DESCO", quantity = 1000 },
            DateTime.UtcNow,
            DateTime.UtcNow,
            new Dictionary<string, object> { ["model"] = "openai/gpt-oss-120b" });

        // Assert
        Assert.NotNull(runId);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("x-api-key"));
        Assert.Equal("https://api.smith.langchain.com/runs", handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task LangSmithTracer_WhenApiErrors_FailsGracefullyWithoutThrowing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["LANGSMITH_TRACING"] = "true",
            ["LANGSMITH_API_KEY"] = "lsv2_pt_test_key_12345",
            ["LANGSMITH_ENDPOINT"] = "https://api.smith.langchain.com"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "Server Error");
        var client = new HttpClient(handler);
        var tracer = new LangSmithTracer(client, config, NullLogger<LangSmithTracer>.Instance);

        // Act
        var runId = await tracer.TraceRunAsync(
            "GroqLlmExtractor",
            "llm",
            new { prompt = "Error test" },
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Assert - Should not throw, returns null
        Assert.Null(runId);
    }
}
