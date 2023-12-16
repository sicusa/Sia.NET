namespace Sia.CodeGenerators;

using System.Reflection;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Globalization;
using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis.CSharp;

internal static class Common
{
    public record PropertyInfo(
        string Name, ITypeSymbol Type, string DisplayType,
        ImmutableArray<string> TypeArguments, ImmutableDictionary<string, TypedConstant> Arguments)
    {
        public string? ImmutableContainerType =>
            DisplayType.StartsWith(ImmutableContainerHead)
                ? DisplayType.Substring(
                    ImmutableContainerHead.Length,
                    DisplayType.IndexOf('<', ImmutableContainerHead.Length) - ImmutableContainerHead.Length)
                : null;

        private const string ImmutableContainerHead = "global::System.Collections.Immutable.Immutable";

        public PropertyInfo(string name, ITypeSymbol symbol, IEnumerable<AttributeData> attributes)
            : this(name, symbol, GetDisplayName(symbol),
                symbol is INamedTypeSymbol namedSymbol
                    ? namedSymbol.TypeArguments.Select(a => a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .ToImmutableArray()
                    : ImmutableArray<string>.Empty,
                (attributes
                    .FirstOrDefault(data =>
                        data!.AttributeClass!.ToDisplayString() == "Sia.SiaPropertyAttribute")
                    ?.NamedArguments.ToImmutableDictionary())
                        ?? ImmutableDictionary<string, TypedConstant>.Empty)
        {
        }

        private static string GetDisplayName(ITypeSymbol symbol)
        {
            var displayName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return symbol.NullableAnnotation == NullableAnnotation.Annotated && displayName[displayName.Length - 1] != '?'
                ? displayName + '?' : displayName;
        }

        public T GetArgument<T>(string name, in T defaultValue)
            => Arguments.TryGetValue(name, out var value)
                ? (T)value.Value! : defaultValue;
    }

    public static readonly AssemblyName AssemblyName = typeof(Common).Assembly.GetName();
    public static readonly string GeneratedCodeAttribute =
        $@"global::System.CodeDom.Compiler.GeneratedCodeAttribute(""{AssemblyName.Name}"", ""{AssemblyName.Version}"")";

    public static readonly SymbolDisplayFormat QualifiedTypeNameWithTypeConstraints = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
                | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FindParentNode<TNode>(
        SyntaxNode node, out TNode? result)
        where TNode : SyntaxNode
    {
        SyntaxNode? currNode = node;
        while (currNode != null) {
            var parent = currNode.Parent;
            if (parent is TNode casted) {
                result = casted;
                return true;
            }
            currNode = parent;
        }
        result = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableArray<TypeDeclarationSyntax> GetParentTypes(SyntaxNode node)
    {
        var builder = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();
        var parent = node.Parent;

        while (parent != null) {
            if (parent is TypeDeclarationSyntax typeDecl) {
                builder.Add(typeDecl);
            }
            parent = parent.Parent;
        }

        return builder.ToImmutable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ITypeSymbol GetNodeType(SemanticModel model, SyntaxNode typeNode, CancellationToken token)
        => model.GetTypeInfo(typeNode, token).Type!;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ITypeSymbol GetVariableType(SemanticModel model, VariableDeclaratorSyntax syntax, CancellationToken token) {
        var parentDecl = (VariableDeclarationSyntax)syntax.Parent!;
        return GetNodeType(model, parentDecl.Type, token);
    }

    public static IndentedTextWriter CreateSource(out StringBuilder builder)
    {
        builder = new StringBuilder();
        var writer = new StringWriter(builder, CultureInfo.InvariantCulture);
        var source = new IndentedTextWriter(writer, "    ");

        source.WriteLine("// <auto-generated/>");
        source.WriteLine("#nullable enable");
        source.WriteLine();

        return source;
    }

    private class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        public void Dispose() {}
    }

    private class EnclosingDisposable(IndentedTextWriter source, int count) : IDisposable
    {
        private readonly IndentedTextWriter _source = source;
        private readonly int _count = count;

        public void Dispose()
        {
            for (int i = 0; i < _count; ++i) {
                _source.Indent--;
                _source.WriteLine("}");
            }
        }
    }

    public static IDisposable GenerateInNamespace(IndentedTextWriter source, INamespaceSymbol ns)
    {
        if (!ns.IsGlobalNamespace) {
            source.Write("namespace ");
            source.WriteLine(ns.ToDisplayString());
            source.WriteLine("{");
            source.Indent++;
            return new EnclosingDisposable(source, 1);
        }
        else {
            return EmptyDisposable.Instance;
        }
    }

    public static IDisposable GenerateInPartialTypes(IndentedTextWriter source, IEnumerable<TypeDeclarationSyntax> typeDecls)
    {
        int indent = 0;
        foreach (var typeDecl in typeDecls) {
            if (typeDecl.Modifiers.Any(SyntaxKind.StaticKeyword)) {
                source.Write("static ");
            }
            switch (typeDecl.Kind()) {
            case SyntaxKind.ClassDeclaration:
                source.Write("partial class ");
                break;
            case SyntaxKind.StructDeclaration:
                source.Write("partial struct ");
                break;
            case SyntaxKind.RecordDeclaration:
                source.Write("partial record ");
                break;
            case SyntaxKind.RecordStructDeclaration:
                source.Write("partial record struct ");
                break;
            default:
                throw new InvalidDataException("Invalid containing type");
            }
            
            WriteType(source, typeDecl);

            source.WriteLine();
            source.WriteLine("{");
            source.Indent++;
            indent++;
        }
        return indent != 0 ? new EnclosingDisposable(source, indent) : EmptyDisposable.Instance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteType(IndentedTextWriter source, TypeDeclarationSyntax typeDecl)
    {
        source.Write(typeDecl.Identifier.ToString());
        WriteTypeParameters(source, typeDecl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteTypeParameters(IndentedTextWriter source, TypeDeclarationSyntax typeDecl)
    {
        var typeParams = typeDecl.TypeParameterList;
        if (typeParams != null) {
            WriteTypeParameters(source, typeParams);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteTypeParameters(IndentedTextWriter source, TypeParameterListSyntax typeParams)
    {
        source.Write('<');
        var paramsList = typeParams.Parameters;
        var lastIndex = paramsList.Count - 1;
        for (int i = 0; i != paramsList.Count; ++i) {
            source.Write(paramsList[i].Identifier.ToString());
            if (i != lastIndex) {
                source.Write(", ");
            }
        }
        source.Write('>');
    }
}