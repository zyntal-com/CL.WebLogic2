using System.Reflection;
using System.Runtime.CompilerServices;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Uploads;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// Copies values between a form model and an entity (<c>request.Forms.Mapper</c>, or inject it).
/// Properties pair up by name (case-insensitive) or <see cref="WebFormMapFromAttribute"/>; values
/// are converted safely (see <see cref="ToEntity{TForm, TEntity}(TForm, TEntity)"/>).
/// </summary>
/// <remarks>
/// Form → entity only writes the form's <b>fields</b> (what <see cref="WebFormDefinitionBuilder"/>
/// binds; <see cref="WebIgnoreAttribute"/> properties never), so anything the form does not define
/// stays untouched on the entity. Properties marked <see cref="WebFormMapIgnoreAttribute"/> are
/// skipped in both directions — use it on sensitive entity properties. Read-only and init-only
/// targets and uploaded files are never written. Property maps are cached per type pair in a
/// <see cref="ConditionalWeakTable{TKey,TValue}"/>, so plugin types can unload.
/// </remarks>
public sealed class WebFormMapper
{
    private readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, PropertyPair[]>> _toEntity = [];
    private readonly ConditionalWeakTable<Type, ConditionalWeakTable<Type, PropertyPair[]>> _toForm = [];

    /// <summary>
    /// Copies <paramref name="form"/>'s fields onto <paramref name="entity"/> and returns it. Only
    /// properties the form defines as fields are written; entity properties without a matching
    /// field keep their values. A value that cannot be converted safely (overflow, a lost fraction,
    /// unparsable text, an undefined enum value) leaves the entity property unchanged; a null or
    /// empty value clears it when the entity property allows null.
    /// </summary>
    public TEntity ToEntity<TForm, TEntity>(TForm form, TEntity entity)
        where TForm : class
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(entity);
        Copy(GetMap(_toEntity, form.GetType(), entity.GetType(), toEntity: true), form, entity);
        return entity;
    }

    /// <summary>Creates a <typeparamref name="TEntity"/> from <paramref name="form"/>'s fields.</summary>
    public TEntity ToEntity<TForm, TEntity>(TForm form)
        where TForm : class
        where TEntity : class, new() =>
        ToEntity(form, new TEntity());

    /// <summary>
    /// Copies <paramref name="entity"/>'s values onto <paramref name="form"/> (an edit form) and
    /// returns it. Every writable form property with a matching entity property is filled.
    /// </summary>
    public TForm ToForm<TEntity, TForm>(TEntity entity, TForm form)
        where TEntity : class
        where TForm : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(form);
        Copy(GetMap(_toForm, form.GetType(), entity.GetType(), toEntity: false), entity, form);
        return form;
    }

    /// <summary>Creates a <typeparamref name="TForm"/> filled from <paramref name="entity"/>.</summary>
    public TForm ToForm<TEntity, TForm>(TEntity entity)
        where TEntity : class
        where TForm : class, new() =>
        ToForm(entity, new TForm());

    private static void Copy(PropertyPair[] map, object source, object target)
    {
        foreach (var pair in map)
        {
            var value = pair.Source.GetValue(source);
            if (value is null)
            {
                if (pair.TargetAcceptsNull)
                    pair.Target.SetValue(target, null);
                continue;
            }
            if (!WebFormMapConverter.TryConvert(value, pair.Target.PropertyType, out var converted))
                continue;
            if (converted is null && !pair.TargetAcceptsNull)
                continue;
            pair.Target.SetValue(target, converted);
        }
    }

    private static PropertyPair[] GetMap(ConditionalWeakTable<Type, ConditionalWeakTable<Type, PropertyPair[]>> cache, Type formType, Type entityType, bool toEntity) =>
        cache.GetValue(formType, _ => [])
             .GetValue(entityType, _ => Build(formType, entityType, toEntity));

    private static PropertyPair[] Build(Type formType, Type entityType, bool toEntity)
    {
        // Form → entity: only the form's fields (what binding writes). Entity → form: any writable form property.
        var formProperties = toEntity
            ? WebFormDefinitionBuilder.Build(formType).Fields.Select(f => f.Property).Where(p => p.GetMethod is { IsPublic: true })
            : Properties(formType).Where(IsWritable);
        var entityProperties = Properties(entityType).Where(p => toEntity ? IsWritable(p) : p.GetMethod is { IsPublic: true }).ToList();

        var nullability = new NullabilityInfoContext();
        var pairs = new List<PropertyPair>();
        var used = new HashSet<PropertyInfo>();
        foreach (var formProperty in formProperties.Where(p => !p.IsDefined(typeof(WebFormMapIgnoreAttribute), true)))
        {
            var entityProperty = Match(formProperty, entityProperties);
            if (entityProperty is null || IsUpload(formProperty.PropertyType) || IsUpload(entityProperty.PropertyType))
                continue;
            if (!used.Add(entityProperty))
                throw new InvalidOperationException(
                    $"More than one property of '{formType}' maps to '{entityType}.{entityProperty.Name}'.");
            var target = toEntity ? entityProperty : formProperty;
            pairs.Add(new PropertyPair(toEntity ? formProperty : entityProperty, target, AcceptsNull(target, nullability)));
        }
        return [.. pairs];
    }

    /// <summary>An entity property that names the form property wins; otherwise the name (or the form's alias), exact case first.</summary>
    private static PropertyInfo? Match(PropertyInfo formProperty, List<PropertyInfo> entityProperties)
    {
        var aliased = entityProperties.Where(e => string.Equals(MapFrom(e), formProperty.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (aliased.Count > 0)
            return aliased.FirstOrDefault(e => string.Equals(MapFrom(e), formProperty.Name, StringComparison.Ordinal)) ?? aliased[0];

        var name = MapFrom(formProperty) ?? formProperty.Name;
        var candidates = entityProperties.Where(e => MapFrom(e) is null && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        return candidates.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal)) ?? candidates.FirstOrDefault();
    }

    private static IEnumerable<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && !p.IsDefined(typeof(WebFormMapIgnoreAttribute), true));

    private static string? MapFrom(PropertyInfo property) => property.GetCustomAttribute<WebFormMapFromAttribute>(true)?.Name;

    /// <summary>A public setter that is not <c>init</c>-only.</summary>
    private static bool IsWritable(PropertyInfo property) =>
        property.SetMethod is { IsPublic: true } setter
        && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static bool AcceptsNull(PropertyInfo property, NullabilityInfoContext nullability)
    {
        var type = property.PropertyType;
        if (type.IsValueType)
            return Nullable.GetUnderlyingType(type) is not null;
        return nullability.Create(property).WriteState != NullabilityState.NotNull;
    }

    private static bool IsUpload(Type type) =>
        type == typeof(WebUploadedFile)
        || (WebValueConverter.CollectionElement(type) is { } element && element == typeof(WebUploadedFile));

    private sealed record PropertyPair(PropertyInfo Source, PropertyInfo Target, bool TargetAcceptsNull);
}
