namespace CL.WebLogic.Forms.Models;

/// <summary>How a field is rendered and validated.</summary>
public enum WebFieldType
{
    /// <summary>Single-line text.</summary>
    Text,
    /// <summary>Multi-line text.</summary>
    TextArea,
    /// <summary>E-mail address.</summary>
    Email,
    /// <summary>Password (never re-rendered with its value).</summary>
    Password,
    /// <summary>Number.</summary>
    Number,
    /// <summary>Slider.</summary>
    Range,
    /// <summary>Date.</summary>
    Date,
    /// <summary>Time.</summary>
    Time,
    /// <summary>Local date and time.</summary>
    DateTimeLocal,
    /// <summary>Colour.</summary>
    Color,
    /// <summary>Telephone number.</summary>
    Tel,
    /// <summary>URL.</summary>
    Url,
    /// <summary>Hidden value.</summary>
    Hidden,
    /// <summary>A single checkbox (bool).</summary>
    Checkbox,
    /// <summary>Several checkboxes (multiple values).</summary>
    CheckboxList,
    /// <summary>Radio buttons.</summary>
    Radio,
    /// <summary>Drop-down.</summary>
    Select,
    /// <summary>Multi-select list.</summary>
    MultiSelect,
    /// <summary>Text with server-side suggestions (options provider search).</summary>
    Autocomplete,
    /// <summary>Rich text (HTML, sanitized when bound).</summary>
    RichText,
    /// <summary>File upload.</summary>
    File,
    /// <summary>Repeated group of sub-fields.</summary>
    Repeater
}
