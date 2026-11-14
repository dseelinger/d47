using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using D47.Core.Storage;

namespace D47.App.Panel;

/// <summary>The engineer portraits that came with the build: <c>&lt;id&gt;.face.jpg</c> square, <c>&lt;id&gt;.jpg</c> whole.</summary>
internal static class EngineerPictures
{
    private static readonly Dictionary<int, Bitmap> Faces = [];
    private static string? _folder;

    /// <summary>The file system the pictures are read through, set once at startup.</summary>
    internal static IFileSystem Files { get; set; } = new DiskFileSystem();

    /// <summary>The folder the pictures are read from — <c>AppPaths.ShippedEngineers</c>, set once at startup.</summary>
    internal static string? Folder
    {
        get => _folder;
        set
        {
            _folder = value;
            Faces.Clear();
        }
    }

    /// <summary>The square face, or null when the file is missing or unreadable.</summary>
    internal static Bitmap? Face(int engineerId)
    {
        if (Faces.TryGetValue(engineerId, out var face))
        {
            return face;
        }

        face = Decode(engineerId + ".face.jpg");

        if (face is not null)
        {
            Faces[engineerId] = face;
        }

        return face;
    }

    /// <summary>The whole picture, decoded afresh, or null when the file is missing or unreadable.</summary>
    internal static Bitmap? Whole(int engineerId) => Decode(engineerId + ".jpg");

    private static Bitmap? Decode(string file)
    {
        if (Folder is not { } folder)
        {
            return null;
        }

        try
        {
            if (Files.ReadBytes(Path.Combine(folder, file)) is not { } bytes)
            {
                return null;
            }

            using var stream = new MemoryStream(bytes);

            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
