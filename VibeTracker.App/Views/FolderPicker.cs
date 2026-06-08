using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VibeTracker.App.Views;

/// <summary>
/// Windows 原生文件夹选择器（COM IFileDialog），不依赖 WinForms。
/// </summary>
public static class FolderPicker
{
    public static string? Show(Window owner, string title = "选择文件夹")
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        dialog.SetOptions(
            FOS.PICKFOLDERS | FOS.FORCEFILESYSTEM | FOS.PATHMUSTEXIST);
        dialog.SetTitle(title);

        var hwnd = owner != null
            ? new WindowInteropHelper(owner).Handle
            : IntPtr.Zero;

        if (dialog.Show(hwnd) == 0) // S_OK
        {
            dialog.GetResult(out var shellItem);
            shellItem.GetDisplayName(SIGDN.FILESYSPATH, out var path);
            Marshal.ReleaseComObject(shellItem);
            return path;
        }

        return null;
    }
}

// ═══════ COM 接口定义（精简版） ═══════

[ComImport, Guid("dc1c5a9c-e88a-4dde-a5a1-60f82a20aef7")]
internal class FileOpenDialog { }

[ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    [PreserveSig] int Show(IntPtr parent);
    void SetFileTypes(uint cFileTypes, [In] IntPtr rgFilterSpec);
    void SetFileTypeIndex(uint iFileType);
    void GetFileTypeIndex(out uint piFileType);
    void Advise(IntPtr pfde, out uint pdwCookie);
    void Unadvise(uint dwCookie);
    void SetOptions(FOS fos);
    void GetOptions(out FOS pfos);
    void SetDefaultFolder(IntPtr psi);
    void SetFolder(IntPtr psi);
    void GetFolder(out IntPtr ppShellItem);
    void GetCurrentSelection(out IntPtr ppShellItem);
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
    void GetResult(out IShellItem ppsi);
    void AddPlace(IntPtr psi, int fdap);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
    void Close([MarshalAs(UnmanagedType.Error)] int hr);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter(IntPtr pFilter);
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    uint GetAttributes(uint sfgaoMask);
    int Compare(IntPtr psi, uint hint);
}

internal enum FOS : uint
{
    OVERWRITEPROMPT = 0x00000002,
    STRICTFILETYPES = 0x00000004,
    NOCHANGEDIR = 0x00000008,
    PICKFOLDERS = 0x00000020,
    FORCEFILESYSTEM = 0x00000040,
    ALLNONSTORAGEITEMS = 0x00000080,
    NOVALIDATE = 0x00000100,
    ALLOWMULTISELECT = 0x00000200,
    PATHMUSTEXIST = 0x00000800,
    FILEMUSTEXIST = 0x00001000,
    CREATEPROMPT = 0x00002000,
    SHAREAWARE = 0x00004000,
    NOREADONLYRETURN = 0x00008000,
    NOTESTFILECREATE = 0x00010000,
    HIDEMRUPLACES = 0x00020000,
    HIDEPINNEDPLACES = 0x00040000,
    NODEREFERENCELINKS = 0x00100000,
    DONOTADDTORECENT = 0x02000000,
    FORCESHOWHIDDEN = 0x10000000,
    DEFAULTNOMINIMODE = 0x20000000
}

internal enum SIGDN : uint
{
    NORMALDISPLAY = 0x00000000,
    PARENTRELATIVEPARSING = 0x80018001,
    DESKTOPABSOLUTEPARSING = 0x80028000,
    PARENTRELATIVEEDITING = 0x80031001,
    DESKTOPABSOLUTEEDITING = 0x8004c000,
    FILESYSPATH = 0x80058000,
    URL = 0x80068000,
    PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
    PARENTRELATIVE = 0x80080001
}
