using System.Text.Json.Nodes;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <summary>
/// <see cref="FieldDefinition.WithKey"/> rebases not just the key but the rule condition scopes,
/// so a rule that watches a sibling keeps working once the field is rebased into a map/array/oneof
/// entry (e.g. <c>connectors/{guid}/…</c>) instead of resolving against the document root.
/// </summary>
public sealed class FieldDefinitionRebaseTests
{
    private static FieldDefinition FieldWatchingSibling() =>
        new()
        {
            Key = "token",
            ControlType = "text",
            Rules =
            [
                new JsonFormsRule
                {
                    Effect = RuleEffect.Enable,
                    Condition = new RuleCondition
                    {
                        Scope = "#/properties/url",
                        Schema = JsonNode.Parse("""{ "not": { "type": "null" } }"""),
                    },
                },
            ],
        };

    [Fact]
    public void WithKey_RebasesRuleScope_IntoTheContainer()
    {
        FieldDefinition rebased = FieldWatchingSibling().WithKey("connectors/abc/token");

        Assert.Equal(
            "#/properties/connectors/properties/abc/properties/url",
            rebased.Rules[0].Condition.Scope
        );
    }

    [Fact]
    public void WithKey_PlainRename_LeavesRuleScopeUntouched()
    {
        // Not a container rebase (the new key does not end with "/token"): scopes stay put.
        FieldDefinition renamed = FieldWatchingSibling().WithKey("accessToken");

        Assert.Equal("#/properties/url", renamed.Rules[0].Condition.Scope);
    }
}
