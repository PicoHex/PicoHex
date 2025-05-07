namespace Pico.Json.Generator;

internal class JsonSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (
            syntaxNode is ClassDeclarationSyntax classDecl
            && classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
            && !classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
        )
        {
            CandidateClasses.Add(classDecl);
        }
    }
}
