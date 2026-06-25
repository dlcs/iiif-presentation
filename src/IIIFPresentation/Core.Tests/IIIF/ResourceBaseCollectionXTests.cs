using Core.IIIF;
using IIIF.Presentation.V3;

namespace Core.Tests.IIIF;

public class ResourceBaseCollectionXTests
{
    private class TestItem : ResourceBase
    {
        public TestItem(string id) { Id = id; }

        public override string Type => "TestItem";
    }

    [Fact]
    public void AddDistinctById_AddsAllItemsWhenTargetEmpty()
    {
        var target = new List<TestItem>();
        var source = new[] { new TestItem("id1"), new TestItem("id2") };

        var added = target.AddDistinctById(source);

        target.Should().HaveCount(2);
        target.Select(x => x.Id).Should().ContainInOrder("id1", "id2");
        added.Should().Be(2);
    }

    [Fact]
    public void AddDistinctById_ExcludesDuplicateIds()
    {
        var target = new List<TestItem> { new("id1") };
        var source = new[] { new TestItem("id1"), new TestItem("id2") };

        var added = target.AddDistinctById(source);

        target.Should().HaveCount(2);
        target.Select(x => x.Id).Should().ContainInOrder("id1", "id2");
        added.Should().Be(1);
    }

    [Fact]
    public void AddDistinctById_IgnoresNullSource()
    {
        var target = new List<TestItem> { new("id1") };

        var added = target.AddDistinctById(null);

        target.Should().HaveCount(1);
        target[0].Id.Should().Be("id1");
        added.Should().Be(0);
    }

    [Fact]
    public void AddDistinctById_ExcludesAllDuplicates()
    {
        var target = new List<TestItem> { new("id1"), new("id2") };
        var source = new[] { new TestItem("id1"), new TestItem("id2"), new TestItem("id3") };

        var added = target.AddDistinctById(source);

        target.Should().HaveCount(3);
        target.Select(x => x.Id).Should().ContainInOrder("id1", "id2", "id3");
        added.Should().Be(1);
    }
    
    [Fact]
    public void AddDistinctById_CanAlterBeforeAdd()
    {
        var target = new List<TestItem> { new("id1"), new("id2") };
        var source = new[] { new TestItem("id1"), new TestItem("id2"), new TestItem("id3") };

        var added = target.AddDistinctById(source, ti => ti.Profile = "Changed");

        target.Should().HaveCount(3);
        target.Select(x => x.Id).Should().ContainInOrder("id1", "id2", "id3");
        added.Should().Be(1);
        target.Where(t => t.Profile == "Changed").Should().HaveCount(1, "Only one item was altered");
    }
}
