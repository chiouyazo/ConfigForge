using ConfigForge.Abstractions;

namespace ConfigForge.Core.Schema;

/// <summary>
/// Resolves whether a category (tab) is visible and enabled from its
/// <see cref="CategoryElement.Rules"/>, using the shared two-directional
/// <see cref="RuleEvaluation"/> fold: an <c>ENABLE</c> rule keeps the tab enabled only while its
/// condition holds (and locks it otherwise), a <c>SHOW</c> rule shows it only while the condition
/// holds. Multiple rules combine with AND.
/// </summary>
public static class CategoryVisibility
{
    /// <summary>Resolves the (visible, enabled) state of a category against the live document.</summary>
    /// <param name="category">The category whose rules to evaluate.</param>
    /// <param name="evaluator">The rule evaluator.</param>
    /// <param name="document">The live document the conditions read from.</param>
    /// <returns>Whether the category should be shown, and whether it should be selectable.</returns>
    public static (bool Visible, bool Enabled) Resolve(
        CategoryElement category,
        IJsonFormsRuleEvaluator evaluator,
        ConfigDocument document
    )
    {
        ArgumentNullException.ThrowIfNull(category);
        return RuleEvaluation.Resolve(category.Rules, evaluator, document);
    }
}
