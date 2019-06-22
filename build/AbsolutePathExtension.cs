using static Nuke.Common.IO.PathConstruction;

static class AbsolutePathExtension
{
    public static string Quoted(this AbsolutePath path)
    {
        return $"\"{path}\"";
    }
}