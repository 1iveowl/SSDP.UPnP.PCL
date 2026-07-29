using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SSDP.UPnP.PCL.Analyzers;

/// <summary>
/// Reports SSDP values that are outside the range UDA 2.0 sets, where the value is
/// known at compile time.
/// </summary>
/// <remarks>
/// <para>
/// One analyzer for three rules because they are one shape: a constant in a known
/// position, checked against a range the specification fixes. Splitting them into
/// three classes would triple the plumbing and share nothing.
/// </para>
/// <para>
/// All three report only for a literal or constant. A value arriving through a
/// parameter, a field, or a <c>with</c> expression is not reported - a rule that
/// guesses is a rule people learn to suppress, and the quiet rules beside it pay
/// for that.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SsdpValueRangeAnalyzer : DiagnosticAnalyzer
{
    private const string _mxSecondsTypeName = "SSDP.UPnP.PCL.Model.MxSeconds";
    private const string _dynamicPortTypeName = "SSDP.UPnP.PCL.Model.DynamicPort";
    private const string _rootDeviceConfigurationTypeName = "SSDP.UPnP.PCL.Model.RootDeviceConfiguration";
    private const string _iPEndPointTypeName = "System.Net.IPEndPoint";

    // UDA 2.0 section 1.3.2: MX "should be less than 5 inclusive". A should, not a
    // shall - the same clause allows raising it "if a large number of devices are
    // expected to respond" - so this is a warning with a documented escape, and the
    // floor is the type's job rather than this rule's.
    private const int _recommendedMaximumMx = 5;

    // UDA 2.0 section 1.2.2: freely assignable CONFIGID values are 0 to 2^24-1.
    private const int _maximumConfigId = 16777215;

    private const int _minDynamicPort = 49152;
    private const int _maxDynamicPort = 65535;
    private const int _ssdpPort = 1900;

    private static readonly DiagnosticDescriptor _mxAboveRecommendedMaximumRule = new(
        DiagnosticIds.MxAboveRecommendedMaximum,
        "M-SEARCH MX is above the 5 seconds UDA 2.0 recommends",
        "MX of {0} seconds exceeds the 5 UDA 2.0 recommends; devices assume 5 or less, so the extra wait never happens",
        DiagnosticIds.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "UDA 2.0 section 1.3.3 has a device assume an MX of 5 or less whenever a larger value "
            + "is sent, so a longer spread is silently not honoured. The specification does allow "
            + "raising MX when a large number of devices are expected to respond; suppress this "
            + "diagnostic where that is the intent.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.MxAboveRecommendedMaximum));

    private static readonly DiagnosticDescriptor _tcpPortOutOfRangeRule = new(
        DiagnosticIds.TcpPortOutOfRange,
        "SSDP port is outside the 49152-65535 range UDA 2.0 mandates",
        "Port {0} is outside the 49152-65535 range UDA 2.0 mandates; this throws at run time",
        DiagnosticIds.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "TCPPORT.UPNP.ORG and SEARCHPORT.UPNP.ORG are restricted to the RFC 4340 dynamic range. "
            + "Constructing a DynamicPort outside it throws, and a device receiving such a value "
            + "rejects the message.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.TcpPortOutOfRange));

    private static readonly DiagnosticDescriptor _deviceConfigurationOutOfRangeRule = new(
        DiagnosticIds.DeviceConfigurationOutOfRange,
        "Device configuration value is outside the range UDA 2.0 mandates",
        "{0}; constructing the device throws at run time",
        DiagnosticIds.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "CONFIGID.UPNP.ORG is freely assignable only from 0 to 16777215, and a device answers "
            + "unicast searches on port 1900 or on a port in the 49152-65535 SEARCHPORT range. "
            + "Both are validated when the Device is constructed.",
        helpLinkUri: DiagnosticIds.HelpLink(DiagnosticIds.DeviceConfigurationOutOfRange));

    // Analyzers run on every keystroke, so the properties a code fix reads are
    // built once per distinct value rather than per diagnostic.
    private static readonly ImmutableDictionary<string, string?> _clampMxToMaximumProperties =
        ImmutableDictionary<string, string?>.Empty
            .Add(DiagnosticIds.ReplacementValueKey, _recommendedMaximumMx.ToString());

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            _mxAboveRecommendedMaximumRule,
            _tcpPortOutOfRangeRule,
            _deviceConfigurationOutOfRangeRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var compilation = compilationStart.Compilation;

            var mxSeconds = compilation.GetTypeByMetadataName(_mxSecondsTypeName);
            var dynamicPort = compilation.GetTypeByMetadataName(_dynamicPortTypeName);
            var rootDeviceConfiguration = compilation.GetTypeByMetadataName(_rootDeviceConfigurationTypeName);
            var ipEndPoint = compilation.GetTypeByMetadataName(_iPEndPointTypeName);

            // Nothing to say about a compilation that does not reference the library.
            if (mxSeconds is null && dynamicPort is null && rootDeviceConfiguration is null)
            {
                return;
            }

            var known = new KnownTypes(mxSeconds, dynamicPort, rootDeviceConfiguration, ipEndPoint);

            compilationStart.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, known),
                OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, KnownTypes known)
    {
        var creation = (IObjectCreationOperation)context.Operation;

        if (creation.Type is null)
        {
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(creation.Type, known.MxSeconds))
        {
            AnalyzeMxSeconds(context, creation);
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(creation.Type, known.DynamicPort))
        {
            AnalyzeDynamicPort(context, creation);
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(creation.Type, known.RootDeviceConfiguration))
        {
            AnalyzeRootDeviceConfiguration(context, creation, known);
        }
    }

    private static void AnalyzeMxSeconds(OperationAnalysisContext context, IObjectCreationOperation creation)
    {
        if (TryGetSingleConstantInt(creation, out var seconds) && seconds > _recommendedMaximumMx)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _mxAboveRecommendedMaximumRule,
                creation.Syntax.GetLocation(),
                _clampMxToMaximumProperties,
                seconds));
        }
    }

    private static void AnalyzeDynamicPort(OperationAnalysisContext context, IObjectCreationOperation creation)
    {
        if (TryGetSingleConstantInt(creation, out var port) && (port < _minDynamicPort || port > _maxDynamicPort))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _tcpPortOutOfRangeRule,
                creation.Syntax.GetLocation(),
                port));
        }
    }

    private static void AnalyzeRootDeviceConfiguration(
        OperationAnalysisContext context,
        IObjectCreationOperation creation,
        KnownTypes known)
    {
        foreach (var assignment in InitializerAssignments(creation))
        {
            if (assignment.Target is not IPropertyReferenceOperation property)
            {
                continue;
            }

            switch (property.Property.Name)
            {
                case "CONFIGID" when TryGetConstantInt(assignment.Value, out var configId)
                                     && (configId < 0 || configId > _maximumConfigId):
                    context.ReportDiagnostic(Diagnostic.Create(
                        _deviceConfigurationOutOfRangeRule,
                        assignment.Syntax.GetLocation(),
                        $"CONFIGID {configId} is outside the 0-{_maximumConfigId} range UDA 2.0 allows"));
                    break;

                case "IpEndPoint" when TryGetEndPointPort(assignment.Value, known, out var port)
                                       && port != _ssdpPort
                                       && (port < _minDynamicPort || port > _maxDynamicPort):
                    context.ReportDiagnostic(Diagnostic.Create(
                        _deviceConfigurationOutOfRangeRule,
                        assignment.Value.Syntax.GetLocation(),
                        $"A device answers unicast searches on port {_ssdpPort} or in {_minDynamicPort}-{_maxDynamicPort}, not {port}"));
                    break;
            }
        }
    }

    private static IEnumerable<ISimpleAssignmentOperation> InitializerAssignments(IObjectCreationOperation creation) =>
        creation.Initializer is null
            ? Enumerable.Empty<ISimpleAssignmentOperation>()
            : creation.Initializer.Initializers.OfType<ISimpleAssignmentOperation>();

    // new IPEndPoint(address, 8080) - only the literal port is decidable, and the
    // address is irrelevant to the range rule.
    private static bool TryGetEndPointPort(IOperation value, KnownTypes known, out int port)
    {
        port = 0;

        return value is IObjectCreationOperation endPoint
               && known.IPEndPoint is not null
               && SymbolEqualityComparer.Default.Equals(endPoint.Type, known.IPEndPoint)
               && endPoint.Arguments.Length == 2
               && TryGetConstantInt(endPoint.Arguments[1].Value, out port);
    }

    private static bool TryGetSingleConstantInt(IObjectCreationOperation creation, out int value)
    {
        value = 0;

        return creation.Arguments.Length == 1 && TryGetConstantInt(creation.Arguments[0].Value, out value);
    }

    private static bool TryGetConstantInt(IOperation operation, out int value)
    {
        value = 0;

        // Unwrap the implicit conversions an argument position introduces, so a
        // constant reaches the check whether it was written as a literal, a const,
        // or a widened smaller integer.
        var current = operation;

        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        if (current.ConstantValue is { HasValue: true, Value: int constant })
        {
            value = constant;
            return true;
        }

        return false;
    }

    private sealed class KnownTypes
    {
        internal KnownTypes(
            INamedTypeSymbol? mxSeconds,
            INamedTypeSymbol? dynamicPort,
            INamedTypeSymbol? rootDeviceConfiguration,
            INamedTypeSymbol? ipEndPoint)
        {
            MxSeconds = mxSeconds;
            DynamicPort = dynamicPort;
            RootDeviceConfiguration = rootDeviceConfiguration;
            IPEndPoint = ipEndPoint;
        }

        internal INamedTypeSymbol? MxSeconds { get; }

        internal INamedTypeSymbol? DynamicPort { get; }

        internal INamedTypeSymbol? RootDeviceConfiguration { get; }

        internal INamedTypeSymbol? IPEndPoint { get; }
    }
}
