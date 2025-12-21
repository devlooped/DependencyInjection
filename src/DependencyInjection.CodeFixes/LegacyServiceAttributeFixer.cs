using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Devlooped.Extensions.DependencyInjection;

[Shared]
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class LegacyServiceAttributeFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
        LegacyServiceAttributeAnalyzer.ServiceTypeNotKeyType.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var attribute = root.FindNode(context.Span).FirstAncestorOrSelf<AttributeSyntax>();
        if (attribute == null)
            return;

        context.RegisterCodeFix(new RemoveGenericParameterCodeAction(context.Document, attribute), context.Diagnostics);
    }

    class RemoveGenericParameterCodeAction(Document document, AttributeSyntax syntax) : CodeAction
    {
        public override string Title => "Remove generic parameter";
        public override string? EquivalenceKey => Title;

        protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null)
                return document;

            var isGeneric = syntax.Name is GenericNameSyntax;
            if (!isGeneric)
                return document;

            var newName = IdentifierName("Service");

            return document.WithSyntaxRoot(root.ReplaceNode(syntax, syntax.WithName(newName)));
        }
    }
}
