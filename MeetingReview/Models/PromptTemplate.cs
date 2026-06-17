using CommunityToolkit.Mvvm.ComponentModel;

namespace MeetingReview.Models;

public partial class PromptTemplate : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private PromptFormat _format = PromptFormat.Dropdown;
}
