using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System.Text;

namespace cloud.api.Services
{
    public static class VirtualPath
    {
        public static bool TryNormalizePath(string? path, out string normalizedPath) //All AI btw
        {
            normalizedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // FormC (composed) is the usual choice for storing and comparing paths
            string normalized = path.Trim().Normalize(NormalizationForm.FormC);

            // Must be absolute
            if (normalized[0] != '/')
            {
                return false;
            }

            // No backslashes anywhere
            if (normalized.Contains('\\'))
            {
                return false;
            }

            // Root is valid on its own
            if (normalized == "/")
            {
                normalizedPath = "/";
                return true;
            }

            // Any other path must not end with '/'
            if (normalized[^1] == '/')
            {
                return false;
            }

            // Check each segment as a whole
            foreach (string segment in normalized.Substring(1).Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    return false;
                }
            }

            normalizedPath = normalized;
            return true;
        }
        public static (string parent, string name) SplitPath(string path)
        {
            int lastSlash = path.LastIndexOf('/');

            string name = path.Substring(lastSlash + 1);
            string parent = lastSlash == 0 ? "/" : path.Substring(0, lastSlash);

            return (parent, name);
        }
    }
}
