using Microsoft.Data.Sqlite;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.MasterData;
using Xunit;

namespace UmaParser.Core.Tests.MasterData;

/// <summary>
/// A custom master.mdb path is written to settings.json only after the file loads.
/// Each test has its own temp directory from <see cref="TempSettingsFile"/>.
/// SQLite's connection pool is process-wide, so the class runs in one non-parallel collection.
/// These tests never call <see cref="GameMasterService.Current"/>.
/// </summary>
[Collection("CustomMasterPath")]
public sealed class CustomMasterPathTests
{
    private const int MarkerSkillId = 900001;

    [Fact]
    public void Invalid_browse_leaves_the_saved_path_and_the_loaded_catalog()
    {
        using var file = new TempSettingsFile();
        string goodPath = Path.Combine(file.DirectoryPath, "good-master.mdb");
        string invalidPath = Path.Combine(file.DirectoryPath, "invalid-master.mdb");
        string missingPath = Path.Combine(file.DirectoryPath, "missing-master.mdb");
        WriteMasterDatabase(goodPath, MarkerSkillId, "Kept Skill");
        File.WriteAllText(invalidPath, "this is not a master database");

        var settings = new GameMasterSettings
        {
            CustomMasterDbPath = goodPath,
            WindowX = 15,
            WindowY = 25,
            WindowWidth = 900,
            WindowHeight = 640,
            WindowState = "Normal",
            ColorMode = "Dark",
        };
        settings.Save(file.FilePath);

        var service = NewService(file);
        service.Initialize();
        Assert.Equal(MasterDataSourceKind.LiveDatabase, service.PrimarySource);
        Assert.Equal("Kept Skill", service.Catalog.FormatSkillName(MarkerSkillId));

        GameMasterCatalog catalog = service.Catalog;
        string before = File.ReadAllText(file.FilePath);

        Assert.False(service.TrySetCustomDatabasePath(invalidPath));
        Assert.False(string.IsNullOrEmpty(service.LastError));
        Assert.Same(catalog, service.Catalog);
        Assert.Equal("Kept Skill", service.Catalog.FormatSkillName(MarkerSkillId));
        Assert.Equal(goodPath, service.Settings.CustomMasterDbPath);
        Assert.Equal(goodPath, service.ResolvedDatabasePath);
        Assert.Equal(MasterDataSourceKind.LiveDatabase, service.PrimarySource);
        Assert.Equal(before, File.ReadAllText(file.FilePath));
        Assert.DoesNotContain("invalid-master.mdb", service.GetStatusLine());

        service.Settings.Save(file.FilePath);
        Assert.Equal(goodPath, GameMasterSettings.Load(file.FilePath).CustomMasterDbPath);

        var closing = GameMasterSettings.Load(file.FilePath);
        closing.WindowWidth = 801;
        closing.Save(file.FilePath);

        Assert.False(service.TrySetCustomDatabasePath(missingPath));
        Assert.Contains("File not found", service.LastError);
        Assert.Same(catalog, service.Catalog);
        Assert.Equal(goodPath, service.Settings.CustomMasterDbPath);

        GameMasterSettings saved = GameMasterSettings.Load(file.FilePath);
        Assert.Equal(goodPath, saved.CustomMasterDbPath);
        Assert.Equal(15, saved.WindowX);
        Assert.Equal(801, saved.WindowWidth);
        Assert.Equal("Dark", saved.ColorMode);
        string json = File.ReadAllText(file.FilePath);
        Assert.DoesNotContain("invalid-master.mdb", json);
        Assert.DoesNotContain("missing-master.mdb", json);
    }

    [Fact]
    public void Valid_browse_saves_the_custom_path()
    {
        using var file = new TempSettingsFile();
        string goodPath = Path.Combine(file.DirectoryPath, "good-master.mdb");
        WriteMasterDatabase(goodPath, MarkerSkillId, "Saved Skill");

        var settings = new GameMasterSettings
        {
            WindowX = 15,
            WindowState = "Maximized",
            ColorMode = "Dark",
        };
        settings.Save(file.FilePath);

        var service = NewService(file);
        service.Initialize();
        Assert.Equal(MasterDataSourceKind.Embedded, service.PrimarySource);
        Assert.Null(service.Settings.CustomMasterDbPath);

        Assert.True(service.TrySetCustomDatabasePath(goodPath));
        Assert.Null(service.LastError);
        Assert.Equal(MasterDataSourceKind.LiveDatabase, service.PrimarySource);
        Assert.Equal(goodPath, service.ResolvedDatabasePath);
        Assert.Equal(goodPath, service.Settings.CustomMasterDbPath);
        Assert.Equal("Saved Skill", service.Catalog.FormatSkillName(MarkerSkillId));
        Assert.DoesNotContain("could not be loaded", service.GetStatusLine());

        GameMasterSettings saved = GameMasterSettings.Load(file.FilePath);
        Assert.Equal(goodPath, saved.CustomMasterDbPath);
        Assert.Equal(15, saved.WindowX);
        Assert.Equal("Maximized", saved.WindowState);
        Assert.Equal("Dark", saved.ColorMode);

        var closing = GameMasterSettings.Load(file.FilePath);
        closing.WindowHeight = 700;
        closing.Save(file.FilePath);
        GameMasterSettings afterClose = GameMasterSettings.Load(file.FilePath);
        Assert.Equal(goodPath, afterClose.CustomMasterDbPath);
        Assert.Equal(700, afterClose.WindowHeight);
        Assert.Equal("Dark", afterClose.ColorMode);
    }

