using System.Runtime.InteropServices;

namespace FG.Precificador;

public static class DesktopShortcut
{
    public static string Create()
    {
        var executable=Environment.ProcessPath??throw new InvalidOperationException("Não foi possível localizar o programa.");
        var desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if(string.IsNullOrWhiteSpace(desktop))throw new InvalidOperationException("A área de trabalho não foi localizada.");
        var path=Path.Combine(desktop,"FG Precificador 3D.lnk");
        var shellType=Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("O Windows não disponibilizou a criação de atalhos.");
        dynamic shell=Activator.CreateInstance(shellType)!;object? shortcut=null;
        try
        {
            shortcut=shell.CreateShortcut(path);dynamic link=shortcut;
            link.TargetPath=executable;link.WorkingDirectory=Path.GetDirectoryName(executable);link.IconLocation=executable+",0";
            link.Description="FG Precificador 3D - custos e orçamentos de impressão 3D";link.Save();return path;
        }
        finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);Marshal.FinalReleaseComObject(shell);}
    }
}
