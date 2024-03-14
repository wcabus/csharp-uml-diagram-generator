// Let's build a dotnet tool that is able to generate different types of UML diagrams for a C# project.
// First use case: generate a class diagram for a given C# project.

// Parse the command line to find out which project we need to analyze

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

if (args.Length != 1)
{
    Console.WriteLine("Please provide the path to a .sln or .csproj file as the argument.");
    return;
}

var projectFilePath = args[0];

if (!File.Exists(projectFilePath))
{
    Console.ForegroundColor = ConsoleColor.DarkRed;
    Console.WriteLine("The specified file does not exist.");
    Console.ResetColor();
    
    return;
}

try
{
    using var workspace = MSBuildWorkspace.Create();
    var project = await workspace.OpenProjectAsync(projectFilePath);

    var compilationResult = await project.GetCompilationAsync();
    if (compilationResult is null)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine($"Failed to compile \"{projectFilePath}\".");
        Console.ResetColor();
        return;
    }

    // Using the Roslyn API to analyze the project
    var classDiagram = GenerateClassDiagram(compilationResult, []);
    Console.WriteLine(classDiagram);

    Console.WriteLine("Class diagram generated successfully.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.DarkRed;
    Console.WriteLine($"An error occurred: {ex.Message}");
    Console.ResetColor();
}

static string GenerateClassDiagram(Compilation compilation, ReadOnlySpan<string> filters)
{
    var classDiagram = new StringBuilder();

    // Extract interfaces
    var interfaces = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
        .Where(symbol => symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Interface)
        .OfType<INamedTypeSymbol>()
        .ToList();

    // Extract classes
    var classes = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
        .Where(symbol => symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Class)
        .OfType<INamedTypeSymbol>()
        .ToList();

    // Extract enums
    var enums = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
        .Where(symbol => symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Enum)
        .OfType<INamedTypeSymbol>()
        .ToList();

    // Generate class diagram
    var indentation = 0;
    classDiagram.AppendLine("classDiagram");

    indentation++;
    foreach (var @interface in interfaces)
    {
        classDiagram.AppendLine($"{Indent(indentation)}class {@interface.Name} {{")
            .AppendFormat("{0}<<interface>>{1}", Indent(indentation + 1), Environment.NewLine);
        classDiagram.AppendFormat("{0}}}{1}", Indent(indentation), Environment.NewLine);
    }

    foreach (var @class in classes)
    {
        var properties = @class.GetMembers().Where(x => x.Kind == SymbolKind.Property).ToArray();
        var methods = @class.GetMembers().Where(x => x.Kind == SymbolKind.Method).ToArray();
        if (properties.Length == 0 && methods.Length == 0)
        {
            classDiagram.AppendLine($"{Indent(indentation)}class {@class.Name}");
        }
        else
        {
            classDiagram.AppendLine($"{Indent(indentation)}class {@class.Name} {{");
            indentation++;
            foreach (var property in properties)
            {
                classDiagram.AppendLine($"{Indent(indentation)}{GetSymbolVisibility(property)}{((IPropertySymbol)property).Type.Name} {property.Name}{(property.IsStatic ? "$" : "")}");
            }

            foreach (var method in methods.OfType<IMethodSymbol>())
            {
                if (method.IsImplicitlyDeclared || method.MethodKind == MethodKind.PropertyGet || method.MethodKind == MethodKind.PropertySet)
                {
                    continue;
                }
                classDiagram.AppendLine($"{Indent(indentation)}{GetSymbolVisibility(method)}{method.ReturnType.Name} {method.Name}({GetMethodArguments((IMethodSymbol)method)}){(method.IsStatic ? "$" : "")}");
            }

            indentation--;
            classDiagram.AppendLine($"{Indent(indentation)}}}");
        }
    }

    foreach (var @enum in enums)
    {
        classDiagram.AppendLine($"{Indent(indentation)}class {@enum.Name} {{")
            .AppendFormat("{0}<<enumeration>>{1}", Indent(indentation + 1), Environment.NewLine);
        classDiagram.AppendFormat("{0}}}{1}", Indent(indentation), Environment.NewLine);
    }

    foreach (var @class in classes)
    {
        foreach (var @interface in @class.Interfaces)
        {
            if (!IsSpecialInterface(@interface) && !IsSystemOrMicrosoftInterface(@interface))
            {
                classDiagram.AppendLine($"{Indent(indentation)}{@interface.Name} <|.. {@class.Name}");
            }
        }

        if (@class.BaseType != null && !IsSpecialClass(@class.BaseType) && !IsSystemOrMicrosoftClass(@class.BaseType))
        {
            classDiagram.AppendLine($"{Indent(indentation)}{@class.BaseType.Name} <|-- {@class.Name}");
        }
    }

    return classDiagram.ToString();
}

static string Indent(int count) => new(' ', count * 4);

static string GetMethodArguments(IMethodSymbol method)
{
    foreach (var methodParameter in method.Parameters)
    {
       
    }

    return "";
}

static char GetSymbolVisibility(ISymbol symbol)
{
    return symbol.DeclaredAccessibility switch
    {
        Accessibility.Public => '+',
        Accessibility.Protected => '#',
        Accessibility.ProtectedAndInternal => '#',
        Accessibility.ProtectedOrInternal => '#',
        Accessibility.Private => '-',
        Accessibility.Internal => '~',
        _ => ' '
    };
}

static bool IsSpecialClass(INamedTypeSymbol namedTypeSymbol)
{
    return namedTypeSymbol.SpecialType switch
    {
        SpecialType.System_Object => true,
        SpecialType.System_Delegate => true,
        SpecialType.System_Enum => true,
        SpecialType.System_MulticastDelegate => true,
        SpecialType.System_Nullable_T => true,
        _ => false
    };
}

static bool IsSystemOrMicrosoftClass(INamedTypeSymbol namedTypeSymbol)
{
    return namedTypeSymbol.ContainingNamespace.Name == "System" || namedTypeSymbol.ContainingNamespace.Name == "Microsoft";
}

static bool IsSpecialInterface(INamedTypeSymbol namedTypeSymbol)
{
    return namedTypeSymbol.SpecialType switch
    {
        SpecialType.System_IDisposable => true,
        SpecialType.System_Collections_IEnumerable => true,
        SpecialType.System_Collections_IEnumerator => true,
        SpecialType.System_Collections_Generic_ICollection_T => true,
        SpecialType.System_Collections_Generic_IEnumerable_T => true,
        SpecialType.System_Collections_Generic_IEnumerator_T => true,
        SpecialType.System_Collections_Generic_IList_T => true,
        SpecialType.System_Collections_Generic_IReadOnlyCollection_T => true,
        SpecialType.System_Collections_Generic_IReadOnlyList_T => true,
        _ => false
    };
}

static bool IsSystemOrMicrosoftInterface(INamedTypeSymbol namedTypeSymbol)
{
    return namedTypeSymbol.ContainingNamespace.Name == "System" || namedTypeSymbol.ContainingNamespace.Name == "Microsoft";
}