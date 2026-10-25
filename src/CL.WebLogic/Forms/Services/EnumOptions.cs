using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CL.WebLogic.Forms.Models;

namespace CL.WebLogic.Forms.Services;

/// <summary>Options of an enum (labels from <see cref="DisplayAttribute"/> or the humanized name).</summary>
public static class EnumOptions
{
    /// <summary>The options of <paramref name="enumType"/>.</summary>
    public static IReadOnlyList<WebFieldOption> For(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        return [.. enumType.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f =>
        {
            var display = f.GetCustomAttribute<DisplayAttribute>();
            return new WebFieldOption(f.Name, display?.GetName() ?? WebFormDefinitionBuilder.Humanize(f.Name));
        })];
    }
}
