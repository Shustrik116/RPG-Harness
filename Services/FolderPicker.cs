using System.Runtime.InteropServices;

namespace RPG_Harness.Services;

/// <summary>
/// Системный диалог выбора папки (современный диалог оболочки Windows).
/// Вызывается из серверной части Blazor, поэтому окно появляется на том же компьютере,
/// где запущен харнес, и в обычный путь вида D:\RPG — браузер подобный диалог дать не может.
/// </summary>
public static class FolderPicker
{
    /// <summary>Диалог нужен интерактивный рабочий стол Windows; иначе остаётся ручной ввод пути.</summary>
    public static bool Supported =>
        OperatingSystem.IsWindows() && Environment.UserInteractive;

    /// <summary>
    /// Открыть системный пикер папок и дождаться выбора. Возвращает путь либо null —
    /// отмена, недоступность диалога или ошибка (диалог никогда не должен ломать страницу настроек).
    /// </summary>
    public static Task<string?> PickAsync(string? initialPath = null)
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive)
        {
            return Task.FromResult<string?>(null);
        }

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.TrySetResult(ShowDialog(initialPath));
            }
            catch (Exception ex)
            {
                // Любой сбой диалога безопасен для страницы настроек: возвращаем отмену,
                // но оставляем след в консоли, иначе причину не найти.
                Console.Error.WriteLine($"[FolderPicker] системный диалог недоступен: {ex.GetType().Name}: {ex.Message}");
                tcs.TrySetResult(null);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    /// <summary>
    /// Показать диалог. Проверка платформы живёт здесь же: вызов идёт из лямбды потока,
    /// и анализатор не переносит защиту через замыкание.
    /// </summary>
    private static string? ShowDialog(string? initialPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        IFileDialog? dialog = null;
        var folderPtr = IntPtr.Zero;
        try
        {
            // Активация по CLSID: приведение CoClass-класса к интерфейсу компилятор не разрешает.
            var activator = Type.GetTypeFromCLSID(ClsidFileOpenDialog);
            if (activator is null)
            {
                return null;
            }

            dialog = (IFileDialog)Activator.CreateInstance(activator)!;
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FosPickFolders | FosForceFileSystem);
            dialog.SetTitle(Lang.T("Выбор папки", "Choose a folder"));

            if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            {
                var hrInit = SHCreateItemFromParsingName(initialPath, IntPtr.Zero, typeof(IShellItem).GUID, out folderPtr);
                if (hrInit == 0 && folderPtr != IntPtr.Zero)
                {
                    dialog.SetFolder(folderPtr);
                }
            }

            // Отмена возвращает ERROR_CANCELLED — это нормальный исход, не ошибка.
            var hr = dialog.Show(IntPtr.Zero);
            if (hr != 0)
            {
                if (hr != ErrorCancelled)
                {
                    Console.Error.WriteLine($"[FolderPicker] Show вернул 0x{hr:X8}");
                }

                return null;
            }

            if (dialog.GetResult(out var itemPtr) != 0 || itemPtr == IntPtr.Zero)
            {
                return null;
            }

            var item = (IShellItem)Marshal.GetObjectForIUnknown(itemPtr);
            Marshal.Release(itemPtr);
            try
            {
                return item.GetDisplayName(SigdnFilePath, out var path) == 0 ? path : null;
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        catch (COMException ex)
        {
            if (ex.ErrorCode != ErrorCancelled)
            {
                Console.Error.WriteLine($"[FolderPicker] ошибка оболочки: 0x{ex.ErrorCode:X8} {ex.Message}");
            }

            return null;
        }
        finally
        {
            if (folderPtr != IntPtr.Zero)
            {
                Marshal.Release(folderPtr);
            }

            if (dialog is not null)
            {
                Marshal.ReleaseComObject(dialog);
            }
        }
    }

    // Режим «только папки» + только файловая система, чтобы получить обычный путь.
    private const uint FosPickFolders = 0x00000020;
    private const uint FosForceFileSystem = 0x00000040;

    // SIGDN_FILESYSPATH — полный путь вида D:\RPG вместо отображаемого имени «RPG».
    private const uint SigdnFilePath = 0x80058000;

    /// <summary>CLSID_FileOpenDialog — системный диалог открытия (в режиме папок — выбор папки).</summary>
    private static readonly Guid ClsidFileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");

    /// <summary>ERROR_CANCELLED — игрок закрыл диалог крестиком или «Отмена».</summary>
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IntPtr ppv);

    /// <summary>
    /// IFileDialog — общий предок диалогов открытия и сохранения; в режиме FOS_PICKFOLDERS
    /// работает именно как выбор папки. Объявлены все слоты в порядке IDL:
    /// COM-интерфейс зовется по позиции в таблице, пропуск слота сломал бы вызов.
    /// </summary>
    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr hwndParent);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        uint GetFileTypeIndex();
        [PreserveSig] int Advise(IntPtr pfde, out uint pdwCookie);
        [PreserveSig] int Unadvise(uint dwCookie);
        [PreserveSig] int SetOptions(uint fos);
        [PreserveSig] int GetOptions(out uint pfos);
        void SetDefaultFolder(IntPtr psi);
        void SetFolder(IntPtr psi);
        [PreserveSig] int GetFolder(out IntPtr ppsi);
        void GetCurrentSelection(out IntPtr ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        [PreserveSig] int GetResult(out IntPtr ppsi);
        void AddPlace(IntPtr psi, int fdap);
        [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        [PreserveSig] int Close(int hr);
        void SetClientGuid([MarshalAs(UnmanagedType.LPStruct)] Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        [PreserveSig] int GetResults(out IntPtr ppenum);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IntPtr ppsi);
        [PreserveSig] int GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IntPtr psi, uint hint, out int piOrder);
    }
}