﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace InterfaceGenerator;

[Generator(LanguageNames.CSharp)]
public class InterfaceIncrementalGenerator : IIncrementalGenerator
{
    private const string Namespace = "Generators";
    private const string GenerateInterfaceAttributeName = "GenerateInterfaceAttribute";
    private const string GenerateIgnoreAttributeName = "GenerateIgnoreAttribute";

    // language=cs
    private const string AttributeSourceCode = $@"// <auto-generated/>
using System;

namespace {Namespace}
{{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class {GenerateInterfaceAttributeName} : System.Attribute
    {{
    }}

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class {GenerateIgnoreAttributeName} : Attribute
    {{
    }}
}}";

    private static readonly ImmutableHashSet<string> Keywords = ImmutableHashSet.Create(
        "event",
        "object",
        "string",
        "class",
        "namespace",
        "new",
        "null",
        "base",
        "this",
        "void",
        "int",
        "long",
        "float",
        "double",
        "decimal",
        "bool",
        "true",
        "false",
        "if",
        "else",
        "while",
        "for",
        "foreach",
        "in",
        "do",
        "switch",
        "case",
        "break",
        "continue",
        "default",
        "goto",
        "return",
        "try",
        "catch",
        "finally",
        "throw",
        "throws",
        "public",
        "private",
        "protected",
        "internal",
        "static",
        "readonly",
        "volatile",
        "virtual",
        "abstract",
        "override",
        "params",
        "ref",
        "out"
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Add the marker attribute to the compilation.
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            $"{GenerateInterfaceAttributeName}.g.cs",
            SourceText.From(AttributeSourceCode, Encoding.UTF8)));

        IncrementalValuesProvider<TypeDeclarationSyntax> typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (s, _) => IsSyntaxTargetForGeneration(s),
                static (ctx, _) => GetTypeForGeneration(ctx))
            .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<TypeDeclarationSyntax> Types)> compilationAndTypes =
            context.CompilationProvider.Combine(typeDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndTypes, static (spc, source) => Execute(source.Compilation, source.Types, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 };

    private static TypeDeclarationSyntax? GetTypeForGeneration(GeneratorSyntaxContext context)
    {
        TypeDeclarationSyntax typeDeclaration = (TypeDeclarationSyntax)context.Node;
        foreach (AttributeListSyntax attributeList in typeDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol attributeSymbol)
                {
                    continue;
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                if (fullName == $"{Namespace}.{GenerateInterfaceAttributeName}")
                {
                    return typeDeclaration;
                }
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<TypeDeclarationSyntax> types, SourceProductionContext context)
    {
        if (types.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (TypeDeclarationSyntax? typeDeclaration in types)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            ISymbol? symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);

            if (symbol is not INamedTypeSymbol classSymbol)
            {
                continue;
            }

            AttributeData? attributeData = classSymbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == GenerateInterfaceAttributeName);

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
                    continue;
                }
            }

            StringBuilder sourceBuilder = new();
            GenerateInterface(typeDeclaration, sourceBuilder, classSymbol, semanticModel);

            string typeName = typeDeclaration.Identifier.Text;
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

        string genericParameters = "";
        if (classSymbol.TypeParameters.Length > 0)
        {
            genericParameters = $"<{string.Join(", ", classSymbol.TypeParameters.Select(tp => tp.Name))}>";
        }

        sourceBuilder.AppendLine($"{accessibility} partial interface I{className}{genericParameters}");

        // Add type parameter constraints if any
        foreach (ITypeParameterSymbol typeParam in classSymbol.TypeParameters)
        {
            string constraints = GetTypeParameterConstraints(typeParam);
            if (!string.IsNullOrEmpty(constraints))
            {
                sourceBuilder.AppendLine($"    {constraints}");
            }
        }

        sourceBuilder.AppendLine("{");

        HashSet<string> processedMembers = [];

        foreach (ISymbol member in classSymbol.GetMembers())
        {
            if (member.GetAttributes().Any(ad => ad.AttributeClass?.Name == "GenerateInterfaceAttribute"))
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
                string typeParameters = "";
                string constraints = "";

                if (method.TypeParameters.Length > 0)
                {
                    typeParameters = $"<{string.Join(", ", method.TypeParameters.Select(tp => tp.Name))}>";

                    IEnumerable<string> typeConstraints = method.TypeParameters
                        .Select(tp => GetTypeParameterConstraints(tp))
                        .Where(c => !string.IsNullOrEmpty(c));

                    if (typeConstraints.Any())
                    {
                        constraints = " " + string.Join(" ", typeConstraints);
                    }
                }

                string returnType = GetGlobalType(semanticModel, method.ReturnType, className);
                string parameters = string.Join(", ",
                    method.Parameters.Select(p =>
                    {
                        string paramType = GetGlobalType(semanticModel, p.Type, className);
                        string paramName = GetSafeParameterName(p.Name);

                        ImmutableArray<AttributeData> attributes = p.GetAttributes();
                        AttributeData? maybeNullWhenAttr = attributes.FirstOrDefault(a =>
                            a.AttributeClass?.Name is "MaybeNullWhenAttribute" or "MaybeNullWhen");

                        string attributeStr = "";
                        if (maybeNullWhenAttr != null)
                        {
                            object? value = maybeNullWhenAttr.ConstructorArguments.FirstOrDefault().Value;
                            string valueStr = value?.ToString().ToLower() ?? "false";
                            attributeStr = $"[global::System.Diagnostics.CodeAnalysis.MaybeNullWhen({valueStr})] ";
                        }

                        string defaultValue = p.HasExplicitDefaultValue
                            ? $" = {p.ExplicitDefaultValue}"
                            : "";

                        string modifiers = p.RefKind switch
                        {
                            RefKind.Out => "out",
                            RefKind.Ref => "ref",
                            RefKind.In  => "in",
                            _           => ""
                        };

                        return $"{attributeStr}{modifiers} {paramType} {paramName}{defaultValue}".Trim();
                    }));

                sourceBuilder.AppendLine($"    {returnType} {method.Name}{typeParameters}({parameters}){constraints};");
            }
        }

        sourceBuilder.AppendLine("}");
    }

    private static string GetTypeParameterConstraints(ITypeParameterSymbol typeParameter)
    {
        List<string> constraints = [];

        if (typeParameter.HasReferenceTypeConstraint)
        {
            constraints.Add("class");
        }
        else if (typeParameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }

        if (typeParameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        if (typeParameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }

        foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
        {
            constraints.Add(constraintType.ToDisplayString());
        }

        if (typeParameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        if (constraints.Count == 0)
        {
            return "";
        }

        return $"where {typeParameter.Name} : {string.Join(", ", constraints)}";
    }

    private static string GetSafeParameterName(string paramName)
        => Keywords.Contains(paramName)
            ? "@" + paramName
            : paramName;

    private static string GetGlobalType(SemanticModel semanticModel, ITypeSymbol typeSymbol, string className)
    {
        if (typeSymbol is ITypeParameterSymbol typeParameter)
        {
            return typeParameter.Name;
        }

        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            string elementType = GetGlobalType(semanticModel, arrayType.ElementType, className);
            if (arrayType.Rank == 1)
            {
                return $"{elementType}[]";
            }

            return $"{elementType}[{new string(',', arrayType.Rank - 1)}]";
        }

        // if (typeSymbol is INamedTypeSymbol { IsTupleType: true } tupleType)
        // {
        //     string tupleElements = string.Join(", ",
        //         tupleType.TupleElements.Select(e =>
        //             GetGlobalType(semanticModel, e.Type, className)));
        //     return $"({tupleElements})";
        // }
        if (typeSymbol is INamedTypeSymbol { IsTupleType: true } tupleType)
        {
            string tupleElements = string.Join(", ",
                tupleType.TupleElements.Select(e =>
                {
                    string elementType = GetGlobalType(semanticModel, e.Type, className);
                    return string.IsNullOrEmpty(e.Name)
                        ? elementType
                        : $"{elementType} {e.Name}";
                }));
            return $"({tupleElements})";
        }

        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            string typeArgs = string.Join(", ", genericType.TypeArguments.Select(t => GetGlobalType(semanticModel, t, className)));

            if (genericType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            {
                return $"{typeArgs}?";
            }

            string baseName = genericType.ConstructedFrom.ToDisplayString(new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.None));

            return $"global::{baseName}<{typeArgs}>";
        }

        if (typeSymbol.ContainingType != null)
        {
            List<string> typePathParts = [];
            INamedTypeSymbol? currentType = typeSymbol.ContainingType;

            typePathParts.Add(typeSymbol.Name);

            while (currentType != null)
            {
                typePathParts.Add(currentType.Name);
                currentType = currentType.ContainingType;
            }

            string namespacePart = typeSymbol.ContainingNamespace?.IsGlobalNamespace == false
                ? $"global::{typeSymbol.ContainingNamespace}"
                : "global";

            typePathParts.Reverse();
            string typePath = string.Join(".", typePathParts);

            return $"{namespacePart}.{typePath}";
        }

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

        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
        {
            // Only add ? for reference types and value types that aren't already Nullable<T>
            bool isAlreadyNullable = typeSymbol is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T };
            if (!isAlreadyNullable)
            {
                globalType += "?";
            }
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
}