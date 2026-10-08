namespace D47.Llm;

/// <summary>What a tool result says in place of a picture the model cannot read.</summary>
internal static class ImageText
{
    public const string Unreadable = "The model at this endpoint cannot read pictures.";

    public static string WithoutPicture(string content) => $"{content}\n\n{Unreadable}";
}
