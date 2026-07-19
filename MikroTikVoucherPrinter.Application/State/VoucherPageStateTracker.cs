using System;
using System.Collections.Generic;

namespace MikroTikVoucherPrinter.Application.State;

public interface IVoucherPageStateTracker
{
    string SelectedNodeId { get; set; }
    string SelectedNodeCategory { get; set; }
    string SelectedNodeValue { get; set; }
    string SearchText { get; set; }
    string FilterStatus { get; set; }
    string FilterSync { get; set; }
    string FilterProfile { get; set; }
    int PageNumber { get; set; }
    int PageSize { get; set; }
    HashSet<Guid> SelectedVoucherIds { get; }
    bool HasSavedState { get; set; }
    bool IsExactSearch { get; set; }
    void Reset();
}

public class VoucherPageStateTracker : IVoucherPageStateTracker
{
    public string SelectedNodeId { get; set; } = "all";
    public string SelectedNodeCategory { get; set; } = "all";
    public string SelectedNodeValue { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public string FilterStatus { get; set; } = "All";
    public string FilterSync { get; set; } = "All";
    public string FilterProfile { get; set; } = "كل الباقات";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public HashSet<Guid> SelectedVoucherIds { get; } = new();
    public bool HasSavedState { get; set; } = false;
    public bool IsExactSearch { get; set; } = false;

    public void Reset()
    {
        SelectedNodeId = "all";
        SelectedNodeCategory = "all";
        SelectedNodeValue = string.Empty;
        SearchText = string.Empty;
        FilterStatus = "All";
        FilterSync = "All";
        FilterProfile = "كل الباقات";
        PageNumber = 1;
        PageSize = 50;
        SelectedVoucherIds.Clear();
        HasSavedState = false;
        IsExactSearch = false;
    }
}
