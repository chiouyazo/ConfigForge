using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConfigForge.Analyzers;

/// <summary>
/// Flags two concrete, statically-checkable ways a <c>[CapabilityContract]</c> interface can
/// violate <c>CapabilityLoader</c>'s "never execute the plugin assembly's code" guarantee: a
/// default interface method body on the contract itself, and a <c>[ModuleInitializer]</c> method
/// anywhere in the same project.
/// </summary>
/// <remarks>
/// This is not an exhaustive safety net: it covers exactly these two concrete risks and
/// documents the rest as an inherent trust boundary (see <c>docs/capability-loading.md</c>):
/// static field initializers or type initializers on other types the interface happens to
/// reference, and reflection-driven code elsewhere that runs merely from a <see cref="Type"/>
/// being inspected, are all still possible and are not detected here.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CapabilityContractSafetyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The id of the "default interface method body on a capability contract" diagnostic.</summary>
    public const string DefaultBodyId = "CFCAP001";

    /// <summary>The id of the "[ModuleInitializer] alongside a capability contract" diagnostic.</summary>
    public const string ModuleInitializerId = "CFCAP002";

    private const string CapabilityContractAttributeMetadataName =
        "ConfigForge.Abstractions.CapabilityContractAttribute";
    private const string ModuleInitializerAttributeMetadataName =
        "System.Runtime.CompilerServices.ModuleInitializerAttribute";

    private static readonly DiagnosticDescriptor s_defaultBodyRule = new(
        DefaultBodyId,
        title: "Capability contract interface has a default interface method with a body",
        messageFormat: "'{0}' is a [CapabilityContract] interface but declares a default "
            + "interface method body, which would execute when a CapabilityLoader proxy calls it",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CapabilityLoader never instantiates or invokes the real implementation "
            + "type, but a default interface method body lives on the interface itself and runs "
            + "no matter which implementation (or proxy) is called through.",
        customTags: WellKnownDiagnosticTags.CompilationEnd
    );

    private static readonly DiagnosticDescriptor s_moduleInitializerRule = new(
        ModuleInitializerId,
        title: "[ModuleInitializer] method alongside a [CapabilityContract] interface",
        messageFormat: "'{0}' is a [ModuleInitializer] method in a project that also declares a "
            + "[CapabilityContract] interface; it runs as soon as CapabilityLoader loads the "
            + "assembly to enumerate that interface, before any proxy call is made",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A module initializer runs on assembly load, which CapabilityLoader always "
            + "does (to enumerate [CapabilityContract] interfaces) even though it never "
            + "instantiates or invokes anything from the assembly itself.",
        customTags: WellKnownDiagnosticTags.CompilationEnd
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [s_defaultBodyRule, s_moduleInitializerRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            INamedTypeSymbol? capabilityContractAttribute =
                compilationContext.Compilation.GetTypeByMetadataName(
                    CapabilityContractAttributeMetadataName
                );
            if (capabilityContractAttribute is null)
            {
                return;
            }

            INamedTypeSymbol? moduleInitializerAttribute =
                compilationContext.Compilation.GetTypeByMetadataName(
                    ModuleInitializerAttributeMetadataName
                );

            var hasCapabilityContract = new StrongBox<bool>();
            var moduleInitializers = new ConcurrentBag<(string Name, Location Location)>();

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext =>
                    AnalyzeInterface(
                        nodeContext,
                        capabilityContractAttribute,
                        hasCapabilityContract
                    ),
                SyntaxKind.InterfaceDeclaration
            );

            if (moduleInitializerAttribute is not null)
            {
                compilationContext.RegisterSyntaxNodeAction(
                    nodeContext =>
                        CollectModuleInitializer(
                            nodeContext,
                            moduleInitializerAttribute,
                            moduleInitializers
                        ),
                    SyntaxKind.MethodDeclaration
                );
            }

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                if (!hasCapabilityContract.Value)
                {
                    return;
                }

                foreach ((string name, Location location) in moduleInitializers)
                {
                    endContext.ReportDiagnostic(
                        Diagnostic.Create(s_moduleInitializerRule, location, name)
                    );
                }
            });
        });
    }

    private static void AnalyzeInterface(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol capabilityContractAttribute,
        StrongBox<bool> hasCapabilityContract
    )
    {
        var declaration = (InterfaceDeclarationSyntax)context.Node;
        INamedTypeSymbol? symbol = context.SemanticModel.GetDeclaredSymbol(
            declaration,
            context.CancellationToken
        );

        if (symbol is null || !HasAttribute(symbol, capabilityContractAttribute))
        {
            return;
        }

        hasCapabilityContract.Value = true;

        foreach (
            MethodDeclarationSyntax method in declaration.Members.OfType<MethodDeclarationSyntax>()
        )
        {
            if (method.Body is not null || method.ExpressionBody is not null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        s_defaultBodyRule,
                        method.Identifier.GetLocation(),
                        $"{symbol.Name}.{method.Identifier.Text}"
                    )
                );
            }
        }
    }

    private static void CollectModuleInitializer(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol moduleInitializerAttribute,
        ConcurrentBag<(string Name, Location Location)> moduleInitializers
    )
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        IMethodSymbol? symbol = context.SemanticModel.GetDeclaredSymbol(
            declaration,
            context.CancellationToken
        );

        if (symbol is not null && HasAttribute(symbol, moduleInitializerAttribute))
        {
            moduleInitializers.Add((symbol.Name, declaration.Identifier.GetLocation()));
        }
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType) =>
        symbol
            .GetAttributes()
            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
}