    [Fact]
    public void Startup_with_a_corrupt_saved_path_falls_back_to_the_default_database()
    {
        using var file = new TempSettingsFile();
        string badPath = Path.Combine(file.DirectoryPath, "moved-master.mdb");
        string defaultPath = Path.Combine(file.DirectoryPath, "default-master.mdb");
        File.WriteAllText(badPath, "this is not a master database");
        WriteMasterDatabase(defaultPath, MarkerSkillId, "Default Skill");

        var settings = new GameMasterSettings
        {
            CustomMasterDbPath = badPath,
            WindowX = 120,
            WindowY = 40,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowState = "Normal",
            ColorMode = "Dark",
        };
        settings.Save(file.FilePath);

        var service = NewService(file, defaultPath);
        service.Initialize();

        Assert.Equal(MasterDataSourceKind.LiveDatabase, service.PrimarySource);
        Assert.Equal(defaultPath, service.ResolvedDatabasePath);
        Assert.Equal("Default Skill", service.Catalog.FormatSkillName(MarkerSkillId));
        Assert.Null(service.Settings.CustomMasterDbPath);
        string status = service.GetStatusLine();
        Assert.Contains("Saved master database could not be loaded", status);
        Assert.Contains(badPath, status);
        Assert.Contains("Using the default location.", status);
        Assert.Contains("game master DB", status);

        GameMasterSettings saved = GameMasterSettings.Load(file.FilePath);
        Assert.Null(saved.CustomMasterDbPath);
        Assert.Equal(120, saved.WindowX);
        Assert.Equal(40, saved.WindowY);
        Assert.Equal(800, saved.WindowWidth);
        Assert.Equal(600, saved.WindowHeight);
        Assert.Equal("Normal", saved.WindowState);
        Assert.Equal("Dark", saved.ColorMode);
        Assert.DoesNotContain("moved-master.mdb", File.ReadAllText(file.FilePath));

        var again = NewService(file, defaultPath);
        again.Initialize();
        Assert.Null(again.Settings.CustomMasterDbPath);
        Assert.Equal(defaultPath, again.ResolvedDatabasePath);
        Assert.Equal("Default Skill", again.Catalog.FormatSkillName(MarkerSkillId));
        Assert.DoesNotContain("Saved master database could not be loaded", again.GetStatusLine());
        Assert.DoesNotContain(badPath, again.GetStatusLine());
    }

    [Fact]
    public void Startup_with_a_missing_saved_path_falls_back_to_built_in_names()
    {
        using var file = new TempSettingsFile();
        string badPath = Path.Combine(file.DirectoryPath, "gone-master.mdb");
        var settings = new GameMasterSettings
        {
            CustomMasterDbPath = badPath,
            WindowX = 77,
            ColorMode = "Light",
        };
        settings.Save(file.FilePath);

        var service = NewService(file);
        service.Initialize();

        Assert.Equal(MasterDataSourceKind.Embedded, service.PrimarySource);
        Assert.Null(service.Settings.CustomMasterDbPath);
        Assert.True(service.Catalog.SectionCounts[MasterTextCategory.CharaShortName] > 100);
        string status = service.GetStatusLine();
        Assert.Contains("Saved master database could not be loaded", status);
        Assert.Contains(badPath, status);
        Assert.Contains("Using the default location.", status);
        Assert.Contains("built-in names", status);

        GameMasterSettings saved = GameMasterSettings.Load(file.FilePath);
        Assert.Null(saved.CustomMasterDbPath);
        Assert.Equal(77, saved.WindowX);
        Assert.Equal("Light", saved.ColorMode);

        var again = NewService(file);
        again.Initialize();
        Assert.Equal(MasterDataSourceKind.Embedded, again.PrimarySource);
        Assert.DoesNotContain("gone-master.mdb", again.GetStatusLine());
        Assert.DoesNotContain("Saved master database could not be loaded", again.GetStatusLine());
    }

