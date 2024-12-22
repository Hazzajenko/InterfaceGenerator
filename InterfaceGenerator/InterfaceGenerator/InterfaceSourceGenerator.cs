using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace InterfaceGenerator;

[Generator]
public class InterfaceSourceGenerator : ISourceGenerator
{
    private static readonly HashSet<string> Keywords =
    [
        "event", "object", "string", "class", "namespace", "new", "null", "base",
        "this", "void", "int", "long", "float", "double", "decimal", "bool",
        "true", "false", "if", "else", "while", "for", "foreach", "in", "do",
        "switch", "case", "break", "continue", "default", "goto", "return",
        "try", "catch", "finally", "throw", "throws", "public", "private",
        "protected", "internal", "static", "readonly", "volatile", "virtual",
        "abstract", "override", "params", "ref", "out"
    ];

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new InterfaceSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not InterfaceSyntaxReceiver receiver)
        {
            return;
        }

        foreach (TypeDeclarationSyntax? classDeclaration in receiver.CandidateClasses)
        {
            SemanticModel semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            ISymbol? symbol = semanticModel.GetDeclaredSymbol(classDeclaration);

            if (symbol is not INamedTypeSymbol classSymbol)
            {
                continue;
            }

            AttributeData? attributeData = classSymbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == "GenerateInterfaceAttributeDeprecated");

            if (attributeData == null)
            {
                continue;
            }

            string? baseTypeName = attributeData.ConstructorArguments.Length > 0
                ? attributeData.ConstructorArguments[0].Value?.ToString()
                : null;

            List<INamedTypeSymbol> typeChain = [classSymbol];

            if (!string.IsNullOrEmpty(baseTypeName))
            {
                INamedTypeSymbol? currentType = classSymbol.BaseType;
                bool foundTargetType = false;

                while (currentType != null)
                {
                    typeChain.Add(currentType);
                    if (currentType.Name == baseTypeName)
                    {
                        foundTargetType = true;
                        break;
                    }

                    currentType = currentType.BaseType;
                }

                if (!foundTargetType)
                {
                    continue; // Skip if we didn't find the specified base type
                }
            }

            StringBuilder sourceBuilder = new();
            GenerateInterface(classDeclaration, sourceBuilder, classSymbol, semanticModel);

            string typeName = classDeclaration.Identifier.Text;

            SourceText sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
            context.AddSource($"I{typeName}.g.cs", sourceText);
        }
    }

    private static void GenerateInterface(TypeDeclarationSyntax typeDeclaration, StringBuilder sourceBuilder, INamedTypeSymbol classSymbol, SemanticModel semanticModel)
    {
        string accessibility = classSymbol.DeclaredAccessibility == Accessibility.Public
            ? "public"
            : "internal";
        string className = typeDeclaration.Identifier.Text;
        sourceBuilder.AppendLine($"namespace {GetNamespace(typeDeclaration)};");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("#nullable enable");
        sourceBuilder.AppendLine($"{accessibility} partial interface I{className}");
        sourceBuilder.AppendLine("{");

        HashSet<string> processedMembers = new();

        foreach (ISymbol member in classSymbol.GetMembers())
        {
            // check if the member has the GenerateIgnoreAttribute, if so, skip it
            if (member.GetAttributes().Any(ad => ad.AttributeClass?.Name == "GenerateIgnoreAttributeDeprecated"))
            {
                continue;
            }

            if (member.IsImplicitlyDeclared    ||
                member.Name.StartsWith("get_") ||
                member.Name.StartsWith("set_") ||
                member.Name.StartsWith("add_") ||
                member.Name.StartsWith("remove_"))
            {
                continue;
            }

            if (member.IsStatic)
            {
                continue;
            }

            // Only include public members and members with at least one public accessor
            if (member.DeclaredAccessibility != Accessibility.Public)
            {
                if (member is IPropertySymbol property)
                {
                    if ((property.GetMethod == null || property.GetMethod.DeclaredAccessibility != Accessibility.Public) &&
                        (property.SetMethod == null || property.SetMethod.DeclaredAccessibility != Accessibility.Public))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
            }

            string memberSignature = GetMemberSignature(member);
            if (!processedMembers.Add(memberSignature))
            {
                continue;
            }

            if (member is IPropertySymbol property2)
            {
                string typeWithGlobal = GetGlobalType(semanticModel, property2.Type, className);
                bool hasPublicGetter = property2.GetMethod?.DeclaredAccessibility == Accessibility.Public;
                bool hasPublicSetter = property2.SetMethod?.DeclaredAccessibility == Accessibility.Public;

                // Only generate get/set accessors that are public in the implementation
                string accessors = "";
                if (hasPublicGetter)
                {
                    accessors += "get; ";
                }

                if (hasPublicSetter)
                {
                    accessors += "set; ";
                }

                sourceBuilder.AppendLine($"    {typeWithGlobal} {property2.Name} {{ {accessors}}}");
            }
            else if (member is IMethodSymbol method)
            {
                string returnType = GetGlobalType(semanticModel, method.ReturnType, className);
                string parameters = string.Join(", ",
                    method.Parameters.Select(p =>
                    {
                        string paramType = GetGlobalType(semanticModel, p.Type, className);
                        string paramName = GetSafeParameterName(p.Name);

                        // Get attributes for the parameter
                        ImmutableArray<AttributeData> attributes = p.GetAttributes();

                        // Check for MaybeNullWhen attribute
                        AttributeData? maybeNullWhenAttr = attributes.FirstOrDefault(a =>
                            a.AttributeClass?.Name is "MaybeNullWhenAttribute" or "MaybeNullWhen");

                        // Build attribute string if MaybeNullWhen is present
                        string attributeStr = "";
                        if (maybeNullWhenAttr != null)
                        {
                            object? value = maybeNullWhenAttr.ConstructorArguments.FirstOrDefault().Value;
                            string valueStr = value?.ToString().ToLower() ?? "false";
                            attributeStr = $"[global::System.Diagnostics.CodeAnalysis.MaybeNullWhen({valueStr})] ";
                        }


                        // check if the parameter has a default value
                        string defaultValue = p.HasExplicitDefaultValue
                            ? $" = {p.ExplicitDefaultValue}"
                            : "";

                        // check if the parameter is a ref, out or in parameter
                        string modifiers = p.RefKind switch
                        {
                            RefKind.Out => "out",
                            RefKind.Ref => "ref",
                            RefKind.In  => "in",
                            _           => ""
                        };

                        return $"{attributeStr}{modifiers} {paramType} {paramName}{defaultValue}".Trim();
                    }));
                sourceBuilder.AppendLine($"    {returnType} {method.Name}({parameters});");
            }
        }

        sourceBuilder.AppendLine("}");
    }

    private static string GetSafeParameterName(string paramName)
        =>
            // List of C# keywords that need to be escaped
            Keywords.Contains(paramName)
                ? "@" + paramName
                : paramName;

    private static string GetGlobalType(SemanticModel semanticModel, ITypeSymbol typeSymbol, string className)
    {
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return $"{GetGlobalType(semanticModel, arrayType.ElementType, className)}[]";
        }


        // Handle generic types
        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            string typeArgs = string.Join(", ", genericType.TypeArguments.Select(t => GetGlobalType(semanticModel, t, className)));

            // Handle nullable types separately
            if (genericType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            {
                return $"{typeArgs}?";
            }

            // For other generic types, include full namespace
            string baseName = genericType.ConstructedFrom.ToDisplayString(new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.None));

            if (typeArgs.Contains("ISyncEvent") && className == "ArenaEntityBase")
            {
                typeArgs = "sliced.Entities.ArenaEntityBase.ISyncEvent";
            }

            return $"global::{baseName}<{typeArgs}>";
        }

        // Handle nested types by building the full type path
        if (typeSymbol.ContainingType != null)
        {
            List<string> typePathParts = [];
            INamedTypeSymbol? currentType = typeSymbol.ContainingType;

            // Add the type name itself
            typePathParts.Add(typeSymbol.Name);

            // Build path through containing types
            while (currentType != null)
            {
                typePathParts.Add(currentType.Name);
                currentType = currentType.ContainingType;
            }

            // Add namespace if it exists
            string namespacePart = typeSymbol.ContainingNamespace?.IsGlobalNamespace == false
                ? $"global::{typeSymbol.ContainingNamespace}"
                : "global";

            // Reverse the type path and combine everything
            typePathParts.Reverse();
            string typePath = string.Join(".", typePathParts);

            return $"{namespacePart}.{typePath}";
        }

        // Handle basic types
        string globalType = typeSymbol.SpecialType switch
        {
            SpecialType.System_Boolean => "bool",
            SpecialType.System_String  => "string",
            SpecialType.System_Int32   => "int",
            SpecialType.System_Single  => "float",
            SpecialType.System_Double  => "double",
            SpecialType.System_Void    => "void",
            SpecialType.System_Object  => "object",
            SpecialType.System_UInt64  => "ulong",
            _                          => $"global::{typeSymbol.ContainingNamespace}.{typeSymbol.Name}"
        };

        // Handle nullable types
        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            globalType += "?";
        }

        return globalType;
    }

    private static string GetMemberSignature(ISymbol member)
    {
        if (member is IMethodSymbol method)
        {
            string parameters = string.Join(",", method.Parameters.Select(p => p.Type.ToString()));
            return $"{method.Name}({parameters})";
        }

        return member.Name;
    }

    private static string GetNamespace(TypeDeclarationSyntax typeDeclaration)
    {
        string namespaceName = string.Empty;
        SyntaxNode? potentialNamespaceParent = typeDeclaration.Parent;

        while (potentialNamespaceParent != null                           &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax &&
               potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            namespaceName = namespaceParent.Name.ToString();
        }

        return namespaceName;
    }

    private static TypeDeclarationSyntax? GetNestedType(TypeDeclarationSyntax parentType, string nestedTypeName)
    {
        return parentType.Members
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == nestedTypeName);
    }
}

internal class InterfaceSyntaxReceiver : ISyntaxReceiver
{
    public List<TypeDeclarationSyntax> CandidateClasses { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is TypeDeclarationSyntax { AttributeLists.Count: > 0 } typeDeclaration)
        {
            CandidateClasses.Add(typeDeclaration);
        }
    }
}