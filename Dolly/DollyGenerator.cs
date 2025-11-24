using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dolly;


/*
 * Todo:
 * X Embed Microsoft.Bcl.HashCode correctly
 * X List, Array, IEnumrable
 * Dictionary
 * X Record
 * X Private setters
 * X Ctor
 * X Inheritance
 * X IgnoreAttribute
 * X Handle null values
 * X Structs
 * CloneConstructorAttribute
 * IClone
 * Move interfaces and attributes to dependency to simplify cross assemlby usage
 * KeyValuePair
 */

[Generator]
public partial class DollyGenerator : IIncrementalGenerator
{
    public static string GetClonableAttribute(string assemblyName) => $$"""
        using System;

        namespace {{assemblyName}}.Dolly
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
            internal class ClonableAttribute : Attribute
            {
            }
        }
        """;

    public static string GetCloneIgnoreAttribute(string assemblyName) => $$"""
        using System;

        namespace {{assemblyName}}.Dolly
        {
            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            internal class CloneIgnoreAttribute : Attribute
            {
            }
        }
        """;

    public static string GetClonableInterface(string assemblyName) => $$"""
        using System;
        namespace {{assemblyName}}.Dolly
        {
            internal interface IClonable<T> : ICloneable
            {
                T DeepClone();
                T ShallowClone();
            }
        }
        """;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Extract assembly name from compilation
        var assemblyNameProvider = context.CompilationProvider
            .Select((compilation, _) => compilation.AssemblyName ?? "App");

        // Register attributes and interface with assembly name
        context.RegisterSourceOutput(assemblyNameProvider, static (context, assemblyName) =>
        {
            context.AddSource($"{assemblyName}.Dolly.ClonableAttribute.g.cs", GetClonableAttribute(assemblyName));
            context.AddSource($"{assemblyName}.Dolly.CloneIgnoreAttribute.g.cs", GetCloneIgnoreAttribute(assemblyName));
            context.AddSource($"{assemblyName}.Dolly.IClonable.g.cs", GetClonableInterface(assemblyName));
        });

        var pipeline = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, cancellationToken) =>
            {
                // Look for types with Clonable attribute
                if (node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Count > 0)
                {
                    return classDecl.AttributeLists.Any(al => al.Attributes.Any(a => 
                        a.Name.ToString() == "Clonable" || a.Name.ToString() == "ClonableAttribute"));
                }
                if (node is StructDeclarationSyntax structDecl && structDecl.AttributeLists.Count > 0)
                {
                    return structDecl.AttributeLists.Any(al => al.Attributes.Any(a => 
                        a.Name.ToString() == "Clonable" || a.Name.ToString() == "ClonableAttribute"));
                }
                if (node is RecordDeclarationSyntax recordDecl && recordDecl.AttributeLists.Count > 0)
                {
                    return recordDecl.AttributeLists.Any(al => al.Attributes.Any(a => 
                        a.Name.ToString() == "Clonable" || a.Name.ToString() == "ClonableAttribute"));
                }
                return false;
            },
            transform: static (context, cancellationToken) =>
            {
                var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node);
                var assemblyName = context.SemanticModel.Compilation.AssemblyName ?? "App";
                
                if (symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    var nullabilityEnabled = context.SemanticModel.GetNullableContext(context.Node.SpanStart).HasFlag(NullableContext.Enabled);
                    if (Model.TryCreate(namedTypeSymbol, nullabilityEnabled, assemblyName, out var model, out var error))
                    {
                        return (Result<Model>?)model;
                    }
                    else
                    {
                        return (Result<Model>?)error;
                    }
                }
                return (Result<Model>?)null;
            })
            .Where(static result => result is not null)
            .Select(static (result, _) => result!);

        context.RegisterSourceOutput(pipeline, static (context, result) =>
        {
            result.Handle(model =>
            {
                var sourceText = SourceTextConverter.ToSourceText(model);
                context.AddSource($"{model.Name}.g.cs", sourceText);
            },
            error =>
            {
                context.ReportDiagnostic(error.ToDiagnostic());
            });
        });
    }
}
