using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Helpers;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper : IProjectionMapper
{
    private sealed class ProjectionGeneration(long version)
    {
        internal readonly long Version = version;
        internal readonly ConcurrentDictionary<(Type Source, Type Destination, AdaptOptions Options), LambdaExpression> Plans = new();
    }
    private ProjectionGeneration _projectionGeneration = new(TypeConverterRegistry.Version);

    public LambdaExpression GetProjection(Type sourceType, Type destinationType, AdaptOptions options)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destinationType);
        MappingTypeValidation.Validate(sourceType, nameof(sourceType));
        MappingTypeValidation.Validate(destinationType, nameof(destinationType));
        ValidateOptions(options);
        if (options.HasFlag(AdaptOptions.IgnoreNullValues))
            throw new ProjectionException(sourceType, destinationType, "$", "IgnoreNullValues retains constructed destination state and is not supported in a query projection. Express a fallback in MapFrom instead.");
        var generation = System.Threading.Volatile.Read(ref _projectionGeneration);
        var version = TypeConverterRegistry.Version;
        if (generation.Version != version)
        {
            var next = new ProjectionGeneration(version);
            var previous = System.Threading.Interlocked.CompareExchange(ref _projectionGeneration, next, generation);
            generation = ReferenceEquals(previous, generation) ? next : previous;
        }
        return generation.Plans.GetOrAdd((sourceType, destinationType, options), key =>
        {
            var parameter = Expression.Parameter(key.Source, "source");
            var builder = new ProjectionBuilder(this, key.Options);
            return Expression.Lambda(builder.Build(parameter, key.Destination, "$"), parameter);
        });
    }

    private sealed class ProjectionBuilder(Mapper mapper, AdaptOptions options)
    {
        private readonly HashSet<(Type Source, Type Destination)> _active = new();
        private bool IgnoreCase => options.HasFlag(AdaptOptions.IgnoreCase);
        private bool IgnoreUnderscore => options.HasFlag(AdaptOptions.IgnoreUnderscore);

        internal Expression Build(Expression source, Type destination, string path, bool explicitlyResolved = false, bool sourceIsNotNull = false)
        {
            var sourceType = source.Type;
            if (TypeUtils.IsSafelyAssignable(sourceType, destination) &&
                (TypeUtils.IsImmutableAtomic(sourceType) && !mapper.RequiresConfiguredMapping(sourceType, destination)))
                return sourceType == destination ? source : Expression.Convert(source, destination);
            if (TypeConverterRegistry.TryGetEffective(sourceType, destination, out _))
                throw Fail(sourceType, destination, path, "Registered runtime converters cannot be translated. Configure a source expression for this member instead.");
            if (!mapper.HasConfiguredMap(sourceType, destination))
            {
                MethodInfo? conversion;
                try { conversion = UserDefinedConversion.Find(sourceType, destination); }
                catch (NotSupportedException error) { throw Fail(sourceType, destination, path, error.Message); }
                if (conversion != null)
                    throw Fail(sourceType, destination, path, "User-defined conversion operators are runtime-only. Configure explicit provider-translatable member expressions instead.");
            }
            var sourceNullable = Nullable.GetUnderlyingType(sourceType);
            var destinationNullable = Nullable.GetUnderlyingType(destination);
            if (sourceNullable != null)
            {
                // A surrounding NullSubstitute condition owns the null branch. Build only
                // its non-null branch here, without introducing a runtime throw requirement.
                if (sourceIsNotNull)
                    return Build(Expression.Property(source, nameof(Nullable<int>.Value)), destination, path, explicitlyResolved);
                if (destination.IsValueType && destinationNullable == null && mapper._config.NullValueHandling == NullValueHandling.Throw)
                    throw Fail(sourceType, destination, path, "Throw-on-null conversion is runtime-only. Use a nullable destination or an explicit expression.");
                return Expression.Condition(Expression.Property(source, nameof(Nullable<int>.HasValue)),
                    Build(Expression.Property(source, nameof(Nullable<int>.Value)), destination, path, explicitlyResolved), Expression.Default(destination));
            }
            if (destinationNullable != null)
                return Expression.Convert(Build(source, destinationNullable, path, explicitlyResolved, sourceIsNotNull), destination);
            Expression body;
            if ((TypeUtils.IsNumeric(sourceType) || sourceType.IsEnum) && (TypeUtils.IsNumeric(destination) || destination.IsEnum))
            {
                var from = sourceType.IsEnum ? Enum.GetUnderlyingType(sourceType) : sourceType;
                var to = destination.IsEnum ? Enum.GetUnderlyingType(destination) : destination;
                var input = sourceType.IsEnum ? Expression.Convert(source, from) : source;
                // Keep Convert rounding semantics (not a truncating cast) for floating/decimal to integer conversions.
                var conversion = typeof(Convert).GetMethod("To" + to.Name, new[] { from });
                body = conversion == null ? Expression.ConvertChecked(input, to) : Expression.Call(conversion, input);
                return destination.IsEnum ? Expression.Convert(body, destination) : body;
            }
            if (TypeUtils.IsSimple(sourceType) || TypeUtils.IsSimple(destination))
                throw Fail(sourceType, destination, path, "This scalar conversion requires runtime parsing or formatting. Supply a provider-translatable MapFrom expression.");
            if (TypeUtils.IsDictionary(sourceType) || TypeUtils.IsDictionary(destination))
                throw Fail(sourceType, destination, path, "Dictionary mapping is runtime-only.");
            if (!TypeUtils.IsCollection(sourceType) && !TypeUtils.IsCollection(destination) &&
                (sourceType == typeof(object) || sourceType.IsInterface || sourceType.IsAbstract || destination == typeof(object) || destination.IsInterface || destination.IsAbstract))
                throw Fail(sourceType, destination, path, "Mutable object/interface polymorphism requires runtime graph dispatch. Select concrete DTO shapes for query projections.");
            if (!TypeUtils.IsCollection(sourceType) && !TypeUtils.IsCollection(destination) && sourceType.IsGenericType && destination.IsGenericType &&
                _active.Any(pair => pair.Source.IsGenericType && pair.Destination.IsGenericType &&
                    pair.Source.GetGenericTypeDefinition() == sourceType.GetGenericTypeDefinition() &&
                    pair.Destination.GetGenericTypeDefinition() == destination.GetGenericTypeDefinition() && pair != (sourceType, destination)))
                throw Fail(sourceType, destination, path, "Expanding generic recursion cannot be represented by a finite projection. Select bounded DTO shapes.");
            if (!_active.Add((sourceType, destination)))
                throw Fail(sourceType, destination, path, "Recursive object graphs cannot be represented by a finite projection. Select a non-recursive DTO.");
            try
            {
                if (TypeUtils.IsCollection(sourceType) || TypeUtils.IsCollection(destination)) body = BuildCollection(source, destination, path);
                else body = BuildObject(source, destination, path);
            }
            finally { _active.Remove((sourceType, destination)); }
            if (!sourceType.IsValueType && !sourceIsNotNull)
            {
                if (destination.IsValueType && mapper._config.NullValueHandling == NullValueHandling.Throw)
                    throw Fail(sourceType, destination, path, "Throw-on-null conversion is runtime-only.");
                body = Expression.Condition(Expression.Equal(source, Expression.Constant(null, sourceType)), Expression.Default(destination), body);
            }
            return body;
        }

        private Expression BuildCollection(Expression source, Type destination, string path)
        {
            var sourceElement = TypeUtils.GetElementType(source.Type);
            var destinationElement = TypeUtils.GetElementType(destination);
            if (sourceElement == null || destinationElement == null || source.Type.IsArray && source.Type.GetArrayRank() != 1 || destination.IsArray && destination.GetArrayRank() != 1)
                throw Fail(source.Type, destination, path, "Projection requires an unambiguous one-dimensional generic collection shape.");
            var item = Expression.Parameter(sourceElement, "item");
            var selector = Expression.Lambda(Build(item, destinationElement, path + "[]"), item);
            var queryable = typeof(IQueryable<>).MakeGenericType(sourceElement).IsAssignableFrom(source.Type);
            var projected = Expression.Call(queryable ? typeof(Queryable) : typeof(Enumerable), nameof(Enumerable.Select), new[] { sourceElement, destinationElement }, source, queryable ? Expression.Quote(selector) : selector);
            // An IEnumerable Select iterator would retain and observe the original mutable
            // collection. Materialize supported child collections when the containing row is projected.
            if (destination.IsArray) return Expression.Call(typeof(Enumerable), nameof(Enumerable.ToArray), new[] { destinationElement }, projected);
            var list = typeof(List<>).MakeGenericType(destinationElement);
            if (destination.IsAssignableFrom(list))
            {
                var materialized = Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), new[] { destinationElement }, projected);
                return list == destination ? materialized : Expression.Convert(materialized, destination);
            }
            throw Fail(source.Type, destination, path, "Only array, List<T>, IEnumerable<T> and compatible collection interfaces are supported in projections.");
        }

        private Expression BuildObject(Expression source, Type destination, string path)
        {
            if (typeof(IDisposable).IsAssignableFrom(destination) || typeof(IAsyncDisposable).IsAssignableFrom(destination) ||
                typeof(System.Threading.Tasks.Task).IsAssignableFrom(destination) || typeof(Delegate).IsAssignableFrom(destination))
                throw Fail(source.Type, destination, path, "Resource/delegate destinations cannot be automatically projected, including user-defined resource subclasses. Select a plain DTO instead.");
            static bool Framework(Type type) => type.Namespace == "System" || type.Namespace?.StartsWith("System.", StringComparison.Ordinal) == true;
            if (Framework(source.Type) || Framework(destination))
                throw Fail(source.Type, destination, path, "Framework resources and opaque state are not automatically copied. Project an explicit DTO instead.");
            if (destination.IsInterface || destination.IsAbstract)
                throw Fail(source.Type, destination, path, "The projected destination must be concrete.");
            var configuration = mapper._config.Configs.Where(item => item.SourceType == source.Type && item.DestType == destination).OrderBy(item => item.IsReverseGenerated).FirstOrDefault();
            var rules = configuration == null ? null : new MappingExpressionBase(configuration.ExpressionObject);
            if (rules != null && rules.ConfiguredPaths.Concat(rules.IgnoredPaths).Any(name => name.Contains('.')))
                throw Fail(source.Type, destination, path, "Destination-path mutation is runtime-only. Project the child DTO using its own member map or a source expression.");
            if (source.Type == destination && rules?.ConstructionFactory == null)
            {
                for (var current = source.Type; current != null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
                    foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (!field.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) || !field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal))
                            throw Fail(source.Type, destination, path, "Automatic same-type projection cannot preserve field-only or opaque private state. Use a property DTO or an explicit source expression.");
            }
            NewExpression creation;
            var bindings = new Dictionary<string, MemberBinding>(StringComparer.Ordinal);
            var constructorMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (rules?.ConstructionFactory is { } factory)
            {
                var factoryBody = Inline(factory, source, destination, path);
                if (factoryBody is MemberInitExpression initialized)
                {
                    creation = initialized.NewExpression;
                    foreach (var binding in initialized.Bindings)
                    {
                        if (binding is not MemberAssignment) throw Fail(source.Type, destination, path, "ConstructUsing must use simple member assignments in projections.");
                        bindings.Add(binding.Member.Name, binding);
                    }
                }
                else if (factoryBody is NewExpression constructed) creation = constructed;
                else throw Fail(source.Type, destination, path, "ConstructUsing must be a new or member-initializer expression for query projection; runtime factories are unsupported.");
            }
            else if (destination.IsValueType && destination.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property => property.SetMethod?.IsPublic == true) || !destination.IsValueType && destination.GetConstructor(Type.EmptyTypes) is { })
                creation = Expression.New(destination);
            else
                creation = BuildConstructor(source, destination, path, rules, constructorMembers);

            if (source.Type == destination && rules?.ConstructionFactory == null)
            {
                foreach (var property in destination.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    if (property.GetMethod?.IsPublic == true && !property.GetMethod.IsStatic && property.GetIndexParameters().Length == 0 &&
                        property.SetMethod?.IsPublic != true && !constructorMembers.Contains(property.Name) && rules?.IsIgnored(property.Name) != true)
                        throw Fail(source.Type, destination, path + "." + property.Name, "Read-only source state is not constructor-bound and cannot be copied without data loss.");
            }

            foreach (var property in destination.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.SetMethod?.IsPublic != true || property.SetMethod.IsStatic || property.GetIndexParameters().Length != 0 || rules?.IsIgnored(property.Name) == true || constructorMembers.Contains(property.Name)) continue;
                var value = Resolve(source, destination, property.Name, property.PropertyType, path + "." + property.Name, rules);
                if (value != null) bindings[property.Name] = Expression.Bind(property, value);
            }
            return bindings.Count == 0 ? creation : Expression.MemberInit(creation, bindings.Values);
        }

        private NewExpression BuildConstructor(Expression source, Type destination, string path, MappingExpressionBase? rules, HashSet<string> members)
        {
            var candidates = new List<(ConstructorInfo Constructor, string[] Members)>();
            foreach (var constructor in destination.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var names = new List<string>();
                var valid = true;
                foreach (var parameter in constructor.GetParameters())
                {
                    var name = destination.GetProperties().FirstOrDefault(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))?.Name ?? parameter.Name!;
                    names.Add(name);
                    if (rules?.IsIgnored(name) == true || rules?.GetCustomBinding(name) != null || parameter.HasDefaultValue) continue;
                    var renamed = rules?.GetRenamedSource(name);
                    var sourcePath = ResolveSourceMemberPath(source.Type, renamed ?? name, IgnoreCase, IgnoreUnderscore,
                        renamed == null && mapper._config.GlobalOptions.EnableFlattening, mapper._config.GlobalOptions.EnableGetMethods);
                    if (sourcePath == null) { valid = false; break; }
                }
                if (valid) candidates.Add((constructor, names.ToArray()));
            }
            var best = candidates.OrderByDescending(candidate => candidate.Members.Length).ToArray();
            if (best.Length == 0 || best.Length > 1 && best[0].Members.Length == best[1].Members.Length)
                throw Fail(source.Type, destination, path, "Projection requires one unambiguous public constructor whose required parameters can be bound from the source.");
            var selected = best[0];
            var parameters = selected.Constructor.GetParameters();
            var arguments = new Expression[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                var name = selected.Members[index];
                members.Add(name);
                arguments[index] = rules?.IsIgnored(name) == true ? DefaultArgument(parameters[index])
                    : Resolve(source, destination, name, parameters[index].ParameterType, path + "." + name, rules) ?? DefaultArgument(parameters[index]);
            }
            return Expression.New(selected.Constructor, arguments);
        }

        private static Expression DefaultArgument(ParameterInfo parameter)
            => parameter.HasDefaultValue && parameter.DefaultValue != null && parameter.DefaultValue != Missing.Value && parameter.DefaultValue != DBNull.Value
                ? Expression.Constant(parameter.DefaultValue, parameter.ParameterType) : Expression.Default(parameter.ParameterType);

        private Expression? Resolve(Expression source, Type destination, string name, Type memberType, string path, MappingExpressionBase? rules)
        {
            var rule = rules?.GetMemberOptions(name);
            if (rule?.IsFinalConverter == true)
                throw Fail(source.Type, destination, path, "Final member converters own runtime results and cannot be translated. Use a provider-translatable MapFrom expression for query projection.");
            if (rule?.Condition != null || rule?.PreCondition != null || rule?.IgnoreNullValues == true)
                throw Fail(source.Type, destination, path, "Assignment conditions and IgnoreNullValues depend on constructed destination state. Use a conditional MapFrom expression for query projections.");
            var custom = rules?.GetCustomBinding(name);
            Expression? value;
            if (custom != null) value = Inline(custom, source, memberType, path);
            else
            {
                var sourceName = rules?.GetRenamedSource(name) ?? name;
                value = SourcePath(source, sourceName, rules?.GetRenamedSource(name) == null, destination, path);
                if (value == null && rules?.GetRenamedSource(name) != null)
                    throw Fail(source.Type, destination, path, "The configured source path is not publicly readable.");
            }
            if (value == null) return null;
            var substitutesNull = rule?.HasNullSubstitute == true && (!value.Type.IsValueType || Nullable.GetUnderlyingType(value.Type) != null);
            if (substitutesNull && rule!.NullSubstitute is { } replacement && !TypeUtils.IsImmutableAtomic(replacement.GetType()))
                throw Fail(source.Type, destination, path, "Mutable NullSubstitute objects require runtime graph copying and cannot be captured as shared query results. Use an explicit new-object fallback expression instead.");
            var mapped = Build(value, memberType, path, custom != null, substitutesNull);
            if (substitutesNull)
            {
                var substitute = rule!.NullSubstitute == null ? Expression.Default(memberType) : (Expression)Expression.Constant(rule.NullSubstitute, memberType);
                new ProjectionSafetyVisitor(source.Type, memberType, path).Visit(substitute);
                mapped = Expression.Condition(Expression.Equal(value, Expression.Default(value.Type)), substitute, mapped);
            }
            return mapped;
        }

        private Expression? SourcePath(Expression root, string sourceName, bool allowFlattening, Type destination, string destinationPath)
        {
            MemberInfo[]? members;
            try
            {
                members = ResolveSourceMemberPath(root.Type, sourceName, IgnoreCase, IgnoreUnderscore,
                    allowFlattening && mapper._config.GlobalOptions.EnableFlattening, mapper._config.GlobalOptions.EnableGetMethods);
            }
            catch (Exception error) when (error is NotSupportedException or AmbiguousMatchException)
            { throw Fail(root.Type, destination, destinationPath, error.Message); }
            if (members == null) return null;
            Expression Read(Expression instance, int index)
            {
                var member = members[index];
                if (member is MethodInfo) throw Fail(root.Type, destination, destinationPath, "Get-method conventions call arbitrary runtime methods and cannot be projected. Use a property or a provider-translatable source expression.");
                var underlying = Nullable.GetUnderlyingType(instance.Type);
                var owner = underlying != null && member.DeclaringType != instance.Type && member.DeclaringType!.IsAssignableFrom(underlying)
                    ? Expression.Property(instance, nameof(Nullable<int>.Value)) : instance;
                Expression value = Expression.Property(owner, (PropertyInfo)member);
                if (index + 1 < members.Length) value = Read(value, index + 1);
                if (!instance.Type.IsValueType || underlying != null)
                    value = Expression.Condition(Expression.Equal(instance, Expression.Default(instance.Type)), Expression.Default(value.Type), value);
                return value;
            }
            return Read(root, 0);
        }

        private Expression Inline(LambdaExpression expression, Expression source, Type destination, string path)
        {
            if (expression.Parameters.Count != 1)
                throw Fail(source.Type, destination, path, "Per-call context and services are runtime-only and cannot be captured in a query projection.");
            var value = new ParameterSubstitution(expression.Parameters[0], source).Visit(expression.Body)!;
            new ProjectionSafetyVisitor(source.Type, destination, path).Visit(value);
            return value;
        }

        private static ProjectionException Fail(Type source, Type destination, string path, string reason) => new(source, destination, path, reason);
    }

    private sealed class ProjectionSafetyVisitor(Type source, Type destination, string path) : ExpressionVisitor
    {
        private ProjectionException Failure(string reason) => new(source, destination, path, reason);
        public override Expression? Visit(Expression? node)
        {
            if (node != null && (typeof(MappingCallContext).IsAssignableFrom(node.Type) || typeof(IServiceProvider).IsAssignableFrom(node.Type)))
                throw Failure("Per-call context and service resolution are runtime-only.");
            return base.Visit(node);
        }
        protected override Expression VisitInvocation(InvocationExpression node) => throw Failure("Delegate invocation is runtime-only; inline the source expression instead.");
        protected override Expression VisitUnary(UnaryExpression node)
        {
            ValidateOperator(node.Method);
            return base.VisitUnary(node);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            ValidateOperator(node.Method);
            return base.VisitBinary(node);
        }
        private void ValidateOperator(MethodInfo? method)
        {
            if (method != null && !TypeUtils.IsSimple(method.DeclaringType))
                throw Failure("User-defined operators are arbitrary runtime methods and cannot be projected.");
        }
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is Delegate || node.Value is IMapper || node.Value is IServiceProvider || node.Value is MappingCallContext)
                throw Failure("A runtime mapper, delegate, service or context cannot be captured in a query projection.");
            if (node.Value != null && !TypeUtils.IsSimple(node.Type) &&
                !node.Type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) &&
                !(node.Type.IsArray && node.Type.GetArrayRank() == 1 && TypeUtils.IsSimple(node.Type.GetElementType())))
                throw Failure("Captured runtime objects cannot be projected. Capture scalar values or select a source member instead.");
            return base.VisitConstant(node);
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression && !TypeUtils.IsSimple(node.Type) &&
                !(node.Type.IsArray && node.Type.GetArrayRank() == 1 && TypeUtils.IsSimple(node.Type.GetElementType())))
                throw Failure("Captured runtime objects or services cannot be projected. Capture scalar values instead.");
            return base.VisitMember(node);
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var type = node.Method.DeclaringType;
            if (type == typeof(MappingCallContext) || type == typeof(MappingCallScope) || type != null && typeof(IServiceProvider).IsAssignableFrom(type))
                throw Failure("Per-call context and service resolution are runtime-only.");
            if (type != typeof(string) && type != typeof(Math) && type != typeof(MathF) && type != typeof(Convert) && type != typeof(decimal) && type != typeof(DateTime) && type != typeof(DateTimeOffset) && type != typeof(DateOnly) && type != typeof(TimeOnly) && type != typeof(TimeSpan) && type != typeof(Enumerable) && type != typeof(Queryable) && !(type?.IsGenericType == true && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                throw Failure($"Method '{node.Method.Name}' is an arbitrary runtime method. Supply an expression using provider-supported scalar operations instead.");
            return base.VisitMethodCall(node);
        }
    }
}
