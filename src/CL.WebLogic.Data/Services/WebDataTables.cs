using CL.Data.Mapping;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Data.Services;

/// <summary>Tables to create or update at startup on one connection (collected by <see cref="WebDataSchemaService"/>).</summary>
/// <param name="ConnectionName">The CL.Data connection name.</param>
/// <param name="Maps">Resolves the entity maps of the row types (the CL.Data generator registers them).</param>
public sealed record WebDataTables(string ConnectionName, Func<IReadOnlyList<Result<EntityMap>>> Maps)
{
    /// <summary>The map of row type <typeparamref name="T"/>.</summary>
    public static Result<EntityMap> Map<T>()
    {
        var accessor = EntityAccessorRegistry.Get<T>();
        return accessor.IsSuccess ? Result<EntityMap>.Success(accessor.Value!.Map) : Result<EntityMap>.Failure(accessor.Error!);
    }
}
