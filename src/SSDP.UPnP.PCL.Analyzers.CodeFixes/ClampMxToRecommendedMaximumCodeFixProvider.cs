using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SSDP.UPnP.PCL.Analyzers.CodeFixes;

/// <summary>
/// Replaces an out-of-range <c>MX</c> with the 5 seconds UDA 2.0 recommends.
/// </summary>
/// <remarks>
/// Safe because it is what already happens: section 1.3.3 has every compliant
/// device assume 5 for anything larger, so this changes the declared intent to
/// match the behaviour rather than the other way round. It is the only fix in this
/// package, because it is the only one where the correct value can be inferred - a
/// port or a configuration number is the caller's to choose.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClampMxToRecommendedMaximumCodeFixProvider))]
[Shared]
public sealed class ClampMxToRecommendedMaximumCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.MxAboveRecommendedMaximum);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(DiagnosticIds.ReplacementValueKey, out var replacement)
                || replacement is null)
            {
                continue;
            }

            var creation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .AncestorsAndSelf()
                .OfType<ObjectCreationExpressionSyntax>()
                .FirstOrDefault();

            if (creation?.ArgumentList is null || creation.ArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Use the recommended maximum of {replacement} seconds",
                    _ => Task.FromResult(WithClampedArgument(context.Document, root, creation, replacement)),
                    equivalenceKey: DiagnosticIds.MxAboveRecommendedMaximum),
                diagnostic);
        }
    }

    private static Document WithClampedArgument(
        Document document,
        SyntaxNode root,
        ObjectCreationExpressionSyntax creation,
        string replacement)
    {
        var argument = creation.ArgumentList!.Arguments[0];

        var clamped = argument.WithExpression(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(int.Parse(replacement))));

        return document.WithSyntaxRoot(root.ReplaceNode(argument, clamped));
    }
}
