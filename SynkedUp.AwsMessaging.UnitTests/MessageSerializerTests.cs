using System;
using Emmersion.Testing;
using NUnit.Framework;

namespace SynkedUp.AwsMessaging.UnitTests;

internal class MessageSerializerTests : With_an_automocked<MessageSerializer>
{
    [Test]
    public void When_serializing_and_deserializing()
    {
        var original = new SerializationTest<SerializationTest<int>>
        {
            IntData = 1,
            StringData = "hello",
            TemporalData = DateTimeOffset.UtcNow,
            GenericData = new SerializationTest<int>
            {
                IntData = 2,
                StringData = "world",
                TemporalData = DateTimeOffset.UtcNow.AddMinutes(5),
                GenericData = -1
            }
        };

        var serialized = ClassUnderTest.Serialize(original);
        var deserialized = ClassUnderTest.Deserialize<SerializationTest<SerializationTest<int>>>(serialized);

        Assert.That(deserialized!.IntData, Is.EqualTo(original.IntData));
        Assert.That(deserialized.StringData, Is.EqualTo(original.StringData));
        Assert.That(deserialized.TemporalData, Is.EqualTo(original.TemporalData));
        Assert.That(deserialized.GenericData!.IntData, Is.EqualTo(original.GenericData.IntData));
        Assert.That(deserialized.GenericData.StringData, Is.EqualTo(original.GenericData.StringData));
        Assert.That(deserialized.GenericData.TemporalData, Is.EqualTo(original.GenericData.TemporalData));
        Assert.That(deserialized.GenericData.GenericData, Is.EqualTo(original.GenericData.GenericData));
    }

    [Test]
    public void When_serializing_camel_case_is_used()
    {
        var data = new SerializationTest<int[]>
        {
            IntData = 1,
            StringData = "hello",
            TemporalData = DateTimeOffset.Parse("2020-11-03T01:23:45Z"),
            GenericData = new[] { 1, 2, 3 }
        };

        var serialized = ClassUnderTest.Serialize(data);

        Assert.That(serialized,
            Is.EqualTo(
                "{\"intData\":1,\"stringData\":\"hello\",\"temporalData\":\"2020-11-03T01:23:45+00:00\",\"genericData\":[1,2,3]}"));
    }

    [Test]
    public void When_deserializing_casing_is_ignored()
    {
        var json =
            "{\"intdata\":1,\"StringData\":\"hello\",\"TEMPORALDATA\":\"2020-11-03T01:23:45+00:00\",\"genericData\":[1,2,3]}";

        var deserialized = ClassUnderTest.Deserialize<SerializationTest<int[]>>(json);

        Assert.That(deserialized!.IntData, Is.EqualTo(1));
        Assert.That(deserialized.StringData, Is.EqualTo("hello"));
        Assert.That(deserialized.TemporalData, Is.EqualTo(DateTimeOffset.Parse("2020-11-03T01:23:45Z")));
        Assert.That(deserialized.GenericData, Is.EqualTo(new[] { 1, 2, 3 }));
    }
}

internal class SerializationTest<T>
{
    public int IntData { get; init; }
    public string StringData { get; init; } = "";
    public DateTimeOffset TemporalData { get; init; }
    public T? GenericData { get; init; }
}