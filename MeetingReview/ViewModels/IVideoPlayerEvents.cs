namespace MeetingReview.ViewModels;

internal interface IVideoPlayerEvents
{
    event EventHandler<long>? TimeChanged;
    void Seek(long positionMs);
}
