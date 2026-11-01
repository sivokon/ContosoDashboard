using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestSupport;

namespace ContosoDashboard.Tests;

public class DocumentFileValidatorTests
{
    [Theory]
    [InlineData("a.pdf", "pdf")]
    [InlineData("a.docx", "docx")]
    [InlineData("a.xlsx", "docx")]
    [InlineData("a.pptx", "docx")]
    [InlineData("a.doc", "doc")]
    [InlineData("a.xls", "doc")]
    [InlineData("a.ppt", "doc")]
    [InlineData("a.png", "png")]
    [InlineData("a.jpg", "jpeg")]
    [InlineData("a.JPEG", "jpeg")]
    [InlineData("a.txt", "txt")]
    public void WhitelistedExtensions_WithMatchingSignature_AreAccepted(string fileName, string kind)
    {
        var bytes = kind switch
        {
            "pdf" => SampleFiles.Pdf(),
            "docx" => SampleFiles.Docx(),
            "doc" => SampleFiles.Doc(),
            "png" => SampleFiles.Png(),
            "jpeg" => SampleFiles.Jpeg(),
            _ => SampleFiles.Text()
        };

        Assert.Null(DocumentFileValidator.Validate(fileName, bytes.Length, bytes));
    }

    [Fact]
    public void UnsupportedExtension_IsRejected()
    {
        var bytes = SampleFiles.Executable();
        Assert.NotNull(DocumentFileValidator.Validate("tool.exe", bytes.Length, bytes));
    }

    [Fact]
    public void RenamedFile_WithWrongSignature_IsRejected()
    {
        var bytes = SampleFiles.Executable();
        Assert.NotNull(DocumentFileValidator.Validate("tool.pdf", bytes.Length, bytes));
    }

    [Fact]
    public void TextFile_WithBinaryContent_IsRejected()
    {
        var bytes = new byte[] { 0x41, 0x00, 0x42 };
        Assert.NotNull(DocumentFileValidator.Validate("notes.txt", bytes.Length, bytes));
    }

    [Fact]
    public void ZeroByteFile_IsRejected()
    {
        Assert.NotNull(DocumentFileValidator.Validate("a.pdf", 0, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void SizeLimit_Is25MB_Inclusive()
    {
        var header = SampleFiles.Pdf(16);
        Assert.Null(DocumentFileValidator.Validate("a.pdf", 25L * 1024 * 1024, header));
        Assert.NotNull(DocumentFileValidator.Validate("a.pdf", 25L * 1024 * 1024 + 1, header));
    }

    [Theory]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void MimeType_IsDerivedFromExtension(string extension, string expected)
    {
        Assert.Equal(expected, DocumentFileValidator.GetMimeType(extension));
    }

    [Fact]
    public void LongestMimeType_FitsIn255Characters()
    {
        foreach (var extension in new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".png" })
            Assert.True(DocumentFileValidator.GetMimeType(extension)!.Length <= 255);
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/msword", false)]
    [InlineData(null, false)]
    public void Preview_IsAllowedOnlyForPdfAndImages(string? fileType, bool expected)
    {
        Assert.Equal(expected, DocumentFileValidator.IsPreviewable(fileType));
    }
}
