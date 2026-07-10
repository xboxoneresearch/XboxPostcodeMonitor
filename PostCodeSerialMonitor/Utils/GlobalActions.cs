using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PostCodeSerialMonitor.Utils;

public static class GlobalActions
{

    public static void OpenHyperlinkAction(string url)
    {
        using var proc = new Process
        {
            StartInfo = {
                UseShellExecute = true,
                FileName = url
            }
        };
        proc.Start();
    }
}