    [Fact]
    public void Load_releases_the_source_file()
    {
        using var file = new TempSettingsFile();
        string dbPath = Path.Combine(file.DirectoryPath, "release-" + Guid.NewGuid().ToString("n") + ".mdb");
        string invalidPath = Path.Combine(file.DirectoryPath, "invalid-" + Guid.NewGuid().ToString("n") + ".mdb");
        WriteMasterDatabase(dbPath, MarkerSkillId, "Released Skill");
        File.WriteAllText(invalidPath, "this is not a master database");

        var settings = new GameMasterSettings { WindowX = 10, ColorMode = "Dark" };
        settings.Save(file.FilePath);
        string[] before = Directory.GetFiles(file.DirectoryPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();

        var service = NewService(file);
        service.Initialize();
        Assert.Equal("Dark", service.Settings.ColorMode);
        Assert.True(service.TrySetCustomDatabasePath(dbPath));
        Assert.Equal("Released Skill", service.Catalog.FormatSkillName(MarkerSkillId));
        Assert.Equal(before, Directory.GetFiles(file.DirectoryPath).OrderBy(path => path, StringComparer.Ordinal).ToArray());
        AssertNoSidecar(dbPath);
        AssertReleasedThenReplaceAndDelete(dbPath);

        Assert.False(service.TrySetCustomDatabasePath(invalidPath));
        Assert.False(string.IsNullOrEmpty(service.LastError));
        Assert.Equal("Dark", service.Settings.ColorMode);
        AssertNoSidecar(invalidPath);
        AssertReleasedThenReplaceAndDelete(invalidPath);
    }

    private static GameMasterService NewService(TempSettingsFile file, string? defaultDbPath = null)
    {
        string defaultPath = defaultDbPath ?? Path.Combine(file.DirectoryPath, "no-default-master.mdb");
        string cachePath = Path.Combine(file.DirectoryPath, "no-cache.json");
        return new GameMasterService(file.FilePath, defaultPath, cachePath);
    }

    private static void WriteMasterDatabase(string path, int skillId, string skillName)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "CREATE TABLE text_data (category INTEGER, [index] INTEGER, text TEXT);");
            Execute(connection, "CREATE TABLE skill_data (id INTEGER, activate_lot INTEGER);");
            Execute(connection, "CREATE TABLE team_stadium_raw_score (id INTEGER, score INTEGER);");
            Execute(connection, "CREATE TABLE race_instance (id INTEGER, race_id INTEGER);");
            Execute(connection, "CREATE TABLE race (id INTEGER, course_set INTEGER);");
            Execute(
                connection,
                """
                CREATE TABLE race_course_set (
                    id INTEGER,
                    race_track_id INTEGER,
                    distance INTEGER,
                    ground INTEGER,
                    turn INTEGER,
                    inout INTEGER);
                """);

            using var insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO text_data (category, [index], text) VALUES ($category, $index, $text);";
            insert.Parameters.AddWithValue("$category", (int)MasterTextCategory.SkillName);
            insert.Parameters.AddWithValue("$index", skillId);
            insert.Parameters.AddWithValue("$text", skillName);
            insert.ExecuteNonQuery();
        }
        finally
        {
            connection.Close();
            SqliteConnection.ClearPool(connection);
            connection.Dispose();
        }
    }

    private static void AssertNoSidecar(string path)
    {
        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-journal"));
        Assert.False(File.Exists(path + "-shm"));
    }

    private static void AssertReleasedThenReplaceAndDelete(string path)
    {
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(exclusive.CanRead);
        }

        AssertNotHeldByThisProcess(path);
        File.WriteAllText(path, "replaced");
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    private static void AssertNotHeldByThisProcess(string path)
    {
        if (!Directory.Exists("/proc/self/fd"))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        foreach (string fd in Directory.EnumerateFiles("/proc/self/fd"))
        {
            FileSystemInfo? target;
            try
            {
                target = File.ResolveLinkTarget(fd, returnFinalTarget: true);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (target != null && string.Equals(target.FullName, fullPath, StringComparison.Ordinal))
            {
                Assert.Fail($"master database is still open: {path}");
            }
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

[CollectionDefinition("CustomMasterPath", DisableParallelization = true)]
public sealed class CustomMasterPathCollection
{
}
