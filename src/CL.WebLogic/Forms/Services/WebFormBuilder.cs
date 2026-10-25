using System.Linq.Expressions;
using System.Reflection;
using CL.WebLogic.Forms.Models;

namespace CL.WebLogic.Forms.Services;

/// <summary>Fluent configuration of a form definition.</summary>
public sealed class WebFormBuilder<TModel>
{
    internal WebFormBuilder(WebFormDefinition definition) => Definition = definition;

    /// <summary>The definition being configured.</summary>
    public WebFormDefinition Definition { get; }

    /// <summary>Sets the title.</summary>
    public WebFormBuilder<TModel> Title(string title)
    {
        Definition.Title = title;
        return this;
    }

    /// <summary>Sets the submit button text.</summary>
    public WebFormBuilder<TModel> SubmitLabel(string label)
    {
        Definition.SubmitLabel = label;
        return this;
    }

    /// <summary>Turns the honeypot and timing checks off (e.g. for signed-in admin forms).</summary>
    public WebFormBuilder<TModel> WithoutAntiBot()
    {
        Definition.AntiBot = false;
        return this;
    }

    /// <summary>Configures a field.</summary>
    public WebFieldBuilder Field<TValue>(Expression<Func<TModel, TValue>> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var member = (property.Body as MemberExpression ?? (property.Body as UnaryExpression)?.Operand as MemberExpression)?.Member as PropertyInfo
            ?? throw new ArgumentException("Select a property, e.g. x => x.Email.", nameof(property));
        var field = Definition.Field(member.Name) ?? throw new ArgumentException($"'{member.Name}' is not a field of form '{Definition.Id}'.", nameof(property));
        return new WebFieldBuilder(field);
    }
}
