using System.Text;

namespace ContosoDashboard.Services;

// Training scanner: detects the standard EICAR test string. Replace with a real scanner in production.
public class LocalSignatureScanService : IMalwareScanService
{
    // Assembled from parts so this source file is not itself flagged by antivirus tools
    public static readonly string EicarTestString =
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$" + "EICAR-STANDARD-ANTIVIRUS" + "-TEST-FILE!$H+H*";

    private static readonly byte[] EicarSignature = Encoding.ASCII.GetBytes(EicarTestString);

    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(30);

    public async Task<MalwareScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var originalPosition = 0L;
        var canSeek = false;

        try
        {
            canSeek = content.CanSeek;
            if (canSeek)
            {
                originalPosition = content.Position;
                content.Position = 0;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ScanTimeout);

            var infected = await ContainsSignatureAsync(content, timeout.Token);

            return infected
                ? new MalwareScanResult(MalwareScanStatus.Infected, "The file contains a known malware test signature.")
                : new MalwareScanResult(MalwareScanStatus.Clean);
        }
        catch (Exception)
        {
            return new MalwareScanResult(MalwareScanStatus.Unavailable, "The malware scan could not be completed.");
        }
        finally
        {
            try
            {
                if (canSeek)
                    content.Position = originalPosition;
            }
            catch (Exception)
            {
                // Stream state is the caller's concern; the scan result is already decided
            }
        }
    }

    // Scans in chunks, carrying over the tail so a signature spanning two chunks is still found
    private static async Task<bool> ContainsSignatureAsync(Stream content, CancellationToken ct)
    {
        const int chunkSize = 64 * 1024;
        var overlap = EicarSignature.Length - 1;
        var buffer = new byte[chunkSize + overlap];
        var carried = 0;

        while (true)
        {
            var read = await content.ReadAsync(buffer.AsMemory(carried, chunkSize), ct);
            if (read == 0)
                return false;

            var total = carried + read;
            if (buffer.AsSpan(0, total).IndexOf(EicarSignature) >= 0)
                return true;

            carried = Math.Min(overlap, total);
            Buffer.BlockCopy(buffer, total - carried, buffer, 0, carried);
        }
    }
}
