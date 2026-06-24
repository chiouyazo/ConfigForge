using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ConfigForge.Build;

/// <summary>
/// Incremental source generator that discovers every concrete plugin type
/// implementing <c>ConfigForge.Abstractions.IPlugin</c> across the current
/// compilation and its referenced assemblies, then emits a
/// <c>ConfigForge.Generated.GeneratedPluginRegistrar</c> class that instantiates
/// and registers each one.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PluginRegistrarGenerator : IIncrementalGenerator
{
    private const string PluginInterfaceMetadataName = "ConfigForge.Abstractions.IPlugin";
    private const string HintName = "GeneratedPluginRegistrar.g.cs";

    /// <summary>
    /// Wires the generator into the incremental pipeline. The registrar is produced
    /// once per compilation from the set of discovered plugin types.
    /// </summary>
    /// <param name="context">The initialization context supplied by Roslyn.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableArray<string>> pluginTypeNames =
            context.CompilationProvider.Select(
                static (compilation, cancellationToken) =>
                    CollectPluginTypeNames(compilation, cancellationToken)
            );

        context.RegisterSourceOutput(
            pluginTypeNames,
            static (productionContext, names) =>
                productionContext.AddSource(HintName, BuildSource(names))
        );
    }

    private static ImmutableArray<string> CollectPluginTypeNames(
        Compilation compilation,
        System.Threading.CancellationToken cancellationToken
    )
    {
        INamedTypeSymbol? pluginInterface = compilation.GetTypeByMetadataName(
            PluginInterfaceMetadataName
        );
        if (pluginInterface is null)
        {
            // The contract assembly is not part of this compilation; emit an empty registrar.
            return ImmutableArray<string>.Empty;
        }

        ImmutableArray<string>.Builder discovered = ImmutableArray.CreateBuilder<string>();

        // Walk the compilation's own types plus every referenced assembly's types.
        CollectFromNamespace(
            compilation.Assembly.GlobalNamespace,
            pluginInterface,
            discovered,
            cancellationToken
        );

        foreach (MetadataReference reference in compilation.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
            {
                CollectFromNamespace(
                    assembly.GlobalNamespace,
                    pluginInterface,
                    discovered,
                    cancellationToken
                );
            }
        }

        discovered.Sort(System.StringComparer.Ordinal);
        return discovered.ToImmutable();
    }

    private static void CollectFromNamespace(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol pluginInterface,
        ImmutableArray<string>.Builder discovered,
        System.Threading.CancellationToken cancellationToken
    )
    {
        foreach (INamespaceOrTypeSymbol member in namespaceSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (member)
            {
                case INamespaceSymbol childNamespace:
                    CollectFromNamespace(
                        childNamespace,
                        pluginInterface,
                        discovered,
                        cancellationToken
                    );
                    break;
                case INamedTypeSymbol type:
                    CollectFromType(type, pluginInterface, discovered, cancellationToken);
                    break;
                default:
                    break;
            }
        }
    }

    private static void CollectFromType(
        INamedTypeSymbol type,
        INamedTypeSymbol pluginInterface,
        ImmutableArray<string>.Builder discovered,
        System.Threading.CancellationToken cancellationToken
    )
    {
        if (IsEligiblePlugin(type, pluginInterface))
        {
            discovered.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        // Nested types can also be plugins.
        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectFromType(nested, pluginInterface, discovered, cancellationToken);
        }
    }

    private static bool IsEligiblePlugin(INamedTypeSymbol type, INamedTypeSymbol pluginInterface)
    {
        if (
            type.TypeKind != TypeKind.Class
            || type.IsAbstract
            || type.IsStatic
            || type.IsGenericType
        )
        {
            return false;
        }

        if (
            type.DeclaredAccessibility != Accessibility.Public
            && type.DeclaredAccessibility != Accessibility.Internal
        )
        {
            return false;
        }

        if (!ImplementsInterface(type, pluginInterface))
        {
            return false;
        }

        return HasAccessibleParameterlessConstructor(type);
    }

    private static bool ImplementsInterface(
        INamedTypeSymbol type,
        INamedTypeSymbol pluginInterface
    ) =>
        type.AllInterfaces.Any(implemented =>
            SymbolEqualityComparer.Default.Equals(implemented, pluginInterface)
        );

    private static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol type) =>
        type.InstanceConstructors.Any(static c =>
            c.Parameters.Length == 0
            && (
                c.DeclaredAccessibility == Accessibility.Public
                || c.DeclaredAccessibility == Accessibility.Internal
            )
        );

    private static SourceText BuildSource(ImmutableArray<string> pluginTypeNames)
    {
        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace ConfigForge.Generated;");
        builder.AppendLine();
        builder.AppendLine("internal static class GeneratedPluginRegistrar");
        builder.AppendLine("{");
        builder.AppendLine(
            "    internal static void RegisterAll(global::ConfigForge.Abstractions.IPluginRegistry registry)"
        );
        builder.AppendLine("    {");

        foreach (string typeName in pluginTypeNames)
        {
            builder.Append("        new ").Append(typeName).AppendLine("().Register(registry);");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");

        return SourceText.From(builder.ToString(), Encoding.UTF8);
    }
}
