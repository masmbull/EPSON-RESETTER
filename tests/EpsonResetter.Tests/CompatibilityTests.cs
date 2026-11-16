using EpsonResetter.Core.Compatibility;
using EpsonResetter.Core.Identification;
using EpsonResetter.Core.WasteInk;
using EpsonResetter.Core.Models;

namespace EpsonResetter.Tests;

public class CompatibilityTests
{
    [Fact]
    public void Default_Registry_Recognises_Known_Epson_Models()
    {
        var registry = ModelCompatibilityRegistry.CreateDefault();

        Assert.True(registry.IsKnown("ET-2850"));
        Assert.True(registry.IsKnown("EPSON L3250 Series"));
        Assert.True(registry.IsKnown("L3250"));
    }

    [Fact]
    public void Default_Registry_Claims_No_Reset_Support()
    {
        var registry = ModelCompatibilityRegistry.CreateDefault();
        var profile = registry.Find("ET-2850");

        Assert.NotNull(profile);
        Assert.Equal(SupportLevel.Unsupported, profile!.Reset);
        Assert.Equal(ResetAuthorization.None, profile.Authorization);
        Assert.False(profile.SupportsReset);
        Assert.False(profile.SupportsCounterRead);
        Assert.False(string.IsNullOrWhiteSpace(profile.Reason));
    }

    [Fact]
    public void Unknown_Model_Is_Not_In_Registry()
    {
        var registry = ModelCompatibilityRegistry.CreateDefault();

        Assert.Null(registry.Find("Imaginary-9000"));
        Assert.False(registry.IsKnown("Imaginary-9000"));
    }

    [Fact]
    public void Lookup_Is_Normalised_Across_Separator_Styles()
    {
        var registry = ModelCompatibilityRegistry.CreateDefault();

        Assert.Same(registry.Find("ET-2850"), registry.Find("ET 2850"));
        Assert.Same(registry.Find("ET-2850"), registry.Find("ET2850"));
    }

    [Fact]
    public void Model_Normalisation_Strips_Series_Suffix()
    {
        Assert.Equal("L3250", PrinterModelInfo.Normalize(PrinterModelInfo.ResolveModel("EPSON L3250 Series", null)));
        Assert.Equal("ET2850", PrinterModelInfo.Normalize("ET-2850"));
    }

    [Fact]
    public void Registry_Back_Reader_Returns_No_Value_For_Unsupported_Model()
    {
        var reader = new RegistryBackedWasteInkCounterReader();
        var profile = ModelCompatibilityRegistry.CreateDefault().Find("L3250");

        var reading = reader.Read(
            new PrinterDevice { Id = "x", Name = "EPSON L3250", ModelName = "L3250" },
            profile);

        Assert.False(reading.IsSupported);
        Assert.Null(reading.CounterValue);
        Assert.Contains("not supported", reading.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registry_Back_Reader_Returns_No_Value_For_Unknown_Model()
    {
        var reader = new RegistryBackedWasteInkCounterReader();

        var reading = reader.Read(
            new PrinterDevice { Id = "y", Name = "Unknown Printer", ModelName = "ZZZ-1" },
            null);

        Assert.False(reading.IsSupported);
        Assert.Null(reading.CounterValue);
    }
}