using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Dolly;

[Flags]
public enum ModelFlags
{
    None = 0,
    Record = 1,
    Struct = 2,
    /// <summary>
    /// Can never occur on struct as it can't be inherited
    /// </summary>
    ClonableBase = 4,
    IsSealed = 8,
}

public partial record struct Test
{
}

[DebuggerDisplay("{Namespace}.{Name}")]
public record Model(string Namespace, string Name, string AssemblyName, ModelFlags Flags, EquatableArray<Member> Members, EquatableArray<Member> Constructor)
{
    public bool HasClonableBaseClass => Flags.HasFlag(ModelFlags.ClonableBase);
    public bool IsRecord => Flags.HasFlag(ModelFlags.Record);
    public bool IsStruct => Flags.HasFlag(ModelFlags.Struct);
    public bool IsSealed => Flags.HasFlag(ModelFlags.IsSealed);
    public bool IsStructRecord => IsRecord && IsStruct;

    public string GetMethodModifiers()
    {
        if (HasClonableBaseClass)
        {
            return "override ";
        }
        else if (!HasClonableBaseClass && !IsStruct && !IsSealed)
        {
            return "virtual ";
        }
        else
        {
            return "";
        }
    }

    public string GetModifiers()
    {
        var builder = new StringBuilder();
        builder.Append("partial ");
        if (IsRecord)
        {
            builder.Append("record ");
        }
        if (IsStruct)
        {
            builder.Append("struct ");
        }
        if (!IsRecord && !IsStruct)
        {
            builder.Append("class ");
        };
        return builder.ToString().TrimEnd();
    }

    public static bool TryCreate(INamedTypeSymbol symbol, bool nullabilityEnabled, string assemblyName, [NotNullWhen(true)] out Model? model, [NotNullWhen(false)] out DiagnosticInfo? error)
    {
        model = null;
        error = null;
        if (symbol.IsAbstract)
        {
            error = DiagnosticInfo.Create(Diagnostics.AbstractClassError, symbol, symbol.GetFullName());
            return false;
        }

        var properties = symbol
            .RecursiveFlatten(t => t.BaseType).SelectMany(t => t.GetMembers())
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsImplicitlyDeclared && !p.HasAttribute("CloneIgnoreAttribute", $"{assemblyName}.Dolly") && !p.IsStatic)
            .Select(p => Member.Create(p.Name, p.SetMethod == null, nullabilityEnabled, p.Type, assemblyName))
            .ToArray();
        var fields = symbol
            .RecursiveFlatten(t => t.BaseType).SelectMany(t => t.GetMembers())
            .OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared && !f.HasAttribute("CloneIgnoreAttribute", $"{assemblyName}.Dolly") && !f.IsStatic && !f.IsConst)
            .Select(m => Member.Create(m.Name, m.IsReadOnly, nullabilityEnabled, m.Type, assemblyName))
            .ToArray();

        var members = properties.Concat(fields).ToArray();
        var memberNames = members.Select(m => m.Name).ToArray();
        var requiredInConstructor = members.Where(m => m.IsReadonly).Select(m => m.Name).ToArray();

        var constructor = symbol.Constructors.OrderBy(c => c.Parameters.Length)
            .Where(p => !p.Parameters.Any(p => !memberNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))) //Remove ctor's that have parameters that aren't included in members
            .Where(p => requiredInConstructor.All(rcp => p.Parameters.Any(p => p.Name.Equals(rcp, StringComparison.OrdinalIgnoreCase)))) //Only include ctor's that have the required parameters
            .FirstOrDefault();

        if (constructor == null)
        {
            error = DiagnosticInfo.Create(Diagnostics.NoValidConstructorError, symbol, string.Join(", ", requiredInConstructor), string.Join(", ", memberNames));
            return false;
        }

        var constructorMembers = constructor.Parameters.Select(p => members.Single(m => m.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
        var membersExcludingConstructor = members.Except(constructorMembers).ToArray();
        var flags = Model.GetFlags(symbol, assemblyName);
        model = new Model(symbol.GetNamespace(), symbol.Name, assemblyName, flags, membersExcludingConstructor, constructorMembers);
        return true;
    }

    private static ModelFlags GetFlags(INamedTypeSymbol namedTypeSymbol, string assemblyName)
    {
        var flags = ModelFlags.None;
        if (namedTypeSymbol.IsRecord)
        {
            flags |= ModelFlags.Record;
        }

        if (namedTypeSymbol.IsValueType)
        {
            flags |= ModelFlags.Struct;
        }

        if (namedTypeSymbol.RecursiveFlatten(t => t.BaseType).Skip(1).Any(t => t.IsClonable(assemblyName)))
        {
            flags |= ModelFlags.ClonableBase;
        }

        if (namedTypeSymbol.IsSealed)
        {
            flags |= ModelFlags.IsSealed;
        }

        return flags;
    }
}
