using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Girgis.InfraKit.SortableAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GirgisInfraKitSortableAnalyzerCodeFixProvider)), Shared]
    public class GirgisInfraKitSortableAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create("SORT001", "SORT002", "SORT003");
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticId = diagnostic.Id;

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null) return;

            var node = root.FindNode(context.Span);

            switch (diagnosticId)
            {
                case "SORT001":
                    context.RegisterCodeFix(
                        Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                            "Add [SortableDefault]",
                            ct => AddSortableDefaultAsync(context.Document, root, node, ct),
                            nameof(GirgisInfraKitSortableAnalyzerCodeFixProvider)),
                        diagnostic);
                    break;

                case "SORT002":
                    context.RegisterCodeFix(
                        Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                            "Add [Sortable] to property",
                            ct => AddSortableToPropertyAsync(context.Document, root, node, ct),
                            nameof(GirgisInfraKitSortableAnalyzerCodeFixProvider)),
                        diagnostic);
                    break;

                case "SORT003":
                    context.RegisterCodeFix(
                        Microsoft.CodeAnalysis.CodeActions.CodeAction.Create(
                            "Remove [SortableDefault]",
                            ct => RemoveSortableDefaultAttributeAsync(context.Document, root, node, ct),
                            nameof(GirgisInfraKitSortableAnalyzerCodeFixProvider)),
                        diagnostic);
                    break;
            }
        }

        private Task<Document> AddSortableDefaultAsync(Document document, SyntaxNode root, SyntaxNode node, CancellationToken ct)
        {
            var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDecl == null) return Task.FromResult(document);

            var firstSortableProp = classDecl.Members
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.AttributeLists.SelectMany(a => a.Attributes)
                    .Any(x => x.Name.ToString().Contains("Sortable")));

            if (firstSortableProp == null) return Task.FromResult(document);

            var attr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("SortableDefault"))
                .WithArgumentList(SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(firstSortableProp.Identifier.Text))))));

            var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            var newClassDecl = classDecl.AddAttributeLists(attrList);
            var newRoot = root.ReplaceNode(classDecl, newClassDecl);

            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private Task<Document> AddSortableToPropertyAsync(Document document, SyntaxNode root, SyntaxNode node, CancellationToken ct)
        {
            var propertyDecl = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
            if (propertyDecl == null) return Task.FromResult(document);

            var attr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Sortable"));
            var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            var newPropertyDecl = propertyDecl.AddAttributeLists(attrList);
            var newRoot = root.ReplaceNode(propertyDecl, newPropertyDecl);

            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private Task<Document> RemoveSortableDefaultAttributeAsync(Document document, SyntaxNode root, SyntaxNode node, CancellationToken ct)
        {
            if (!(node is ClassDeclarationSyntax classDecl)) return Task.FromResult(document);

            var newClass = classDecl.WithAttributeLists(
                new SyntaxList<AttributeListSyntax>(classDecl.AttributeLists
                    .Where(al => !al.Attributes.Any(a => a.Name.ToString().Contains("SortableDefault")))));

            var newRoot = root.ReplaceNode(classDecl, newClass);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }
    }
}
