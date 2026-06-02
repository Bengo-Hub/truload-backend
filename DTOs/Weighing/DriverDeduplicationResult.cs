namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// Summary of a driver de-duplication run: how many duplicate groups were collapsed,
/// how many records were removed, and how many foreign-key references were repointed.
/// </summary>
public class DriverDeduplicationResult
{
    public int GroupsMerged { get; set; }
    public int DriversRemoved { get; set; }
    public int ReferencesRepointed { get; set; }
    public List<string> Details { get; set; } = new();
}
