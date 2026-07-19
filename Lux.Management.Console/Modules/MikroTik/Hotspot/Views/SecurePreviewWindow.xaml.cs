using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Lux.Management.Console.Modules.MikroTik.Hotspot.Views
{
    public partial class SecurePreviewWindow : Window
    {
        private readonly Dictionary<string, byte[]> _files;

        public SecurePreviewWindow(Dictionary<string, byte[]> files)
        {
            InitializeComponent();
            _files = files;
            Loaded += SecurePreviewWindow_Loaded;
        }

        private async void SecurePreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();

                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                webView.CoreWebView2.AddWebResourceRequestedFilter("https://preview.local/*", CoreWebView2WebResourceContext.All);
                webView.CoreWebView2.WebResourceRequested += WebResourceRequestedHandler;

                webView.CoreWebView2.Navigate("https://preview.local/ALFA/index.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل بدء المعاينة الآمنة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void WebResourceRequestedHandler(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var uriString = e.Request.Uri;
            if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                var relativePath = uri.AbsolutePath.TrimStart('/');
                if (_files.TryGetValue(relativePath, out var fileBytes))
                {
                    var ext = Path.GetExtension(relativePath).ToLower();
                    var contentType = GetContentType(ext);

                    var stream = new MemoryStream(fileBytes);
                    var response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        stream, 
                        200, 
                        "OK", 
                        $"Content-Type: {contentType}\nAccess-Control-Allow-Origin: *");

                    e.Response = response;
                    return;
                }
            }

            e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 
                404, 
                "Not Found", 
                "Content-Type: text/plain");
        }

        private string GetContentType(string extension)
        {
            return extension switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".woff2" => "font/woff2",
                ".woff" => "font/woff",
                ".ttf" => "font/ttf",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
