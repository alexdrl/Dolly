using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dolly;

[Flags]
public enum MemberFlags
{
    None = 0,
    Clonable = 1,
    Enumerable = 2,
    /// <summary>
    /// Can only occur when <see cref="MemberFlags.Enumerable"/> is present
    /// </summary>
    NewCollection = 4,
    MemberNullable = 8,
    /// <summary>
    /// Can only occur when <see cref="MemberFlags.Enumerable"/> is present
    /// </summary>
    ElementNullable = 32,
}

[DebuggerDisplay("{Name}")]
public sealed record Member(string Name, bool IsReadonly, MemberFlags Flags)
{
    public bool IsEnumerable => Flags.HasFlag(MemberFlags.Enumerable);
    public bool IsClonable => Flags.HasFlag(MemberFlags.Clonable);
    public bool IsNewCompatible => Flags.HasFlag(MemberFlags.NewCollection);
    public bool IsMemberNullable => Flags.HasFlag(MemberFlags.MemberNullable);
    public bool IsElementNullable => Flags.HasFlag(MemberFlags.ElementNullable);

    public static Member Create(string name, bool isReadonly, bool nullabilityEnabled, ITypeSymbol symbol, string assemblyName)
    {
        var flags = GetFlags(symbol, nullabilityEnabled, assemblyName);
        return new Member(name, isReadonly, flags);

    }

    private static MemberFlags GetFlags(ITypeSymbol symbol, bool nullabilityEnabled, string assemblyName)
    {
        MemberFlags flags = MemberFlags.None;
        if (symbol.IsNullable(nullabilityEnabled, out symbol))
        {
            flags |= MemberFlags.MemberNullable;
        }

        // Handle Array
        if (symbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            flags |= MemberFlags.Enumerable;
            var elementType = arrayTypeSymbol.ElementType;
            if (elementType.IsNullable(nullabilityEnabled, out elementType))
            {
                flags |= MemberFlags.ElementNullable;
            }
            if (elementType.IsClonable(assemblyName))
            {
                flags |= MemberFlags.Clonable;
            }
        }
        // Handle Enumerable
        else if (symbol is INamedTypeSymbol namedSymbol && namedSymbol.IsGenericIEnumerable())
        {
            flags |= MemberFlags.Enumerable;
            var elementType = namedSymbol.TypeArguments[0];
            if (elementType.IsNullable(nullabilityEnabled, out elementType))
            {
                flags |= MemberFlags.ElementNullable;
            }
            if (elementType.IsClonable(assemblyName))
            {
                flags |= MemberFlags.Clonable;
            }
        }
        // Handle types that implement IEnumerable<T> and take IEnumerable<T> as constructor parameter (ConcurrentQueue<T>, List<T>, ConcurrentStack<T> and LinkedList<T>)
        else if (symbol is INamedTypeSymbol namedSymbol2 &&
            namedSymbol2.TryGetIEnumerableType(out var enumerableType) &&
            namedSymbol2.Constructors.Any(c =>
            c.Parameters.Length == 1 &&
            c.Parameters[0].Type.TryGetIEnumerableType(out var constructorEnumerableType) &&
            SymbolEqualityComparer.Default.Equals(enumerableType, constructorEnumerableType)))
        {
            flags |= MemberFlags.Enumerable;
            flags |= MemberFlags.NewCollection;
            if (enumerableType.IsNullable(nullabilityEnabled, out enumerableType))
            {
                flags |= MemberFlags.ElementNullable;
            }
            if (enumerableType.IsClonable(assemblyName))
            {
                flags |= MemberFlags.Clonable;
            }
        }
        else if (symbol.IsClonable(assemblyName))
        {
            flags |= MemberFlags.Clonable;
        }
        return flags;
    }

    public string ToString(bool deepClone)
    {
        var builder = new StringBuilder();
        // Start building the output
        if (IsNewCompatible)
        {
            // NewCollection logic
            if (IsMemberNullable)
            {
                builder.Append($"{Name} == null ? null : ");
            }
            builder.Append("new (");

            // Add the inner value
            if (!IsEnumerable)
            {
                throw new InvalidOperationException("This case should never happen");
            }

            if (deepClone && IsClonable)
            {
                if (IsElementNullable)
                {
                    builder.Append($"{Name}.Select(item => item?.DeepClone())");
                }
                else
                {
                    builder.Append($"{Name}.Select(item => item.DeepClone())");
                }
            }
            else
            {
                builder.Append(Name);
            }

            builder.Append(')');
        }
        else
        {
            // Regular logic without NewCollection
            if (IsMemberNullable && (IsEnumerable || (IsClonable && deepClone)))
            {
                builder.Append(Name).Append('?');
            }
            else
            {
                builder.Append(Name);
            }

            if (IsEnumerable)
            {
                if (deepClone && IsClonable)
                {
                    if (IsElementNullable)
                    {
                        builder.Append(".Select(item => item?.DeepClone())");
                    }
                    else
                    {
                        builder.Append(".Select(item => item.DeepClone())");
                    }
                }
                builder.Append(".ToArray()");
            }
            else if (IsClonable && deepClone)
            {
                builder.Append(".DeepClone()");
            }
        }
        return builder.ToString();
    }
}