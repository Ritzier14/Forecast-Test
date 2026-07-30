using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public interface IProjectFileService
{
    ProjectDataset Load(string path);

    void Save(string path, ProjectDataset dataset);

    string CreateBackup(string path);
}

public interface IUserPreferencesService
{
    AppUserPreferences Load();

    void Save(AppUserPreferences preferences);
}
