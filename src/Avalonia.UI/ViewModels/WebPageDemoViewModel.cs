using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;

using AC = Avalonia.Controls;
using AP = Avalonia.Platform;

namespace Avalonia.UI.ViewModels;

public partial class WebPageDemoViewModel : ObservableObject
{

    private async void NativeWebView_OnNavigationCompleted(object? sender, AC.WebViewNavigationCompletedEventArgs e)
    {
       

        var webView = (NativeWebView)sender!;
        await InvokeTestScript(webView, """ invokeCSharpAction({'key': 10}) """);

        await InvokeTestScript(webView, "1+1");
        await InvokeTestScript(webView, "'test'");
        await InvokeTestScript(webView, "var x = 123; x");
        await InvokeTestScript(webView, "var x = 'test'; x");
        await InvokeTestScript(webView, "'te\"st'");
        await InvokeTestScript(webView, "'te()st'");
        await InvokeTestScript(webView, "document.body.innerHTML");
        await InvokeTestScript(webView, "true");
        try
        {
            await InvokeTestScript(webView, "throw new Error('Hello there')");
        }
        catch (Exception ex)
        {
            
        }

      
    }

    private async Task InvokeTestScript(NativeWebView webView, string script)
    {
        try
        {
            var result = await webView.InvokeScript(script);
            
        }
        catch (Exception ex)
        {
            
        }
    }

    

}
