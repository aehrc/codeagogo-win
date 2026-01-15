// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Web;

namespace Codeagogo;

/// <summary>
/// Builds the HTML page hosting the ECL editor web component (Monaco + ecl-editor).
/// The generated HTML is loaded into a WebView2 control via NavigateToString,
/// with ecl-editor.standalone.js served via virtual host mapping.
/// </summary>
public static class ECLEditorHtmlBuilder
{
    /// <summary>
    /// The virtual hostname used for serving local ecl-editor resources via WebView2.
    /// </summary>
    public const string VirtualHost = "ecl-editor.local";

    /// <summary>
    /// Generates the editor HTML string.
    /// </summary>
    /// <param name="value">Initial ECL expression to load in the editor</param>
    /// <param name="fhirServerUrl">FHIR server URL for autocomplete and validation</param>
    /// <param name="darkTheme">Whether to use dark theme</param>
    /// <returns>Complete HTML page string</returns>
    public static string Build(string value = "", string fhirServerUrl = "", bool darkTheme = true)
    {
        var escapedValue = HttpUtility.HtmlEncode(value);
        var theme = darkTheme ? "vs-dark" : "vs";

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <style>
                    * { margin: 0; padding: 0; box-sizing: border-box; }
                    html, body { height: 100%; overflow: hidden; background: transparent; }
                    ecl-editor {
                        display: block;
                        height: 100%;
                        width: 100%;
                    }
                    /* Hide the ecl-editor's internal resize handle — resizing is
                       controlled by the WPF GridSplitter in the parent panel */
                    ecl-editor > div[style*="ns-resize"] {
                        display: none !important;
                    }
                    #loading {
                        padding: 12px;
                        color: #888;
                        font-family: system-ui;
                        font-size: 12px;
                    }
                    #error {
                        display: none;
                        padding: 12px;
                        color: #e74c3c;
                        font-family: system-ui;
                        font-size: 12px;
                    }
                </style>
            </head>
            <body>
                <!-- Monaco AMD loader from CDN -->
                <script src="https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs/loader.js"></script>
                <script>
                    var monacoLoaded = false;
                    require.config({
                        paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs' }
                    });
                    require(['vs/editor/editor.main'], function() {
                        monacoLoaded = true;
                        document.getElementById('loading').style.display = 'none';
                    });

                    // 10-second timeout for Monaco CDN loading
                    setTimeout(function() {
                        if (!monacoLoaded) {
                            document.getElementById('loading').style.display = 'none';
                            document.getElementById('error').style.display = 'block';
                        }
                    }, 10000);
                </script>

                <!-- ECL Editor standalone bundle (served via virtual host) -->
                <script src="https://{{VirtualHost}}/ecl-editor.standalone.js"></script>

                <div id="loading">Loading editor...</div>
                <div id="error">Could not load the editor. Check your internet connection.</div>

                <ecl-editor
                    value="{{escapedValue}}"
                    fhir-server-url="{{fhirServerUrl}}"
                    theme="{{theme}}"
                    height="100%"
                    minimap="false"
                    semantic-validation="true"
                ></ecl-editor>

                <script>
                    // Debounced change forwarding to C# via WebView2 postMessage
                    var changeTimer = null;
                    var editor = document.querySelector('ecl-editor');

                    editor.addEventListener('ecl-change', function(e) {
                        clearTimeout(changeTimer);
                        changeTimer = setTimeout(function() {
                            window.chrome.webview.postMessage({
                                event: 'change',
                                value: e.detail.value
                            });
                        }, 500);
                    });

                    // Hide the ecl-editor's internal resize handle
                    function hideResizeHandle() {
                        var el = editor.querySelector('div[style*="ns-resize"]');
                        if (el) {
                            el.style.display = 'none';
                        } else {
                            setTimeout(hideResizeHandle, 200);
                        }
                    }
                    setTimeout(hideResizeHandle, 500);

                    // Ctrl+Enter to evaluate
                    document.addEventListener('keydown', function(e) {
                        if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                            e.preventDefault();
                            window.chrome.webview.postMessage({
                                event: 'evaluate',
                                value: editor.value
                            });
                        }
                    });
                </script>
            </body>
            </html>
            """;
    }
}
