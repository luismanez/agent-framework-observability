// SPDX-License-Identifier: MIT
using System.Text.Json;
using Melic.AgentFramework.Observability.Tools.Internal;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class ToolPayloadSerializerTests
{
    private readonly ToolPayloadSerializer _serializer = new();

    [Fact]
    public void Serializes_Empty_Input_As_Object()
    {
        Assert.Equal("{}", _serializer.SerializeInput(null, 2048));
    }

    [Fact]
    public void Serializes_Null_Output_As_Null()
    {
        Assert.Equal("null", _serializer.SerializeOutput(null, 2048));
    }

    [Fact]
    public void Serializes_Structured_Output_As_Json()
    {
        string? json = _serializer.SerializeOutput(new { status = "shipped" }, 2048);

        Assert.Equal("shipped", JsonDocument.Parse(json!).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void Serializes_Exception_As_Structured_Error()
    {
        string? json = _serializer.SerializeException(new InvalidOperationException("Missing order id"), 2048);
        using var document = JsonDocument.Parse(json!);

        Assert.Equal("InvalidOperationException", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Missing order id", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Oversized_String_Remains_Valid_Json_Within_Limit()
    {
        string? json = _serializer.SerializeOutput(new { value = new string('x', 500) }, 40);

        Assert.NotNull(json);
        Assert.True(json!.Length <= 40);
        JsonDocument.Parse(json);
    }

    [Fact]
    public void Oversized_Nested_Payload_Remains_Valid_Json_Within_Limit()
    {
        string? json = _serializer.SerializeOutput(new { items = Enumerable.Range(0, 100).Select(i => new { value = new string('x', i + 20) }) }, 80);

        Assert.NotNull(json);
        Assert.True(json!.Length <= 80);
        JsonDocument.Parse(json);
    }
}