﻿using System.Text;
using Core.IIIF;
using FluentAssertions;
using IIIF.Presentation.V3;
using Models.API.Collection;

namespace Core.Tests.IIIF;

public class IIIFResponseXTests
{
    [Fact]
    public async Task ToPresentation_ReturnsDeserialized_StandardIIIFModel()
    {
        const string input = "{\"id\": \"test-sample\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var actual = await stream.ToPresentation<Collection>();

        actual.Id.Should().Be("test-sample");
    }
    
    [Fact]
    public async Task ToPresentation_ReturnsDeserialized_NonStandardIIIFModel()
    {
        const string input = "{\"id\": \"test-sample\", \"slug\": \"foo\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var actual = await stream.ToPresentation<PresentationCollection>();
        
        actual.Id.Should().Be("test-sample");
        actual.Slug.Should().Be("foo");
    }
}