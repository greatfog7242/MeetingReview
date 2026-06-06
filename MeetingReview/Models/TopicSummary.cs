using CommunityToolkit.Mvvm.ComponentModel;

namespace MeetingReview.Models;

public partial class TopicSummary : ObservableObject
{
    public required string Title { get; init; }
    public required string DetailedContent { get; init; }
    public long StartMs { get; init; }
    public long EndMs { get; init; }
    [ObservableProperty] private bool _isExpanded;
}
