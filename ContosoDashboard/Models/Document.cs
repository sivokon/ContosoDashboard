using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class Document
{
    [Key]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    // Stored as text (not an enum); values come from DocumentCategories
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = DocumentCategories.Other;

    // Comma-separated tags
    [MaxLength(500)]
    public string? Tags { get; set; }

    // Display and download name only; never used to build a storage path
    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    // Relative storage path: {userId}/{projectId or "personal"}/{guid}.{ext}
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    // MIME type; Office types can be long, so 255 characters
    [Required]
    [MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [Required]
    public int UploadedByUserId { get; set; }

    public int? ProjectId { get; set; }

    public int? TaskId { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedDate { get; set; }

    // Navigation properties
    [ForeignKey("UploadedByUserId")]
    public virtual User UploadedBy { get; set; } = null!;

    [ForeignKey("ProjectId")]
    public virtual Project? Project { get; set; }

    [ForeignKey("TaskId")]
    public virtual TaskItem? Task { get; set; }

    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
    public virtual ICollection<DocumentActivity> Activities { get; set; } = new List<DocumentActivity>();
}

public static class DocumentCategories
{
    public const string ProjectDocuments = "Project Documents";
    public const string TeamResources = "Team Resources";
    public const string PersonalFiles = "Personal Files";
    public const string Reports = "Reports";
    public const string Presentations = "Presentations";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ProjectDocuments, TeamResources, PersonalFiles, Reports, Presentations, Other
    };
}
