#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morpeh.BoilerplateGenerationProcessor;

[Generator]
public class AttributeGenerator : IIncrementalGenerator
{
    private const string IComponentFullName = "Scellecs.Morpeh.IComponent";
    private const string Il2CppSetOptionAttributeFullName = "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute";
    private const string SerializableAttributeFullName = "System.SerializableAttribute";

    private static readonly HashSet<string> MorpehSystemInterfaces = new()
    {
        "Scellecs.Morpeh.ISystem",
        "Scellecs.Morpeh.IInitializer",
        "Scellecs.Morpeh.IFixedSystem",
        "Scellecs.Morpeh.ILateSystem",
        "Scellecs.Morpeh.ICleanupSystem"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<TypeDeclarationInfo?> partialMorpehTypesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (s, _) => s is TypeDeclarationSyntax tds && tds.Modifiers.Any(SyntaxKind.PartialKeyword) &&
                                 tds.BaseList != null,
                static (ctx, _) => GetTypeDeclarationForGeneration(ctx))
            .Where(static m => m.HasValue);

        context.RegisterSourceOutput(partialMorpehTypesProvider,
            static (spc, source) => Execute(spc, source!.Value));
    }

    private static TypeDeclarationInfo? GetTypeDeclarationForGeneration(GeneratorSyntaxContext context)
    {
        try
        {
            var typeDeclarationSyntax = (TypeDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(typeDeclarationSyntax) is not INamedTypeSymbol typeSymbol)
                return null;

            if (!IsCandidateForGeneration(typeSymbol) ||
                !IsMorpehTarget(typeSymbol, out MorpehTypeInfo morpehTypeInfo)) return null;

            for (SyntaxNode? parent = typeDeclarationSyntax.Parent;
                 parent is ClassDeclarationSyntax parentClassSyntax;
                 parent = parent.Parent)
                if (parentClassSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    string parentClassName = parentClassSyntax.Identifier.Text;
                    string childTypeName = typeDeclarationSyntax.Identifier.Text;
                    var diagnostic = Diagnostic.Create(NestedInStaticClassError,
                        typeDeclarationSyntax.Identifier.GetLocation(), childTypeName, parentClassName);
                    return new TypeDeclarationInfo(diagnostic);
                }

            var needsSerializable = false;
            var needsNullChecks = false;
            var needsArrayBoundsChecks = false;

            if (morpehTypeInfo.IsComponent)
            {
                needsSerializable = !HasAttribute(typeSymbol, SerializableAttributeFullName);
            }
            else if (morpehTypeInfo.IsSystem)
            {
                HashSet<string> existingIl2CppOptions = GetExistingIl2CppOptions(typeSymbol);
                needsNullChecks = !existingIl2CppOptions.Contains("NullChecks");
                needsArrayBoundsChecks = !existingIl2CppOptions.Contains("ArrayBoundsChecks");
            }

            bool needsAnyAttribute = needsSerializable || needsNullChecks || needsArrayBoundsChecks;

            if (!needsAnyAttribute) return null;

            return CreateSuccessInfo(typeSymbol, typeDeclarationSyntax, needsSerializable, needsNullChecks,
                needsArrayBoundsChecks);
        }
        catch (Exception ex)
        {
            string typeName = (context.Node as TypeDeclarationSyntax)?.Identifier.Text ?? "<unknown>";
            return new TypeDeclarationInfo(Diagnostic.Create(ProcessingError, context.Node.GetLocation(), typeName,
                ex.ToString()));
        }
    }

    private static bool IsCandidateForGeneration(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsStatic) return false;

        return typeSymbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;
    }

    private static bool IsMorpehTarget(INamedTypeSymbol typeSymbol, out MorpehTypeInfo morpehTypeInfo)
    {
        bool implementsIComponent =
            typeSymbol.AllInterfaces.Any(iface => iface.ToDisplayString() == IComponentFullName);

        var implementsMorpehSystem = false;
        if (!implementsIComponent)
            implementsMorpehSystem =
                typeSymbol.AllInterfaces.Any(iface => MorpehSystemInterfaces.Contains(iface.ToDisplayString()));

        bool isComponent = typeSymbol.TypeKind == TypeKind.Struct && implementsIComponent;
        bool isSystem = typeSymbol.TypeKind == TypeKind.Class && implementsMorpehSystem;

        morpehTypeInfo = new MorpehTypeInfo(isComponent, isSystem);
        return isComponent || isSystem;
    }

    private static bool HasAttribute(INamedTypeSymbol typeSymbol, string attributeFullName)
    {
        return typeSymbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeFullName);
    }

    private static HashSet<string> GetExistingIl2CppOptions(INamedTypeSymbol typeSymbol)
    {
        var options = new HashSet<string>();
        foreach (AttributeData? attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != Il2CppSetOptionAttributeFullName ||
                attr.ConstructorArguments.Length != 2) continue;

            TypedConstant optionArgument = attr.ConstructorArguments[0];
            if (optionArgument.Kind != TypedConstantKind.Enum || optionArgument.Type?.Name != "Option") continue;

            if (optionArgument.Type is not INamedTypeSymbol enumType) continue;

            foreach (ISymbol? member in enumType.GetMembers())
                if (member is IFieldSymbol { IsConst: true } field && field.ConstantValue is not null &&
                    field.ConstantValue.Equals(optionArgument.Value))
                {
                    options.Add(field.Name);
                    break;
                }
        }

        return options;
    }

    private static TypeDeclarationInfo CreateSuccessInfo(INamedTypeSymbol typeSymbol, TypeDeclarationSyntax syntax,
        bool needsSerializable, bool needsNullChecks, bool needsArrayBoundsChecks)
    {
        string namespaceName = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        string accessibility = typeSymbol.DeclaredAccessibility is Accessibility.Public ? "public" : "internal";

        return new TypeDeclarationInfo(
            syntax.Identifier.Text,
            namespaceName,
            GetParentClassHierarchy(syntax),
            typeSymbol.IsValueType,
            accessibility,
            needsSerializable,
            needsNullChecks,
            needsArrayBoundsChecks
        );
    }

    private static void Execute(SourceProductionContext context, TypeDeclarationInfo typeInfo)
    {
        foreach (Diagnostic diagnostic in typeInfo.Diagnostics) context.ReportDiagnostic(diagnostic);
        if (string.IsNullOrEmpty(typeInfo.Name) || typeInfo.HasErrors) return;

        try
        {
            string sourceCode = GenerateSource(typeInfo);
            context.AddSource($"{typeInfo.GetGeneratedFileName()}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(ProcessingError, Location.None, typeInfo.Name,
                $"Source generation failed: {ex}"));
        }
    }

    private static string GenerateSource(TypeDeclarationInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();

        bool hasNamespace = !string.IsNullOrEmpty(info.Namespace);
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {info.Namespace}");
            sb.AppendLine("{");
        }

        var indent = new Indent(hasNamespace ? 1 : 0);

        foreach (ParentClassInfo parentClass in info.ParentClasses)
        {
            sb.AppendLine($"{indent}{parentClass.Accessibility} partial class {parentClass.Name}");
            sb.AppendLine($"{indent}{{");
            indent.Increment();
        }

        if (info.NeedsSerializableAttribute) sb.AppendLine($"{indent}[global::System.Serializable]");
        if (info.NeedsNullChecks)
            sb.AppendLine(
                $"{indent}[global::Unity.IL2CPP.CompilerServices.Il2CppSetOption(global::Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]");
        if (info.NeedsArrayBoundsChecks)
            sb.AppendLine(
                $"{indent}[global::Unity.IL2CPP.CompilerServices.Il2CppSetOption(global::Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]");

        string typeKeyword = info.IsStruct ? "struct" : "class";
        sb.AppendLine($"{indent}{info.Accessibility} partial {typeKeyword} {info.Name}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}}}");

        for (var i = 0; i < info.ParentClasses.Count; i++)
        {
            indent.Decrement();
            sb.AppendLine($"{indent}}}");
        }

        if (hasNamespace) sb.AppendLine("}");

        return sb.ToString();
    }

    private static List<ParentClassInfo> GetParentClassHierarchy(TypeDeclarationSyntax typeSyntax)
    {
        var hierarchy = new List<ParentClassInfo>();
        for (SyntaxNode? parent = typeSyntax.Parent;
             parent is ClassDeclarationSyntax classSyntax;
             parent = classSyntax.Parent)
        {
            string accessibility = classSyntax.Modifiers.Any(SyntaxKind.PublicKeyword) ? "public" : "internal";
            hierarchy.Add(new ParentClassInfo(classSyntax.Identifier.Text, accessibility));
        }

        hierarchy.Reverse();
        return hierarchy;
    }

    private readonly struct MorpehTypeInfo
    {
        public readonly bool IsComponent;
        public readonly bool IsSystem;

        public MorpehTypeInfo(bool isComponent, bool isSystem)
        {
            IsComponent = isComponent;
            IsSystem = isSystem;
        }
    }

    private sealed class Indent
    {
        private int _level;

        public Indent(int initialLevel)
        {
            _level = initialLevel;
        }

        public void Increment()
        {
            _level++;
        }

        public void Decrement()
        {
            _level--;
        }

        public override string ToString()
        {
            return new string(' ', _level * 4);
        }
    }


    private readonly struct ParentClassInfo
    {
        public readonly string Name;
        public readonly string Accessibility;

        public ParentClassInfo(string name, string accessibility)
        {
            Name = name;
            Accessibility = accessibility;
        }
    }

    private readonly struct TypeDeclarationInfo
    {
        public readonly string Name;
        public readonly string Namespace;
        public readonly List<ParentClassInfo> ParentClasses;
        public readonly bool IsStruct;
        public readonly string Accessibility;
        public readonly bool NeedsSerializableAttribute;
        public readonly bool NeedsNullChecks;
        public readonly bool NeedsArrayBoundsChecks;

        public List<Diagnostic> Diagnostics { get; }

        public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

        public TypeDeclarationInfo(string name, string ns, List<ParentClassInfo> parentClasses, bool isStruct,
            string accessibility, bool needsSerializable, bool needsNullChecks, bool needsArrayBoundsChecks)
        {
            Name = name;
            Namespace = ns;
            ParentClasses = parentClasses;
            IsStruct = isStruct;
            Accessibility = accessibility;
            NeedsSerializableAttribute = needsSerializable;
            NeedsNullChecks = needsNullChecks;
            NeedsArrayBoundsChecks = needsArrayBoundsChecks;
            Diagnostics = new List<Diagnostic>();
        }

        public TypeDeclarationInfo(Diagnostic diagnostic)
        {
            Name = string.Empty;
            Namespace = string.Empty;
            ParentClasses = new List<ParentClassInfo>();
            IsStruct = false;
            Accessibility = string.Empty;
            NeedsSerializableAttribute = false;
            NeedsNullChecks = false;
            NeedsArrayBoundsChecks = false;
            Diagnostics = new List<Diagnostic> { diagnostic };
        }

        public string GetGeneratedFileName()
        {
            if (ParentClasses.Count == 0) return Name;
            return $"{string.Join(".", ParentClasses.Select(p => p.Name))}.{Name}";
        }
    }

    #region Diagnostics

    private static readonly DiagnosticDescriptor ProcessingError = new(
        "BGP001", "Processing Error", "Error processing type '{0}': {1}",
        "Generator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NestedInStaticClassError = new(
        "BGP002", "Invalid Nesting in Static Class",
        "The Morpeh type '{0}' cannot be nested inside the static class '{1}'. Static classes cannot be 'partial', which is required for source generation.",
        "Design", DiagnosticSeverity.Error, true);

    #endregion
}