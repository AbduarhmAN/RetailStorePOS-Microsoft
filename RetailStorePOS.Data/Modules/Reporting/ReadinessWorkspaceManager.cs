namespace RetailStorePOS.Data.Modules.Reporting;

using RetailStorePOS.Data.Modules.Migrations;

public class ReadinessWorkspaceManager
{
    public void PrepareWorkspace(DatasetProfile profile)
    {
        var targetPath = profile.WorkingDatabasePath;
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(profile.SeedSource))
        {
            File.Copy(profile.SeedSource, targetPath, true);
        }
        else
        {
            // Initialize fresh database with schema if no seed source provided
            DatabaseInitializer.Initialize(targetPath);
        }

        // Prepare import sample directory
        if (!string.IsNullOrEmpty(profile.ImportSampleDirectory))
        {
            Directory.CreateDirectory(profile.ImportSampleDirectory);
        }
    }

    public void CleanupWorkspace(DatasetProfile profile)
    {
        if (File.Exists(profile.WorkingDatabasePath))
        {
            try
            {
                File.Delete(profile.WorkingDatabasePath);
                // Delete associated WAL/SHM files if they exist
                var wal = profile.WorkingDatabasePath + "-wal";
                var shm = profile.WorkingDatabasePath + "-shm";
                if (File.Exists(wal)) File.Delete(wal);
                if (File.Exists(shm)) File.Delete(shm);
            }
            catch (IOException)
            {
                // File might be in use, log or handle if necessary
            }
        }

        if (Directory.Exists(profile.ImportSampleDirectory))
        {
            try
            {
                Directory.Delete(profile.ImportSampleDirectory, true);
            }
            catch (IOException)
            {
                // Directory might be in use
            }
        }
    }
}
