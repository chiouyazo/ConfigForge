using System.Threading.Tasks;
using ConfigForge.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ConfigForge.Analyzers.Tests;

public static class CapabilityContractSafetyAnalyzerTests
{
    // A minimal stand-in for ConfigForge.Abstractions.CapabilityContractAttribute, declared under
    // the exact same namespace+name the analyzer matches by metadata name. Keeps this test project
    // targeting the same .NET reference assemblies the analyzer itself is compiled and run
    // against, instead of pulling in the real (net10.0-built) Abstractions assembly. All `using`
    // directives live here, at the true top of the combined file. Everything appended after this
    // scaffold is top-level declarations only, no more usings.
    private const string Scaffold = """
        using System.Threading.Tasks;
        using System.Runtime.CompilerServices;
        using ConfigForge.Abstractions;

        namespace ConfigForge.Abstractions
        {
            [System.AttributeUsage(System.AttributeTargets.Interface)]
            public sealed class CapabilityContractAttribute : System.Attribute { }
        }

        """;

    private static async Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<CapabilityContractSafetyAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState = { Sources = { Scaffold + source } },
        };

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync();
    }

    private static DiagnosticResult DefaultBody(string member) =>
        new DiagnosticResult(
            CapabilityContractSafetyAnalyzer.DefaultBodyId,
            DiagnosticSeverity.Warning
        ).WithArguments(member);

    private static DiagnosticResult ModuleInitializer(string method) =>
        new DiagnosticResult(
            CapabilityContractSafetyAnalyzer.ModuleInitializerId,
            DiagnosticSeverity.Warning
        ).WithArguments(method);

    [Fact]
    public static Task AllowsAPurelyAbstractCapabilityContract() =>
        VerifyAsync(
            """
            [CapabilityContract]
            public interface IWellBehavedCapability
            {
                Task<string> EchoAsync(string value);
            }
            """
        );

    [Fact]
    public static Task FlagsADefaultInterfaceMethodBodyOnACapabilityContract() =>
        VerifyAsync(
            """
            [CapabilityContract]
            public interface IUnsafeCapability
            {
                Task<string> EchoAsync(string value);

                Task<string> {|#0:LogThenEchoAsync|}(string value)
                {
                    System.Console.WriteLine("this runs just from being called through the interface");
                    return EchoAsync(value);
                }
            }
            """,
            DefaultBody("IUnsafeCapability.LogThenEchoAsync").WithLocation(0)
        );

    [Fact]
    public static Task FlagsAModuleInitializerInAProjectThatDeclaresACapabilityContract() =>
        VerifyAsync(
            """
            [CapabilityContract]
            public interface IObservedCapability
            {
                Task<string> EchoAsync(string value);
            }

            internal static class Startup
            {
                [ModuleInitializer]
                internal static void {|#0:Initialize|}()
                {
                    System.Console.WriteLine("this runs the instant the assembly is loaded");
                }
            }
            """,
            ModuleInitializer("Initialize").WithLocation(0)
        );

    [Fact]
    public static Task DoesNotFlagAModuleInitializerWhenNoCapabilityContractIsDeclared() =>
        VerifyAsync(
            """
            internal static class Startup
            {
                [ModuleInitializer]
                internal static void Initialize() { }
            }
            """
        );
}
