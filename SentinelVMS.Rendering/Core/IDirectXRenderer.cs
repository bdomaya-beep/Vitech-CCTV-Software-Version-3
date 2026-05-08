using SentinelVMS.Streaming.Models;

namespace SentinelVMS.Rendering.Core;

public interface IDirectXRenderer
{
    void Initialize();
    void UploadFrame(VideoFrame frame);
}
