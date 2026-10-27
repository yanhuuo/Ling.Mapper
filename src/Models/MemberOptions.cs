using System;
using System.Linq.Expressions;

namespace Ling.Mapper;

/// <summary>Per-destination-member rules. Source selection and destination value types are independent.</summary>
/// <typeparam name="TSource">Source object type.</typeparam>
/// <typeparam name="TDestination">Destination object type.</typeparam>
/// <typeparam name="TMember">Destination member type.</typeparam>
public sealed class MemberOptions<TSource, TDestination, TMember>
{
    internal MemberRule Rule { get; } = new();
    internal LambdaExpression? SourceExpression { get; private set; }
    internal bool IsIgnored { get; private set; }

    internal MemberOptions() { }

    /// <summary>Select a source value of any supported mapping type. Conversion to the destination member follows resolution.</summary>
    public MemberOptions<TSource, TDestination, TMember> MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        SourceExpression = expression;
        return this;
    }

    /// <summary>Resolve a member with the current invocation's parameters/services. Context is supplied at mapping time, never cached in the plan.
    /// Runtime-only: query projection does not support invocation context.</summary>
    public MemberOptions<TSource, TDestination, TMember> MapFrom<TSourceMember>(Expression<Func<TSource, MappingCallContext, TSourceMember>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        SourceExpression = expression;
        return this;
    }

    /// <summary>Convert the selected source member to its final destination value. MapFrom (or convention) is read once.
    /// Null follows NullSubstitute/IgnoreNull/default policy before this converter; a non-null returned mutable instance is not cloned.</summary>
    public MemberOptions<TSource, TDestination, TMember> ConvertUsing<TSourceMember>(Expression<Func<TSourceMember, TMember>> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        Rule.FinalConverter = converter;
        return this;
    }

    /// <summary>Final selected-value conversion with the current call context. The result is not mapped again.</summary>
    public MemberOptions<TSource, TDestination, TMember> ConvertUsing<TSourceMember>(Expression<Func<TSourceMember, MappingCallContext, TMember>> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        Rule.FinalConverter = converter;
        return this;
    }

    /// <summary>Skip the member when false, without evaluating its source getter or converter.</summary>
    public MemberOptions<TSource, TDestination, TMember> PreCondition(Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        Rule.PreCondition = predicate;
        return this;
    }

    /// <summary>Decide whether to assign after resolution and conversion. The final argument is the destination-typed candidate value.
    /// This is not a source-getter guard; use PreCondition to avoid reading a source member.</summary>
    public MemberOptions<TSource, TDestination, TMember> Condition(Expression<Func<TSource, TDestination, TMember, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        Rule.Condition = predicate;
        return this;
    }

    /// <summary>Use this destination-typed value when the selected source is null. Immutable atoms are assigned directly;
    /// mutable substitutes are independently mapped per result. It bypasses the source-typed final converter and wins over IgnoreNullValues.</summary>
    public MemberOptions<TSource, TDestination, TMember> NullSubstitute(TMember value)
    {
        Rule.HasNullSubstitute = true;
        Rule.NullSubstitute = value;
        return this;
    }

    /// <summary>Override the call's IgnoreNullValues setting for this member. False allows assigning null/default values.</summary>
    public MemberOptions<TSource, TDestination, TMember> IgnoreNullValues(bool ignore = true)
    {
        Rule.IgnoreNullValues = ignore;
        return this;
    }

    /// <summary>Ignore this destination member. Ignore wins over all other rules, regardless of registration order.</summary>
    public MemberOptions<TSource, TDestination, TMember> Ignore()
    {
        IsIgnored = true;
        return this;
    }
}

internal sealed class MemberRule
{
    internal LambdaExpression? FinalConverter;
    internal bool IsFinalConverter => FinalConverter != null;
    internal LambdaExpression? PreCondition;
    internal LambdaExpression? Condition;
    internal bool HasNullSubstitute;
    internal object? NullSubstitute;
    internal bool? IgnoreNullValues;
}
