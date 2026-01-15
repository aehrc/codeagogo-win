// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Reflection;

namespace Codeagogo;

/// <summary>
/// Manages extraction of the ecl-editor standalone JS bundle from embedded resources
/// to a temp directory for WebView2 virtual host mapping.
/// </summary>
public static class ECLEditorResourceManager
{
    private static string? _resourceDir;
    private static readonly object Lock = new();

    /// <summary>
    /// Returns the directory path containing the extracted ecl-editor.standalone.js.
    /// Extracts from embedded resources on first call; subsequent calls return the cached path.
    /// </summary>
    public static string GetResourceDirectory()
    {
        if (_resourceDir != null && Directory.Exists(_resourceDir))
            return _resourceDir;

        lock (Lock)
        {
            if (_resourceDir != null && Directory.Exists(_resourceDir))
                return _resourceDir;

            var dir = Path.Combine(Path.GetTempPath(), "Codeagogo", "ecl-editor");
            Directory.CreateDirectory(dir);

            var targetPath = Path.Combine(dir, "ecl-editor.standalone.js");

            // Only extract if not already present or different size
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Codeagogo.ecl-editor.standalone.js");
            if (stream == null)
            {
                Log.Error("ECLEditorResourceManager: embedded resource not found");
                return dir;
            }

            if (!File.Exists(targetPath) || new FileInfo(targetPath).Length != stream.Length)
            {
                using var fs = File.Create(targetPath);
                stream.CopyTo(fs);
                Log.Info($"ECLEditorResourceManager: extracted ecl-editor.standalone.js to {targetPath}");
            }

            _resourceDir = dir;
            return dir;
        }
    }
}
