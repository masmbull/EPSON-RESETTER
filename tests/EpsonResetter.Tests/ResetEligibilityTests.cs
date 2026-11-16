using EpsonResetter.Core.Compatibility;
using EpsonResetter.Core.WasteInk;

namespace EpsonResetter.Tests;

public class ResetEligibilityTests
{
    private static ResetEligibilityService DefaultService() =>
        new(ModelCompatibilityRegistry.CreateDefault());

    private static ModelCompatibilityRegistry SupportedRegistry() => new(new[]
    {
        new ModelProfile
        {
            ModelKey = "ET2850",
            DisplayName = "Epson ET-2850",
            CounterRead = SupportLevel.Supported,
            Reset = SupportLevel.Supported,
            Authorization = ResetAuthorization.VendorDocumented,
            VerifiedMethod = "Vendor documentation (unit-test fixture only)",
            VerifiedMethodVersion = "1.0"
        }
    });

    private static ResetEligibilityRequest Ready() => new()
    {
        ModelName = "ET-2850",
        PrinterOnline = true,
        PadInspectedAndServiced = true,
        OwnerAuthorised = true,
        MethodConfirmedByOperator = true
    };

    [Fact]
    public void Unknown_Model_Is_Not_Eligible()
    {
        var result = DefaultService().Evaluate(Ready() with { ModelName = "Nonexistent-1" });

        Assert.False(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.ModelUnknown, result.Code);
        Assert.Contains(result.Reasons, reason => reason.Contains("not in the compatibility registry"));
    }

    [Fact]
    public void Unsupported_Model_Is_Not_Eligible_Even_When_All_Checks_Pass()
    {
        var result = DefaultService().Evaluate(Ready());

        Assert.False(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.ResetUnsupported, result.Code);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void Offline_Printer_Is_Not_Eligible()
    {
        var service = new ResetEligibilityService(SupportedRegistry());

        var result = service.Evaluate(Ready() with { PrinterOnline = false });

        Assert.False(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.PrinterOffline, result.Code);
        Assert.Contains(result.Reasons, reason => reason.Contains("not reachable"));
    }

    [Fact]
    public void Pad_Inspection_Is_Required()
    {
        var service = new ResetEligibilityService(SupportedRegistry());

        var result = service.Evaluate(Ready() with { PadInspectedAndServiced = false });

        Assert.False(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.PadNotInspected, result.Code);
        Assert.Contains(result.Reasons, reason => reason.Contains("waste ink pad"));
    }

    [Fact]
    public void Owner_Authorisation_Is_Required()
    {
        var service = new ResetEligibilityService(SupportedRegistry());

        var result = service.Evaluate(Ready() with { OwnerAuthorised = false });

        Assert.False(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.OwnerNotAuthorised, result.Code);
    }

    [Fact]
    public void Operator_Must_Confirm_The_Documented_Method()
    {
        var service = new ResetEligibilityService(SupportedRegistry());

        var result = service.Evaluate(Ready() with { MethodConfirmedByOperator = false });

        Assert.False(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.MethodNotConfirmed, result.Code);
    }

    [Fact]
    public void Policy_Engine_Can_Report_Eligible_When_Every_Precondition_Is_Met()
    {
        var service = new ResetEligibilityService(SupportedRegistry());

        var result = service.Evaluate(Ready());

        Assert.True(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.Eligible, result.Code);
        Assert.Empty(result.Reasons);
        Assert.NotNull(result.Profile);
        Assert.Contains("preconditions are met", result.Summary);
    }

    [Fact]
    public void Unauthorised_Profile_Is_Rejected_Even_If_Reset_Is_Marked_Supported()
    {
        var registry = new ModelCompatibilityRegistry(new[]
        {
            new ModelProfile
            {
                ModelKey = "ET2850",
                DisplayName = "Epson ET-2850",
                Reset = SupportLevel.Supported,
                Authorization = ResetAuthorization.None
            }
        });

        var result = new ResetEligibilityService(registry).Evaluate(Ready());

        Assert.False(result.IsEligible);
        Assert.Equal(ResetEligibilityCodes.MethodNotAuthorised, result.Code);
    }
}