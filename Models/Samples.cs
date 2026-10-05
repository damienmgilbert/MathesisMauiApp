namespace MathesisMauiApp.Models;

/// <summary>One line of a table that compares an input with the verdict of an attribute.</summary>
public sealed record VerdictRow(string Input, string Kind, string Verdict, bool IsValid)
{
    public bool IsInvalid => !IsValid;
}

/// <summary>A labelled value: what the library read, measured or computed.</summary>
public sealed record Fact(string Label, string Value, string? Note = null)
{
    public bool HasNote => !string.IsNullOrEmpty(Note);
}

/// <summary>A named input for a "try this" chip.</summary>
public sealed record Sample(string Label, string Text);
