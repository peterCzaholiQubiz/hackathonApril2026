namespace PortfolioThermometer.Api.Configuration;

public static class CrmDataPathResolver
{
    public static string? Resolve(
        string? requestedPath,
        string? configuredPath,
        string? configuredRoot,
        string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            return null;

        var resolvedRoot = ResolveAbsolutePath(configuredRoot, contentRootPath);
        var candidate = string.IsNullOrWhiteSpace(requestedPath)
            ? configuredPath
            : requestedPath;

        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        var resolvedPath = ResolveAbsolutePath(candidate, resolvedRoot);
        return IsWithinRoot(resolvedPath, resolvedRoot) ? resolvedPath : null;
    }

    private static string ResolveAbsolutePath(string path, string basePath)
    {
        var normalized = NormalizeSeparators(path);
        return Path.IsPathRooted(normalized)
            ? Path.GetFullPath(normalized)
            : Path.GetFullPath(Path.Combine(basePath, normalized));
    }

    private static bool IsWithinRoot(string candidatePath, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        return relativePath == "."
            || (!relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && relativePath != ".."
                && !Path.IsPathRooted(relativePath));
    }

    private static string NormalizeSeparators(string path)
        => path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
}
