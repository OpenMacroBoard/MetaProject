using static Nuke.Common.IO.PathConstruction;

static class AbsolutePathExtension
{
    public static string ShellEscape(this AbsolutePath path)
    {
        return $"\"{path}\"";
    }
}