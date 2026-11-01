using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentActivity
{
    [Key]
    public int DocumentActivityId { get; set; }

    // Null after the document is deleted; the title snapshot keeps the audit trail readable
    public int? DocumentId { get; set; }

    [Required]
    [MaxLength(255)]
    public string DocumentTitle { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }

    [Required]
    public DocumentActivityType ActivityType { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }

    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("DocumentId")]
    public virtual Document? Document { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

public enum DocumentActivityType
{
    Upload,
    Download,
    Preview,
    EditMetadata,
    ReplaceFile,
    Delete,
    Share,
    RevokeShare
}
