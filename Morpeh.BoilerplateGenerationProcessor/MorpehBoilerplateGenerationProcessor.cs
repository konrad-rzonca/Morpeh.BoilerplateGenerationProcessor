
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morpeh.BoilerplateGenerationProcessor
{
    [Generator]
    public class AttributeGenerator : IIncrementalGenerator
    {
        private const string IComponentFullName = "Scellecs.Morpeh.IComponent";
        private const string Il2CppSetOptionAttributeFullName = "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute";

        private static readonly HashSet<string> MorpehSystemInterfaces = new HashSet<string>
        {
            "Scellecs.Morpeh.ISystem",
            "Scellecs.Morpeh.IInitializer",
            "Scellecs.Morpeh.IFixedSystem",
            "Scellecs.Morpeh.ILateSystem",
            "Scellecs.Morpeh.ICleanupSystem"
        };

        private static readonly DiagnosticDescriptor NonPartialTypeWarning = new DiagnosticDescriptor(
            id: "MORPEH001",
            title: "Type Should Be Partial",
            messageFormat: "Type '{0}' is a Morpeh Component or System and should be declared 'partial' for automatic attribute generation",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor ProcessingError = new DiagnosticDescriptor(
            id: "MORPEH002",
            title: "Processing Error",
            messageFormat: "Error processing type '{0}': {1}",
            category: "Generator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor NullSymbolWarning = new DiagnosticDescriptor(
            id: "MORPEH003",
            title: "Null Symbol Warning",
            messageFormat: "Could not get symbol for type at {0}. Type: {1}, Reason: {2}",
            category: "Generator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // Changed to Warning so Unity shows logs by default
        private static readonly DiagnosticDescriptor GenerationInfo = new DiagnosticDescriptor(
            id: "MORPEH004",
            title: "Generation Info",
            messageFormat: "Generated attributes for {0} type '{1}' in namespace '{2}'",
            category: "Generator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SkippedTypeInfo = new DiagnosticDescriptor(
            id: "MORPEH005",
            title: "Skipped Type",
            messageFormat: "Skipped type '{0}': {1}",
            category: "Generator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var typeDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => s is TypeDeclarationSyntax,
                    transform: static (ctx, _) => GetTypeDeclarationForGeneration(ctx))
                .Where(static m => m is not null);

            context.RegisterSourceOutput(typeDeclarations,
                static (spc, source) => Execute(spc, source));
        }

        private static TypeDeclarationInfo? GetTypeDeclarationForGeneration(GeneratorSyntaxContext context)
        {
            TypeDeclarationSyntax typeDeclarationSyntax = null;
            string typeName = "<unknown>";
            Location location = null;

            try
            {
                // NOTE: Removed strict Assets/ path filtering to make generator robust across
                // different project layouts (Packages, Tests, Editor-only builds, etc.).

                typeDeclarationSyntax = context.Node as TypeDeclarationSyntax;
                if (typeDeclarationSyntax == null)
                {
                    return new TypeDeclarationInfo(
                        Diagnostic.Create(NullSymbolWarning,
                            context.Node.GetLocation(),
                            context.Node.GetLocation(),
                            context.Node.GetType().Name,
                            "Not a TypeDeclarationSyntax"));
                }

                typeName = typeDeclarationSyntax.Identifier.Text;
                location = typeDeclarationSyntax.GetLocation();

                var semanticModel = context.SemanticModel;
                if (semanticModel == null)
                {
                    return new TypeDeclarationInfo(
                        Diagnostic.Create(NullSymbolWarning,
                            location,
                            location,
                            typeName,
                            "Semantic model is null"));
                }

                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclarationSyntax) as INamedTypeSymbol;
                if (typeSymbol == null)
                {
                    return new TypeDeclarationInfo(
                        Diagnostic.Create(NullSymbolWarning,
                            location,
                            location,
                            typeName,
                            "Could not get declared symbol"));
                }

                // Full display name for diagnostics
                typeName = typeSymbol.ToDisplayString();

                // If type is static - never generate for static types
                if (typeSymbol.IsStatic)
                {
                    return new TypeDeclarationInfo(
                        Diagnostic.Create(SkippedTypeInfo,
                            location,
                            typeName,
                            "Static types are ignored by the generator"));
                }

                // Quick check: if type already has IL2CPP attribute, skip
                if (HasRequiredAttributes(typeSymbol))
                {
                    return new TypeDeclarationInfo(
                        Diagnostic.Create(SkippedTypeInfo,
                            location,
                            typeName,
                            "Already has IL2CPP attributes"));
                }

                // Collect interface names using semantic model (preferred)
                var allInterfaceNames = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    foreach (var iface in typeSymbol.AllInterfaces)
                    {
                        if (iface?.ToDisplayString() != null)
                            allInterfaceNames.Add(iface.ToDisplayString());
                        if (iface?.Name != null)
                            allInterfaceNames.Add(iface.Name);
                    }
                }
                catch
                {
                    // ignored - we'll fall back to syntax-based checks below
                }

                // Also collect base type chain names (semantic)
                var baseTypeNames = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    for (var bt = typeSymbol.BaseType; bt != null; bt = bt.BaseType)
                    {
                        if (!string.IsNullOrEmpty(bt.ToDisplayString())) baseTypeNames.Add(bt.ToDisplayString());
                        if (!string.IsNullOrEmpty(bt.Name)) baseTypeNames.Add(bt.Name);
                    }
                }
                catch
                {
                    // ignore
                }

                // Also inspect the syntax base list (names as written in source) to handle unresolved symbols
                var syntaxBaseTypeNames = new HashSet<string>(StringComparer.Ordinal);
                if (typeDeclarationSyntax.BaseList != null)
                {
                    foreach (var t in typeDeclarationSyntax.BaseList.Types)
                    {
                        try
                        {
                            var txt = t.Type.ToString();
                            if (!string.IsNullOrWhiteSpace(txt)) syntaxBaseTypeNames.Add(txt);
                            // also add simple identifier (without generics or namespaces)
                            var simple = txt.Split('<')[0].Split('.').LastOrDefault();
                            if (!string.IsNullOrWhiteSpace(simple)) syntaxBaseTypeNames.Add(simple);
                        }
                        catch { }
                    }
                }

                // Determine if this is a Morpeh component or Morpeh system.
                bool implementsIComponent = allInterfaceNames.Contains(IComponentFullName) ||
                                            allInterfaceNames.Contains("IComponent") ||
                                            baseTypeNames.Contains(IComponentFullName) ||
                                            baseTypeNames.Contains("IComponent") ||
                                            syntaxBaseTypeNames.Contains("IComponent") ||
                                            syntaxBaseTypeNames.Contains(IComponentFullName);

                bool implementsMorpehSystem = allInterfaceNames.Overlaps(MorpehSystemInterfaces) ||
                                              allInterfaceNames.Overlaps(MorpehSystemInterfaces.Select(s => s.Split('.').Last())) ||
                                              baseTypeNames.Overlaps(MorpehSystemInterfaces) ||
                                              baseTypeNames.Overlaps(MorpehSystemInterfaces.Select(s => s.Split('.').Last())) ||
                                              syntaxBaseTypeNames.Overlaps(MorpehSystemInterfaces.Select(s => s.Split('.').Last()));

                bool isMorpehComponent = typeSymbol.TypeKind == TypeKind.Struct && implementsIComponent;
                bool isMorpehSystem = typeSymbol.TypeKind == TypeKind.Class && implementsMorpehSystem;

                bool isPartial = typeDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword);

                // If non-static Morpeh types need partial keyword - warn
                if ((isMorpehComponent || isMorpehSystem) && !isPartial)
                {
                    return new TypeDeclarationInfo(
                        Diagnostic.Create(NonPartialTypeWarning, location, typeSymbol.Name));
                }

                // Only proceed with generation for partial Morpeh types
                if (!isPartial || (!isMorpehComponent && !isMorpehSystem))
                {
                    // Not a target type - skip quietly
                    return null;
                }

                string namespaceName = string.Empty;
                if (typeSymbol.ContainingNamespace != null && !typeSymbol.ContainingNamespace.IsGlobalNamespace)
                {
                    namespaceName = typeSymbol.ContainingNamespace.ToDisplayString();
                }

                string accessibility = typeSymbol.DeclaredAccessibility switch
                {
                    Accessibility.Public => "public",
                    Accessibility.Internal => "internal",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(accessibility))
                {
                    return new TypeDeclarationInfo(
                        Diagnostic.Create(SkippedTypeInfo,
                            location,
                            typeName,
                            $"Unsupported accessibility: {typeSymbol.DeclaredAccessibility}"));
                }

                // Prepare success info
                var successInfo = new TypeDeclarationInfo(
                    typeDeclarationSyntax.Identifier.Text,
                    namespaceName,
                    GetParentClassHierarchy(typeDeclarationSyntax),
                    typeSymbol.IsValueType,
                    accessibility
                );

                // Add warning diagnostic about generation so Unity will show it
                successInfo.AddInfoDiagnostic(
                    Diagnostic.Create(GenerationInfo,
                        location,
                        typeSymbol.IsValueType ? "struct" : "class",
                        typeSymbol.Name,
                        string.IsNullOrEmpty(namespaceName) ? "<global>" : namespaceName));

                return successInfo;
            }
            catch (Exception ex)
            {
                var errorLocation = location ?? (typeDeclarationSyntax?.GetLocation() ?? Location.None);
                return new TypeDeclarationInfo(
                    Diagnostic.Create(ProcessingError,
                        errorLocation,
                        typeName,
                        $"{ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}"));
            }
        }

        private static bool HasRequiredAttributes(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return false;

            try
            {
                foreach (var attr in typeSymbol.GetAttributes())
                {
                    if (attr?.AttributeClass != null &&
                        attr.AttributeClass.ToDisplayString() == Il2CppSetOptionAttributeFullName)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // If attribute inspection fails for any reason, don't treat it as having attributes.
            }

            return false;
        }

        private static void Execute(SourceProductionContext context, TypeDeclarationInfo? typeInfo)
        {
            if (typeInfo == null || !typeInfo.HasValue) return;

            // Report all diagnostics so developer sees what's happening in Unity's Console
            if (typeInfo.Value.Diagnostics != null)
            {
                foreach (var diagnostic in typeInfo.Value.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Don't generate if name missing or there were errors
            if (string.IsNullOrEmpty(typeInfo.Value.Name) || typeInfo.Value.HasErrors)
            {
                return;
            }

            try
            {
                var sourceCode = GenerateSource(typeInfo.Value);
                context.AddSource($"{typeInfo.Value.Name}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(ProcessingError,
                        Location.None,
                        typeInfo.Value.Name,
                        $"Source generation failed: {ex.Message}"));
            }
        }

        private static string GenerateSource(TypeDeclarationInfo info)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine($"// Generated for: {info.Name}");
            sb.AppendLine($"// Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            // Removed pragma warning disable/restore so generated files don't silence important compiler warnings

            bool hasNamespace = !string.IsNullOrEmpty(info.Namespace);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {info.Namespace}");
                sb.AppendLine("{");
            }

            var indent = new Indent(hasNamespace ? 1 : 0);

            if (info.ParentClasses != null && info.ParentClasses.Count > 0)
            {
                foreach (var parentClass in info.ParentClasses)
                {
                    sb.AppendLine($"{indent}{parentClass.Accessibility} partial class {parentClass.Name}");
                    sb.AppendLine($"{indent}{{");
                    indent.Increment();
                }
            }

            sb.AppendLine($"{indent}[global::System.Serializable]");
            sb.AppendLine($"{indent}[global::Unity.IL2CPP.CompilerServices.Il2CppSetOption(global::Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]");
            sb.AppendLine($"{indent}[global::Unity.IL2CPP.CompilerServices.Il2CppSetOption(global::Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]");
            sb.AppendLine($"{indent}[global::Unity.IL2CPP.CompilerServices.Il2CppSetOption(global::Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]");

            string typeKeyword = info.IsStruct ? "struct" : "class";
            sb.AppendLine($"{indent}{info.Accessibility} partial {typeKeyword} {info.Name}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}}}");

            if (info.ParentClasses != null && info.ParentClasses.Count > 0)
            {
                foreach (var _ in info.ParentClasses)
                {
                    indent.Decrement();
                    sb.AppendLine($"{indent}}}");
                }
            }

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private static List<ParentClassInfo> GetParentClassHierarchy(TypeDeclarationSyntax typeSyntax)
        {
            if (typeSyntax == null)
            {
                return null;
            }

            var hierarchy = new List<ParentClassInfo>();
            try
            {
                for (var parent = typeSyntax.Parent; parent is ClassDeclarationSyntax classSyntax; parent = classSyntax.Parent)
                {
                    if (classSyntax == null) break;

                    string accessibility = classSyntax.Modifiers.Any(SyntaxKind.PublicKeyword) ? "public" : "internal";
                    hierarchy.Add(new ParentClassInfo(classSyntax.Identifier.Text, accessibility));
                }
            }
            catch
            {
                // If we can't get parent hierarchy, just return what we have
            }

            if (hierarchy.Count > 0)
            {
                hierarchy.Reverse();
                return hierarchy;
            }

            return null;
        }

        private class Indent
        {
            private int _level;
            public Indent(int initialLevel) => _level = initialLevel;
            public void Increment() => _level++;
            public void Decrement() => _level--;
            public override string ToString() => new string(' ', _level * 4);
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
            public readonly List<Diagnostic> Diagnostics;

            public bool HasErrors => Diagnostics?.Any(d => d.Severity == DiagnosticSeverity.Error) ?? false;

            public TypeDeclarationInfo(string name, string ns, List<ParentClassInfo> parentClasses, bool isStruct, string accessibility)
            {
                Name = name;
                Namespace = ns;
                ParentClasses = parentClasses;
                IsStruct = isStruct;
                Accessibility = accessibility;
                Diagnostics = new List<Diagnostic>();
            }

            public TypeDeclarationInfo(Diagnostic diagnostic)
            {
                Name = string.Empty;
                Namespace = string.Empty;
                ParentClasses = null;
                IsStruct = false;
                Accessibility = string.Empty;
                Diagnostics = new List<Diagnostic> { diagnostic };
            }

            public void AddInfoDiagnostic(Diagnostic diagnostic)
            {
                if (Diagnostics != null)
                {
                    Diagnostics.Add(diagnostic);
                }
            }
        }
    }
}
