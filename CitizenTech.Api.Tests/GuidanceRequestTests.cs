using CitizenTech.Api.Contracts;

namespace CitizenTech.Api.Tests;

public sealed class GuidanceRequestTests
{
    [Fact]
    public void ValidRequest_AcceptsRealElementIdsAndNullUserMessage()
    {
        Assert.True(GuidanceServiceTests.ValidRequest().IsValid());
    }

    [Fact]
    public void DuplicateElementIds_AreRejected()
    {
        var request = GuidanceServiceTests.ValidRequest();
        request.Screen!.Elements!.Add(request.Screen.Elements[0]);

        Assert.False(request.IsValid());
    }

    [Fact]
    public void NullElement_IsRejected()
    {
        var request = GuidanceServiceTests.ValidRequest();
        request.Screen!.Elements!.Add(null);

        Assert.False(request.IsValid());
    }

    [Fact]
    public void EmptyElements_AreRejected()
    {
        Assert.False(GuidanceServiceTests.ValidRequest([]).IsValid());
    }

    [Fact]
    public void TooManyElements_AreRejected()
    {
        var elements = Enumerable.Range(1, 81).Select(i => (ScreenElement?)new ScreenElement
        {
            Id = $"element-{i}", Role = "button", Text = "Agendamento", Enabled = true
        }).ToList();

        Assert.False(GuidanceServiceTests.ValidRequest(elements).IsValid());
    }

    [Fact]
    public void TooMuchHistory_IsRejected()
    {
        var request = GuidanceServiceTests.ValidRequest();
        for (var i = 0; i < 4; i++)
            request.History!.Add(new HistoryItem { Action = "explain", Instruction = "Orientação anterior." });

        Assert.False(request.IsValid());
    }

    [Fact]
    public void NullHistoryEntry_IsRejected()
    {
        var request = GuidanceServiceTests.ValidRequest();
        request.History!.Add(null);

        Assert.False(request.IsValid());
    }
}
