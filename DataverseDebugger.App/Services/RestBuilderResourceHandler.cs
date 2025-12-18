using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using Microsoft.Web.WebView2.Core;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Handles web resource requests for the embedded DataverseRESTBuilder.
    /// </summary>
    /// <remarks>
    /// Intercepts WebView2 requests for fake web resources (dvdebugger_fakewr_*)
    /// and serves content from the bundled REST Builder zip archive.
    /// </remarks>
    internal sealed class RestBuilderResourceHandler
    {
        /// <summary>The fake web resource name prefix.</summary>
        public const string FakeIdentifier = "dvdebugger_fakewr_";

        private Dictionary<string, byte[]> _webResources = new(StringComparer.OrdinalIgnoreCase);
        private const string LocalSourceFolderName = "DataverseDebugger.RestBuilder";
        private static readonly HashSet<string> LocalResourceExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".htm",
            ".html",
            ".js",
            ".css",
            ".json",
            ".map",
            ".png",
            ".jpg",
            ".jpeg",
            ".svg",
            ".ico",
            ".gif"
        };
        private static readonly string[] LocalResourceIgnoreFolders = { ".git", "node_modules", "dist", "build" };

        /// <summary>
        /// Initializes the handler by loading web resources from the developer folder (when present)
        /// or falling back to bundled zip archives.
        /// </summary>
        public RestBuilderResourceHandler()
        {
            LoadResources();
        }

        public void ReloadResources()
        {
            LoadResources();
        }

        private void LoadResources()
        {
            if (TryLoadLocalResources(out var localResources) && localResources != null)
            {
                _webResources = localResources;
                return;
            }

            var packagedResources = LoadFromZipArchives();
            _webResources = packagedResources;
            LogService.Append($"[RestBuilder] Loaded {packagedResources.Count} resources from packaged extensions.");
        }

        private Dictionary<string, byte[]> LoadFromZipArchives()
        {
            var resources = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var extensionsDir = Path.Combine(baseDir, "extensions");
            if (!Directory.Exists(extensionsDir))
            {
                LogService.Append("[RestBuilder] extensions folder missing; no resources loaded.");
                return resources;
            }

            foreach (var zipFile in Directory.GetFiles(extensionsDir, "*.zip"))
            {
                try
                {
                    LoadWebResources(zipFile, resources);
                }
                catch
                {
                    // best effort
                }
            }

            return resources;
        }

        private bool TryLoadLocalResources(out Dictionary<string, byte[]>? resources)
        {
            resources = null;
            try
            {
                var folder = FindLocalSourceFolder();
                if (string.IsNullOrWhiteSpace(folder))
                {
                    return false;
                }

                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Select(file => new
                    {
                        FullPath = file,
                        RelativePath = Path.GetRelativePath(folder, file).Replace('\\', '/')
                    })
                    .Where(info => ShouldIncludeLocalFile(info.RelativePath))
                    .ToList();

                if (files.Count == 0)
                {
                    return false;
                }

                var loadedResources = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    try
                    {
                        var resourceName = NormalizeWebResourceName($"gp_/drb/{file.RelativePath}");
                        loadedResources[resourceName] = File.ReadAllBytes(file.FullPath);
                    }
                    catch
                    {
                        // ignore malformed files
                    }
                }

                if (loadedResources.Count == 0)
                {
                    return false;
                }

                LogService.Append($"[RestBuilder] Loaded {loadedResources.Count} resources from {folder}.");
                resources = loadedResources;
                return true;
            }
            catch (Exception ex)
            {
                LogService.Append($"[RestBuilder] Local resource load failed: {ex.Message}");
                return false;
            }
        }

        private static string? FindLocalSourceFolder()
        {
            try
            {
                var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (var i = 0; i < 6 && current != null; i++)
                {
                    var candidate = Path.Combine(current.FullName, LocalSourceFolderName);
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    current = current.Parent;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static bool ShouldIncludeLocalFile(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var extension = Path.GetExtension(relativePath);
            if (string.IsNullOrWhiteSpace(extension) || !LocalResourceExtensions.Contains(extension))
            {
                return false;
            }

            var firstSeparator = relativePath.IndexOf('/') >= 0 ? relativePath.IndexOf('/') : relativePath.Length;
            var firstSegment = relativePath.Substring(0, firstSeparator);
            if (LocalResourceIgnoreFolders.Any(ignore => firstSegment.Equals(ignore, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        public bool TryHandle(CoreWebView2WebResourceRequestedEventArgs e, CoreWebView2Environment environment)
        {
            if (e?.Request?.Uri == null)
            {
                return false;
            }

            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.AbsolutePath.EndsWith("/main.aspx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (uri.AbsolutePath.IndexOf("/webresources/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var path = uri.AbsolutePath;
            var index = path.IndexOf(FakeIdentifier, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
            {
                return false;
            }

            var wrName = path.Substring(index + FakeIdentifier.Length);
            wrName = NormalizeWebResourceName(wrName);

            if (_webResources.TryGetValue(wrName, out var content))
            {
                var headers = $"Content-Type: {GetContentType(wrName)}\r\n";
                if (IsRestBuilderEntryPoint(wrName) && IsHtml(wrName))
                {
                    try
                    {
                        var injected = TryInjectBaseHref(content, e.Request.Uri);
                        if (injected != null)
                        {
                            content = injected;
                        }
                    }
                    catch
                    {
                        // best effort
                    }
                }

                e.Response = environment.CreateWebResourceResponse(new MemoryStream(content, writable: false), 200, "OK", headers);
                return true;
            }

            return false;
        }

        private static void LoadWebResources(string zipFile, Dictionary<string, byte[]> target)
        {
            using var archive = ZipFile.OpenRead(zipFile);
            if (TryLoadCrmSolutionArchive(archive, target))
            {
                return;
            }

            LoadFlatArchive(archive, target);
        }

        private static bool TryLoadCrmSolutionArchive(ZipArchive archive, Dictionary<string, byte[]> target)
        {
            var customisations = archive.Entries.FirstOrDefault(e => e.FullName.Equals("customizations.xml", StringComparison.OrdinalIgnoreCase));
            if (customisations == null)
            {
                return false;
            }

            using var stream = customisations.Open();
            LoadCrmWebResources(archive, stream, target);
            return true;
        }

        private static void LoadCrmWebResources(ZipArchive archive, Stream custosStream, Dictionary<string, byte[]> target)
        {
            var mgr = new XmlNamespaceManager(new NameTable());
            mgr.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(custosStream);
            var webresourceNodes = xmlDocument.SelectNodes("//WebResource", mgr);
            if (webresourceNodes == null)
            {
                return;
            }

            foreach (XmlNode webresourceNode in webresourceNodes)
            {
                var nameNode = webresourceNode.SelectSingleNode("Name");
                var fileNode = webresourceNode.SelectSingleNode("FileName");
                if (nameNode == null || fileNode == null)
                {
                    continue;
                }

                var name = nameNode.InnerText;
                var fileName = fileNode.InnerText;
                if (fileName.StartsWith("/", StringComparison.Ordinal))
                {
                    fileName = fileName.Substring(1);
                }

                var entry = archive.GetEntry(fileName);
                if (entry == null)
                {
                    continue;
                }

                using var fileStream = entry.Open();
                using var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                target[NormalizeWebResourceName(name)] = memoryStream.ToArray();
            }
        }

        private static void LoadFlatArchive(ZipArchive archive, Dictionary<string, byte[]> target)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.FullName))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var relativePath = entry.FullName.Replace("\\", "/");
                if (!ShouldIncludeLocalFile(relativePath))
                {
                    continue;
                }

                try
                {
                    using var fileStream = entry.Open();
                    using var memoryStream = new MemoryStream();
                    fileStream.CopyTo(memoryStream);
                    var resourceName = NormalizeWebResourceName($"gp_/drb/{relativePath}");
                    target[resourceName] = memoryStream.ToArray();
                }
                catch
                {
                    // ignore malformed entries
                }
            }
        }

        private static bool IsRestBuilderEntryPoint(string wrName)
        {
            return string.Equals(wrName, "gp_/drb/drb_index.htm", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(wrName, "gp_/drb/drb_index.html", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHtml(string wrName)
        {
            var ext = Path.GetExtension(wrName);
            return string.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase);
        }

        private static byte[]? TryInjectBaseHref(byte[] htmlBytes, string requestUri)
        {
            if (htmlBytes == null || htmlBytes.Length == 0 || string.IsNullOrEmpty(requestUri))
            {
                return null;
            }

            var idxWebResources = requestUri.IndexOf("/WebResources/", StringComparison.OrdinalIgnoreCase);
            if (idxWebResources < 0)
            {
                return null;
            }

            var basePrefix = requestUri.Substring(0, idxWebResources + "/WebResources/".Length);
            var baseHref = basePrefix + FakeIdentifier + "/gp_/drb/";

            string html;
            try
            {
                html = System.Text.Encoding.UTF8.GetString(htmlBytes);
            }
            catch
            {
                return null;
            }

            if (html.IndexOf("<base ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            var headIndex = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
            if (headIndex < 0)
            {
                return null;
            }

            var headClose = html.IndexOf(">", headIndex, StringComparison.OrdinalIgnoreCase);
            if (headClose < 0)
            {
                return null;
            }

            var injection =
                "\r\n    <base href=\"" + baseHref + "\">" +
                "\r\n    <script>" +
                "try{if(typeof parent!=='undefined'&&typeof parent.Xrm==='undefined'&&typeof top!=='undefined'&&typeof top.Xrm!=='undefined'){parent.Xrm=top.Xrm;}}catch(e){}" +
                "</script>";
            var updated = html.Insert(headClose + 1, injection);
            return System.Text.Encoding.UTF8.GetBytes(updated);
        }

        private static string NormalizeWebResourceName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var normalized = name.Trim();
            try
            {
                normalized = Uri.UnescapeDataString(normalized);
            }
            catch
            {
                // ignore invalid escape sequences
            }

            while (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            normalized = normalized.Replace('\\', '/');
            return normalized;
        }

        private static string GetContentType(string name)
        {
            var ext = Path.GetExtension(name)?.ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html",
                ".htm" => "text/html",
                ".js" => "application/javascript",
                ".css" => "text/css",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".eot" => "application/vnd.ms-fontobject",
                _ => "application/octet-stream"
            };
        }
    }
}
