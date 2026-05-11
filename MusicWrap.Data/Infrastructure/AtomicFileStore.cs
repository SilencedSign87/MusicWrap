using System.IO;

namespace MusicWrap.Data.Infrastructure
{
    public static class AtomicFileStore
    {
        public static void WriteAllBytes(string targetPath, byte[] data, string? backupPath = null)
        {
            var directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException($"Invalid target path: {targetPath}");
            }

            Directory.CreateDirectory(directory);

            var tmpPath = Path.Combine(directory, $"{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(flushToDisk: true);
                }

                if (File.Exists(targetPath))
                {
                    File.Replace(tmpPath, targetPath, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tmpPath, targetPath);
                }
            }
            catch
            {
                TryDelete(tmpPath);
                throw;
            }

        }
        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
