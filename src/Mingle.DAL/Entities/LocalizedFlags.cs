namespace Mingle.DAL.Entities;

/// <summary>A yes/no per language, stored as three columns (for example <c>machine_hy</c>).</summary>
public sealed class LocalizedFlags
{
    public bool Hy { get; set; }
    public bool Ru { get; set; }
    public bool En { get; set; }
}
