namespace PicoDI.Gen;

internal static class TypeNameDisplay
{
    public static string GetSimpleName(string fullName)
    {
        var name = fullName.Replace("global::", "");
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name.Substring(lastDot + 1);

        var angleIndex = name.IndexOf('<');
        if (angleIndex >= 0)
            name = name.Substring(0, angleIndex);

        return name;
    }
}
