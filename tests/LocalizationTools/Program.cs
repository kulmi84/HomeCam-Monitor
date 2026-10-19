using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

var root = Path.GetFullPath(args[0]);
var options = new CSharpParseOptions(preprocessorSymbols: ["BETA", "V2_BETA", "RELEASE_V2", "STORE_PACKAGE"]);
var strings = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
var testMode = args.Length > 1 && args[1] == "apply-tests";
foreach (var file in Directory.GetFiles(testMode ? Path.Combine(root,"tests/V2Isolation") : root, "*.cs"))
{
    if (Path.GetFileName(file).StartsWith("UiText") || Path.GetFileName(file) == "LocalizationChecks.cs") continue;
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), options);
    foreach (var node in tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().Where(n => n.IsKind(SyntaxKind.StringLiteralExpression)))
    {
        var value = node.Token.ValueText;
        if (value.Length < 3) continue;
        if (!strings.TryGetValue(value, out var locations)) strings[value] = locations = [];
        locations.Add($"{Path.GetFileName(file)}:{tree.GetLineSpan(node.Span).StartLinePosition.Line + 1}");
    }
    foreach (var node in tree.GetRoot().DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
    {
        var value = Templates.Key(node);
        if (!strings.TryGetValue(value, out var locations)) strings[value] = locations = [];
        locations.Add($"{Path.GetFileName(file)}:{tree.GetLineSpan(node.Span).StartLinePosition.Line + 1} (format)");
    }
    if (args.Length > 1 && args[1].StartsWith("apply"))
    {
        var catalog = File.ReadAllLines(Path.Combine(root,"assets/Strings.en.tsv")).Where(l=>l.Length>0).Select(l=>l.Split('\t',2))
            .ToDictionary(p=>p[0].Replace("\\n","\n").Replace("\\r","\r"),p=>p[1],StringComparer.Ordinal);
        var changed = new Translator(catalog).Visit(tree.GetRoot());
        File.WriteAllText(file, changed!.ToFullString());
    }
}
File.WriteAllText(Path.Combine(root,"store-artifacts/localization-strings.json"), JsonSerializer.Serialize(strings, new JsonSerializerOptions {WriteIndented=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
Console.WriteLine($"{strings.Count} distinct literals extracted.");

static class Templates
{
    internal static string Key(InterpolatedStringExpressionSyntax node)
    {
        var value = new System.Text.StringBuilder(); var index = 0;
        foreach (var part in node.Contents)
            if (part is InterpolatedStringTextSyntax text) value.Append(text.TextToken.ValueText);
            else if (part is InterpolationSyntax item)
                value.Append('{').Append(index++).Append(item.AlignmentClause?.ToString()).Append(item.FormatClause?.ToString()).Append('}');
        return value.ToString();
    }
}
sealed class Translator(Dictionary<string,string> catalog) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (!node.IsKind(SyntaxKind.StringLiteralExpression) || !catalog.ContainsKey(node.Token.ValueText)) return base.VisitLiteralExpression(node);
        if (node.Ancestors().Any(n => n is AttributeSyntax or ConstantPatternSyntax or CaseSwitchLabelSyntax || n is FieldDeclarationSyntax f && f.Modifiers.Any(SyntaxKind.ConstKeyword))) return node;
        // Applying twice must never nest lookup calls or change catalog keys.
        if (node.Parent is ArgumentSyntax a && a.Parent?.Parent is InvocationExpressionSyntax call && call.Expression.ToString().StartsWith("UiText.")) return node;
        return SyntaxFactory.ParseExpression("UiText.T(" + node.WithoutTrivia() + ")").WithTriviaFrom(node);
    }
    public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        var key = Templates.Key(node);
        if (!catalog.ContainsKey(key)) return base.VisitInterpolatedStringExpression(node);
        var quoted = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(key)).ToString();
        var arguments = node.Contents.OfType<InterpolationSyntax>().Select(i => Visit(i.Expression)!.ToString());
        return SyntaxFactory.ParseExpression("UiText.F(" + quoted + ", " + string.Join(", ",arguments) + ")").WithTriviaFrom(node);
    }
}
