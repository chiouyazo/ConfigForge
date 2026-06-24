using ConfigForge.Abstractions;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class RuleEvaluatorTests
{
    private static JsonFormsRule TargetChannelRule()
    {
        ConfigSchema schema = new JsonFormsSchemaParser().Parse(CanonicalSchema.Json);
        return schema.Fields["target_channel"].Rules[0];
    }

    [Fact]
    public void Evaluate_ReturnsDisable_WhenEndpointUrlIsEmpty()
    {
        var evaluator = new JsonFormsRuleEvaluator();
        var document = new ConfigDocument { ["endpoint_url"] = "" };

        RuleEffect effect = evaluator.Evaluate(TargetChannelRule(), document);

        Assert.Equal(RuleEffect.Disable, effect);
    }

    [Fact]
    public void Evaluate_ReturnsNone_WhenEndpointUrlIsNonEmpty()
    {
        var evaluator = new JsonFormsRuleEvaluator();
        var document = new ConfigDocument { ["endpoint_url"] = "https://x" };

        RuleEffect effect = evaluator.Evaluate(TargetChannelRule(), document);

        Assert.Equal(RuleEffect.None, effect);
    }
}
