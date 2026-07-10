namespace ConfigForge.Blazor.Components;

/// <summary>
/// Identifies one entry of a collection category: the category's index in the sidebar and
/// the entry's key within the backing map. Raised by <c>SidebarNav</c> when the user selects
/// or removes a collection entry.
/// </summary>
/// <param name="CategoryIndex">The index of the collection category.</param>
/// <param name="EntryKey">The entry key within the category's backing map.</param>
public readonly record struct CollectionEntryRef(int CategoryIndex, string EntryKey);
